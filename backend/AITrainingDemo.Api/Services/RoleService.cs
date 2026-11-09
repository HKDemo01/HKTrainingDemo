using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Services;

public class RoleService(AppDbContext db) : IRoleService
{
    private const int MaxPageSize = 100;

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

        var items = await q
            .OrderBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RoleDto(
                r.Id,
                r.Code,
                r.Name,
                r.Description,
                r.Users.Count,
                r.CreatedAt))
            .ToListAsync();

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
        await db.SaveChangesAsync();

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

        role.Name = request.Name.Trim();
        role.Description = request.Description;

        await db.SaveChangesAsync();

        return new RoleDto(role.Id, role.Code, role.Name, role.Description, role.Users.Count, role.CreatedAt);
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
        await db.SaveChangesAsync();
    }
}
