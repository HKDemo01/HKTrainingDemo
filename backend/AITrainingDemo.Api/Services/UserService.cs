using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Services;

public class UserService(AppDbContext db, IPasswordHasher hasher) : IUserService
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, MaxPageSize);

        IQueryable<User> q = db.Users.Include(u => u.Role);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(u => u.Username.Contains(keyword)
                            || u.DisplayName.Contains(keyword)
                            || (u.Email != null && u.Email.Contains(keyword)));
        }

        if (query.RoleId.HasValue)
        {
            q = q.Where(u => u.RoleId == query.RoleId.Value);
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto(u.Id, u.Username, u.DisplayName, u.Email, u.RoleId, u.Role!.Name, u.Enabled, u.CreatedAt))
            .ToListAsync();

        return new PagedResult<UserDto>(items, total, page, pageSize);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new BusinessException(400, "用户名不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new BusinessException(400, "姓名不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BusinessException(400, "密码不能为空");
        }

        var username = request.Username.Trim();

        if (await db.Users.AnyAsync(u => u.Username == username))
        {
            throw new BusinessException(409, "用户名已存在");
        }

        await EnsureRoleExistsAsync(request.RoleId);

        var user = new User
        {
            Username = username,
            PasswordHash = hasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email,
            RoleId = request.RoleId,
            Enabled = request.Enabled,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return await GetByIdAsync(user.Id);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest request)
    {
        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            throw new BusinessException(404, "用户不存在");
        }

        await EnsureRoleExistsAsync(request.RoleId);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new BusinessException(400, "姓名不能为空");
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email;
        user.RoleId = request.RoleId;
        user.Enabled = request.Enabled;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = hasher.Hash(request.Password);
        }

        await db.SaveChangesAsync();

        return await GetByIdAsync(user.Id);
    }

    public async Task DeleteAsync(int id, int currentUserId)
    {
        if (id == currentUserId)
        {
            throw new BusinessException(400, "不能删除当前登录用户");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            throw new BusinessException(404, "用户不存在");
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
    }

    private async Task EnsureRoleExistsAsync(int? roleId)
    {
        if (roleId is null)
        {
            return;
        }

        if (!await db.Roles.AnyAsync(r => r.Id == roleId.Value))
        {
            throw new BusinessException(400, "角色不存在");
        }
    }

    private async Task<UserDto> GetByIdAsync(int id)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
        {
            throw new BusinessException(404, "用户不存在");
        }

        return new UserDto(user.Id, user.Username, user.DisplayName, user.Email, user.RoleId, user.Role?.Name, user.Enabled, user.CreatedAt);
    }
}
