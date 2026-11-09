namespace AITrainingDemo.Api.Dtos;

public record LoginRequest(string Username, string Password);

public record LoginUserInfo(int Id, string Username, string DisplayName, string? RoleName);

public record LoginResponse(string Token, DateTime ExpiresAt, LoginUserInfo User);
