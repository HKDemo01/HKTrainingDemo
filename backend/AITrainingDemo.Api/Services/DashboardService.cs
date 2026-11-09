using AITrainingDemo.Api.Data;
using AITrainingDemo.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AITrainingDemo.Api.Services;

public class DashboardService(AppDbContext db) : IDashboardService
{
    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        var userCount = await db.Users.CountAsync();
        var roleCount = await db.Roles.CountAsync();
        return new DashboardStatsDto(userCount, roleCount);
    }
}
