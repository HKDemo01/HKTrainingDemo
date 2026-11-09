using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Api.Services;

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync();
}
