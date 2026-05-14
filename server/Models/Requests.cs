namespace Server.Models;

public record RegisterRequest(string? Email, string? Password, string? Name);

public record LoginRequest(string? Email, string? Password);

public record RefreshTokenRequest(string? RefreshToken);

/// <summary>Body for create/update item. Fields are nullable so "not provided" is distinguishable.</summary>
public record ItemRequest(string? Title, string? Description, ItemStatus? Status);
