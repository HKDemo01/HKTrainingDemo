using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AITrainingDemo.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResult<PagedResult<UserDto>>>> GetPaged(
        [FromQuery] string? keyword,
        [FromQuery] int? roleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await userService.GetPagedAsync(new UserQuery(keyword, roleId, page, pageSize));
        return Ok(ApiResult<PagedResult<UserDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResult<UserDto>>> Create(CreateUserRequest request)
    {
        var result = await userService.CreateAsync(request);
        return Ok(ApiResult<UserDto>.Ok(result));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResult<UserDto>>> Update(int id, UpdateUserRequest request)
    {
        var currentUserId = User.GetUserId();
        var result = await userService.UpdateAsync(id, request, currentUserId);
        return Ok(ApiResult<UserDto>.Ok(result));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResult<object>>> Delete(int id)
    {
        var currentUserId = User.GetUserId();
        await userService.DeleteAsync(id, currentUserId);
        return Ok(ApiResult<object>.Ok(new { }));
    }
}
