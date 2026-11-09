using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Services;

public class UserService(AppDbContext db, IPasswordHasher hasher) : IUserService
{
    private const int MaxPageSize = 100;

    /// <summary>与 AppDbContext 中 Username / DisplayName 的 HasMaxLength(50) 保持一致。</summary>
    private const int MaxNameLength = 50;

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

        // 偏移量用 long 计算：(page - 1) * pageSize 若是 int 运算，极大 page 会溢出成负数，
        // SQLite 会把负 OFFSET 当 0，导致请求第 N 页却返回第一页数据
        var offset = (long)(page - 1) * pageSize;
        var items = offset >= total
            ? new List<UserDto>()
            : await q
                .OrderBy(u => u.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .Select(u => new UserDto(u.Id, u.Username, u.DisplayName, u.Email, u.RoleId, u.Role!.Name, u.Enabled, u.CreatedAt))
                .ToListAsync();

        // SQLite 读回的 DateTime 是 Unspecified，统一标成 UTC，避免与创建/更新响应（带 Z）口径不一致
        items = items.Select(u => u with { CreatedAt = DateTime.SpecifyKind(u.CreatedAt, DateTimeKind.Utc) }).ToList();

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

        if (username.Length > MaxNameLength)
        {
            throw new BusinessException(400, $"用户名长度不能超过 {MaxNameLength} 个字符");
        }

        if (request.DisplayName.Trim().Length > MaxNameLength)
        {
            throw new BusinessException(400, $"姓名长度不能超过 {MaxNameLength} 个字符");
        }

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
        await SaveChangesAsync("用户名已存在");

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

        if (request.DisplayName.Trim().Length > MaxNameLength)
        {
            throw new BusinessException(400, $"姓名长度不能超过 {MaxNameLength} 个字符");
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email;
        user.RoleId = request.RoleId;
        user.Enabled = request.Enabled;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = hasher.Hash(request.Password);
        }

        await SaveChangesAsync("用户名已存在");

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

    /// <summary>
    /// 保存变更；并发写入撞上唯一索引时转成 409，避免把数据库异常冒成 500。
    /// </summary>
    private async Task SaveChangesAsync(string duplicateMessage)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new BusinessException(409, duplicateMessage);
        }
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
