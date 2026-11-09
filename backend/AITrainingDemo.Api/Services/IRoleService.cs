using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Api.Services;

public interface IRoleService
{
    Task<PagedResult<RoleDto>> GetPagedAsync(RoleQuery query);
    Task<IReadOnlyList<RoleOptionDto>> GetAllAsync();
    Task<RoleDto> CreateAsync(CreateRoleRequest request);
    Task<RoleDto> UpdateAsync(int id, UpdateRoleRequest request);
    Task DeleteAsync(int id);
}
