using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AITrainingDemo.Api.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController(IRoleService roleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<RoleDto>>>> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await roleService.GetPagedAsync(new RoleQuery(keyword, page, pageSize));
        return Ok(ApiResult<PagedResult<RoleDto>>.Ok(result));
    }

    [HttpGet("all")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<RoleOptionDto>>>> GetAll()
    {
        var result = await roleService.GetAllAsync();
        return Ok(ApiResult<IReadOnlyList<RoleOptionDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<RoleDto>>> Create(CreateRoleRequest request)
    {
        var result = await roleService.CreateAsync(request);
        return Ok(ApiResult<RoleDto>.Ok(result));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResult<RoleDto>>> Update(int id, UpdateRoleRequest request)
    {
        var result = await roleService.UpdateAsync(id, request);
        return Ok(ApiResult<RoleDto>.Ok(result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(int id)
    {
        await roleService.DeleteAsync(id);
        return Ok(ApiResult<object>.Ok(new { }));
    }
}
