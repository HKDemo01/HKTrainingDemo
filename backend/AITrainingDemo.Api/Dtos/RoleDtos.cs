namespace AITrainingDemo.Api.Dtos;

public record RoleDto(int Id, string Code, string Name, string? Description, int UserCount, DateTime CreatedAt);

public record RoleOptionDto(int Id, string Code, string Name);

public record CreateRoleRequest(string Code, string Name, string? Description);

public record UpdateRoleRequest(string Name, string? Description);

public record RoleQuery(string? Keyword, int Page = 1, int PageSize = 20);
