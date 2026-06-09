using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Server.Configuration;

namespace Server.Auth;

/// <summary>
/// Microsoft Entra ID (Azure AD) OAuth 2.0 authorization-code flow with PKCE.
///
/// This is a confidential client: the code-for-token exchange is a server-to-server
/// call authenticated with the client secret over TLS, so the returned id_token is
/// trusted without front-channel signature validation (Microsoft's guidance for the
/// auth-code flow). We still verify the audience and tenant claims defensively.
/// </summary>
public sealed class MicrosoftOAuthService(HttpClient http, AppConfig config)
{
    // GroupMember.Read.All lets the returned access token read the user's group memberships
    // from Microsoft Graph (needs admin consent on the app registration).
    private const string Scope = "openid profile email https://graph.microsoft.com/GroupMember.Read.All";

    // Cap on group names carried in our JWT, to keep the token (and the redirect URL) bounded.
    private const int MaxGroups = 100;

    private string Authority => $"https://login.microsoftonline.com/{config.AzureTenantId}";

    /// <summary>Builds the /authorize redirect plus the CSRF state, OIDC nonce, and PKCE
    /// verifier the caller must stash (in a cookie) and hand back to <see cref="ExchangeCodeAsync"/>.</summary>
    public AuthorizeRequest BuildAuthorizeRequest()
    {
        var state = RandomToken();
        var nonce = RandomToken();
        var verifier = RandomToken();
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var url = QueryHelpers.AddQueryString($"{Authority}/oauth2/v2.0/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = config.AzureClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = config.AzureRedirectUri,
            ["response_mode"] = "query",
            ["scope"] = Scope,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        });

        return new AuthorizeRequest(state, nonce, verifier, url);
    }

    /// <summary>Exchanges the authorization code for tokens and returns the verified user identity.</summary>
    public async Task<MicrosoftUser> ExchangeCodeAsync(
        string code, string codeVerifier, string expectedNonce, CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config.AzureClientId,
            ["client_secret"] = config.AzureClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = config.AzureRedirectUri,
            ["scope"] = Scope,
            ["code_verifier"] = codeVerifier,
        });

        using var resp = await http.PostAsync($"{Authority}/oauth2/v2.0/token", form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Entra token exchange failed ({(int)resp.StatusCode})");

        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("id_token", out var idTokenEl) ||
            idTokenEl.GetString() is not { Length: > 0 } idToken)
            throw new InvalidOperationException("Entra token response had no id_token");

        // Graph access token — used right away to read group memberships, never persisted.
        var graphToken = json.RootElement.TryGetProperty("access_token", out var atEl)
            ? atEl.GetString() ?? ""
            : "";

        var jwt = new JsonWebToken(idToken);

        if (Claim(jwt, "aud") != config.AzureClientId)
            throw new InvalidOperationException("id_token audience mismatch");
        if (Claim(jwt, "tid") != config.AzureTenantId)
            throw new InvalidOperationException("id_token tenant mismatch");
        if (Claim(jwt, "nonce") is { } nonce && nonce != expectedNonce)
            throw new InvalidOperationException("id_token nonce mismatch");

        // `oid` is the immutable per-tenant object id — the stable key for this account.
        var oid = Claim(jwt, "oid")
            ?? throw new InvalidOperationException("id_token missing oid claim");
        // Work/school accounts expose the address via `email` or, failing that, `preferred_username`.
        var email = Claim(jwt, "email") ?? Claim(jwt, "preferred_username")
            ?? throw new InvalidOperationException("id_token missing email/preferred_username claim");
        var name = Claim(jwt, "name") ?? email;

        return new MicrosoftUser(oid, email, name, graphToken);
    }

    /// <summary>Reads the user's group display names from Microsoft Graph. Returns an empty list
    /// (never throws) if the token lacks consent or the call fails — group info is best-effort.</summary>
    public async Task<IReadOnlyList<string>> GetGroupNamesAsync(string graphAccessToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(graphAccessToken))
            return [];

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https://graph.microsoft.com/v1.0/me/memberOf/microsoft.graph.group?$select=displayName&$top=999");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", graphAccessToken);

            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return [];

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("value", out var values))
                return [];

            var names = new List<string>();
            foreach (var group in values.EnumerateArray())
            {
                if (group.TryGetProperty("displayName", out var dn) && dn.GetString() is { Length: > 0 } name)
                    names.Add(name);
                if (names.Count >= MaxGroups)
                    break;
            }
            return names;
        }
        catch
        {
            return [];
        }
    }

    private static string? Claim(JsonWebToken jwt, string key) =>
        jwt.TryGetPayloadValue<string>(key, out var value) ? value : null;

    private static string RandomToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public record AuthorizeRequest(string State, string Nonce, string Verifier, string Url);

public record MicrosoftUser(string Oid, string Email, string Name, string GraphAccessToken);
