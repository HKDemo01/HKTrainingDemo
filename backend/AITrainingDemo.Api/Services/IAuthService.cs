using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Api.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginUserInfo> GetCurrentUserAsync(int userId);
}
