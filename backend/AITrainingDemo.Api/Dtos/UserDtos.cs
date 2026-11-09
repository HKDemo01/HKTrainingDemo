namespace AITrainingDemo.Api.Dtos;

public record UserDto(
    int Id,
    string Username,
    string DisplayName,
    string? Email,
    int? RoleId,
    string? RoleName,
    bool Enabled,
    DateTime CreatedAt);

public record CreateUserRequest(
    string Username,
    string Password,
    string DisplayName,
    string? Email,
    int? RoleId,
    bool Enabled);

public record UpdateUserRequest(
    string? Password,
    string DisplayName,
    string? Email,
    int? RoleId,
    bool Enabled);

public record UserQuery(string? Keyword, int? RoleId, int Page = 1, int PageSize = 20);
