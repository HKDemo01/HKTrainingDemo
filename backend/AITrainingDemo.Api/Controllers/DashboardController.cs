using AITrainingDemo.Api.Common;
using AITrainingDemo.Api.Dtos;
using AITrainingDemo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AITrainingDemo.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResult<DashboardStatsDto>>> GetStats()
    {
        var result = await dashboardService.GetStatsAsync();
        return Ok(ApiResult<DashboardStatsDto>.Ok(result));
    }
}
