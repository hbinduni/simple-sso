namespace Server.Auth;

/// <summary>The authenticated identity, sourced entirely from the OAuth provider (Microsoft
/// Entra ID) and carried in our JWTs. There is no database: the token is the record of truth.</summary>
public record AuthUser(
    string Id,
    string Email,
    string Name,
    string Role,
    IReadOnlyList<string> Groups);
