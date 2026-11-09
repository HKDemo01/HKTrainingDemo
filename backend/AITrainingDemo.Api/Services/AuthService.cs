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
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        // 用户不存在、已停用、密码错误一律返回同一提示，避免用户名枚举
        if (user is null || !user.Enabled || !hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new BusinessException(401, "用户名或密码错误");
        }

        var expiresAt = DateTime.UtcNow.AddHours(8);
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
}
