using AITrainingDemo.Api.Dtos;

namespace AITrainingDemo.Api.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetPagedAsync(UserQuery query);
    Task<UserDto> CreateAsync(CreateUserRequest request);
    Task<UserDto> UpdateAsync(int id, UpdateUserRequest request, int currentUserId);
    Task DeleteAsync(int id, int currentUserId);
}
