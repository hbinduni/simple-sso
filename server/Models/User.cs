using System.Text.Json.Serialization;

namespace Server.Models;

public sealed class User
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";

    [JsonIgnore] // never sent to the client
    public string? PasswordHash { get; set; }

    public string Name { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
