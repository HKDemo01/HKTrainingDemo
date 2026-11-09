using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Services;

public class RoleService(AppDbContext db) : IRoleService
{
    private const int MaxPageSize = 100;

    /// <summary>与 AppDbContext 中 Code / Name 的 HasMaxLength(50) 保持一致。</summary>
    private const int MaxNameLength = 50;

    public async Task<PagedResult<RoleDto>> GetPagedAsync(RoleQuery query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, MaxPageSize);

        IQueryable<Role> q = db.Roles;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            q = q.Where(r => r.Code.Contains(keyword) || r.Name.Contains(keyword));
        }

        var total = await q.CountAsync();

        // 偏移量用 long 计算：(page - 1) * pageSize 若是 int 运算，极大 page 会溢出成负数，
        // SQLite 会把负 OFFSET 当 0，导致请求第 N 页却返回第一页数据
        var offset = (long)(page - 1) * pageSize;
        var items = offset >= total
            ? new List<RoleDto>()
            : await q
                .OrderBy(r => r.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .Select(r => new RoleDto(
                    r.Id,
                    r.Code,
                    r.Name,
                    r.Description,
                    r.Users.Count,
                    r.CreatedAt))
                .ToListAsync();

        // SQLite 读回的 DateTime 是 Unspecified，统一标成 UTC，避免与创建/更新响应（带 Z）口径不一致
        items = items.Select(r => r with { CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc) }).ToList();

        return new PagedResult<RoleDto>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyList<RoleOptionDto>> GetAllAsync()
    {
        return await db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new RoleOptionDto(r.Id, r.Code, r.Name))
            .ToListAsync();
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new BusinessException(400, "角色编码不能为空");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessException(400, "角色名称不能为空");
        }

        var code = request.Code.Trim();

        if (code.Length > MaxNameLength)
        {
            throw new BusinessException(400, $"角色编码长度不能超过 {MaxNameLength} 个字符");
        }

        if (request.Name.Trim().Length > MaxNameLength)
        {
            throw new BusinessException(400, $"角色名称长度不能超过 {MaxNameLength} 个字符");
        }

        if (await db.Roles.AnyAsync(r => r.Code == code))
        {
            throw new BusinessException(409, "角色编码已存在");
        }

        var role = new Role
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };

        db.Roles.Add(role);
        await SaveChangesAsync();

        return new RoleDto(role.Id, role.Code, role.Name, role.Description, 0, role.CreatedAt);
    }

    public async Task<RoleDto> UpdateAsync(int id, UpdateRoleRequest request)
    {
        var role = await db.Roles.Include(r => r.Users).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null)
        {
            throw new BusinessException(404, "角色不存在");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessException(400, "角色名称不能为空");
        }

        if (request.Name.Trim().Length > MaxNameLength)
        {
            throw new BusinessException(400, $"角色名称长度不能超过 {MaxNameLength} 个字符");
        }

        role.Name = request.Name.Trim();
        role.Description = request.Description;

        // 角色编码不可修改，这里不会触发唯一索引冲突，直接保存
        await db.SaveChangesAsync();

        return new RoleDto(role.Id, role.Code, role.Name, role.Description, role.Users.Count, role.CreatedAt)
            with { CreatedAt = DateTime.SpecifyKind(role.CreatedAt, DateTimeKind.Utc) };
    }

    public async Task DeleteAsync(int id)
    {
        var role = await db.Roles.Include(r => r.Users).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null)
        {
            throw new BusinessException(404, "角色不存在");
        }

        // 角色仍被用户引用时先给出友好提示，避免外键异常冒成 500
        if (role.Users.Count > 0)
        {
            throw new BusinessException(409, $"该角色已被 {role.Users.Count} 个用户引用，无法删除");
        }

        db.Roles.Remove(role);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase) == true)
        {
            // 检查与删除之间存在竞态窗口：并发给该角色挂上用户时，删除会撞外键约束
            throw new BusinessException(409, "该角色已被引用，无法删除");
        }
    }

    /// <summary>
    /// 保存变更；并发写入撞上唯一索引时转成 409，避免把数据库异常冒成 500。
    /// 当前 Roles 表只有一个唯一索引（Code），因此任何唯一冲突都只能是角色编码重复。
    /// </summary>
    private async Task SaveChangesAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new BusinessException(409, "角色编码已存在");
        }
    }
}
