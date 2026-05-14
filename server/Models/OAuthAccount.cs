using System.Text.Json.Serialization;

namespace Server.Models;

public sealed class OAuthAccount
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public OAuthProvider Provider { get; set; }
    public string ProviderAccountId { get; set; } = "";

    [JsonIgnore] // never sent to the client
    public string? AccessToken { get; set; }

    [JsonIgnore] // never sent to the client
    public string? RefreshToken { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
