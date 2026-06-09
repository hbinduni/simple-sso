using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Server.Models;

namespace Server.Auth;

/// <summary>Issues and validates HS256 JWTs. Used both for issuing tokens and for the
/// manual refresh-token check in the /api/auth/refresh endpoint. The JwtBearer middleware
/// (configured in Program.cs) reuses <see cref="ValidationParameters"/> for access tokens.</summary>
public sealed class JwtService
{
    public static readonly TimeSpan AccessTokenExpiry = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenExpiry = TimeSpan.FromDays(7);

    private readonly SymmetricSecurityKey _key;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtService(string secret) =>
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

    public string GenerateAccessToken(AuthUser user) => Generate(user, TokenType.Access, AccessTokenExpiry);

    public string GenerateRefreshToken(AuthUser user) => Generate(user, TokenType.Refresh, RefreshTokenExpiry);

    private string Generate(AuthUser user, TokenType type, TimeSpan expiry)
    {
        var now = DateTime.UtcNow;
        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Id,
            ["email"] = user.Email,
            ["name"] = user.Name,
            ["role"] = user.Role,
            ["type"] = type.ToString().ToLowerInvariant(),
        };
        // Group memberships (display names) come from the identity provider at login and ride
        // along in the token, so the profile endpoint needs no datastore to surface them.
        if (user.Groups.Count > 0)
            claims["groups"] = user.Groups;

        var descriptor = new SecurityTokenDescriptor
        {
            Claims = claims,
            IssuedAt = now,
            Expires = now.Add(expiry),
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        };
        return _handler.CreateToken(descriptor);
    }

    /// <summary>Validates a token and returns its claims, or null if invalid/expired.</summary>
    public async Task<JwtClaims?> ValidateAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, ValidationParameters);
        if (!result.IsValid)
            return null;

        var jwt = (JsonWebToken)result.SecurityToken;
        var groups = jwt.TryGetPayloadValue<string[]>("groups", out var g) ? g : [];
        return new JwtClaims(
            jwt.GetClaim("sub").Value,
            jwt.GetClaim("email").Value,
            jwt.TryGetPayloadValue<string>("name", out var name) ? name : "",
            jwt.GetClaim("role").Value,
            jwt.GetClaim("type").Value,
            groups);
    }

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _key,
        ClockSkew = TimeSpan.FromSeconds(5),
    };
}

public record JwtClaims(string UserId, string Email, string Name, string Role, string Type, IReadOnlyList<string> Groups);
