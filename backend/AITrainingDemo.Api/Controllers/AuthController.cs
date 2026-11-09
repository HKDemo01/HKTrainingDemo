using System.Security.Claims;
using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AITrainingDemo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResult<LoginResponse>>> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        return Ok(ApiResult<LoginResponse>.Ok(result));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResult<LoginUserInfo>>> Me()
    {
        var userId = int.Parse(User.FindFirstValue("sub")!);
        var result = await authService.GetCurrentUserAsync(userId);
        return Ok(ApiResult<LoginUserInfo>.Ok(result));
    }
}
