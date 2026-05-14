namespace Server.Models;

public sealed class Session
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
