using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AITrainingDemo.Api.Services;

public class AuthService(AppDbContext db, IPasswordHasher hasher, IConfiguration configuration) : IAuthService
{
    /// <summary>
    /// 等时化用的哑元哈希：固定的盐与哈希。用户不存在或已停用时也跑一次同参数的 PBKDF2，
    /// 否则「用户不存在」比「口令错误」快一个数量级，可用响应时间枚举出有效用户名。
    /// </summary>
    private static readonly string DummyHash =
        $"{Convert.ToBase64String(new byte[16])}.{Convert.ToBase64String(new byte[32])}";

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        // 用户不存在、已停用、密码错误一律返回同一提示，避免用户名枚举
        if (user is null || !user.Enabled)
        {
            hasher.Verify(request.Password, DummyHash);
            throw new BusinessException(401, "用户名或密码错误");
        }

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new BusinessException(401, "用户名或密码错误");
        }

        var expiresAt = DateTime.UtcNow.AddHours(GetExpiresHours());
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.Username)
        };

        if (user.Role is not null)
        {
            claims.Add(new Claim("role", user.Role.Code));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            new LoginUserInfo(user.Id, user.Username, user.DisplayName, user.Role?.Name));
    }

    public async Task<LoginUserInfo> GetCurrentUserAsync(int userId)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            throw new BusinessException(404, "用户不存在");
        }

        return new LoginUserInfo(user.Id, user.Username, user.DisplayName, user.Role?.Name);
    }

    /// <summary>Token 有效期（小时），取自配置 Jwt:ExpiresHours，缺省或非法时回退 8 小时。</summary>
    private int GetExpiresHours()
    {
        return int.TryParse(configuration["Jwt:ExpiresHours"], out var hours) && hours > 0 ? hours : 8;
    }
}
