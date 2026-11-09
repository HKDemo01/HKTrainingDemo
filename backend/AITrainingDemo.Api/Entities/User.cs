using System.Text.Json.Serialization;

namespace AITrainingDemo.Api.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int? RoleId { get; set; }
    public Role? Role { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
