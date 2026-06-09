using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server.Auth;
using Server.Common;
using Server.Configuration;

namespace Server.Endpoints;

/// <summary>
/// "Sign in with Microsoft" via the Entra ID authorization-code flow. The server is a stateless
/// SSO broker: there is no datastore. Identity (and group memberships) come straight from the
/// Entra token and are minted into our own JWTs.
///
///   1. GET /api/auth/oauth/microsoft           -> redirect the browser to Entra,
///      stashing { state, nonce, PKCE verifier } in a short-lived HttpOnly cookie.
///   2. GET /api/auth/oauth/microsoft/callback   -> verify state, exchange the code, read the
///      identity + groups, mint our JWTs, and redirect back to the SPA with the tokens in the
///      URL fragment (kept out of server/proxy logs).
/// </summary>
public static class OAuthEndpoints
{
    private const string TxCookie = "ms_oauth_tx";
    private const string CookiePath = "/api/auth/oauth/microsoft";

    public static void MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(CookiePath);
        group.MapGet("", Start);
        group.MapGet("/callback", Callback);
    }

    private static IResult Start(HttpContext ctx, MicrosoftOAuthService ms, AppConfig config)
    {
        var req = ms.BuildAuthorizeRequest();
        var tx = EncodeTx(new OAuthTx(req.State, req.Nonce, req.Verifier));
        ctx.Response.Cookies.Append(TxCookie, tx, CookieOpts(config));
        return Results.Redirect(req.Url);
    }

    private static async Task<IResult> Callback(
        HttpContext ctx,
        string? code,
        string? state,
        string? error,
        string? error_description,
        MicrosoftOAuthService ms,
        AppConfig config,
        JwtService jwt,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var txCookie = ctx.Request.Cookies[TxCookie];
        ctx.Response.Cookies.Delete(TxCookie, CookieOpts(config));

        if (!string.IsNullOrEmpty(error))
            return ToClient(config, "error", error_description ?? error);
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) || txCookie is null)
            return ToClient(config, "error", "invalid_oauth_response");

        if (DecodeTx(txCookie) is not { } tx || !FixedTimeEquals(tx.State, state))
            return ToClient(config, "error", "state_mismatch");

        try
        {
            var info = await ms.ExchangeCodeAsync(code, tx.Verifier, tx.Nonce, ct);

            // Best-effort group lookup; carried in the JWT so /api/auth/me can surface it.
            var groups = await ms.GetGroupNamesAsync(info.GraphAccessToken, ct);

            var user = new AuthUser(
                Id: info.Oid, // Entra object id — stable per-user identifier
                Email: Validation.NormalizeEmail(info.Email),
                Name: info.Name,
                Role: "user",
                Groups: groups);

            var fragment =
                $"accessToken={Uri.EscapeDataString(jwt.GenerateAccessToken(user))}" +
                $"&refreshToken={Uri.EscapeDataString(jwt.GenerateRefreshToken(user))}" +
                $"&expiresIn={(int)JwtService.AccessTokenExpiry.TotalSeconds}" +
                $"&tokenType=Bearer";
            return Results.Redirect($"{config.FrontendUrl.TrimEnd('/')}/auth/callback#{fragment}");
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("OAuth.Microsoft").LogError(ex, "Microsoft OAuth callback failed");
            return ToClient(config, "error", "token_exchange_failed");
        }
    }

    // Redirect the SPA to /auth/callback with a single fragment param. Always a fragment so
    // tokens/errors never land in access logs, the Referer header, or browser history.
    private static IResult ToClient(AppConfig config, string key, string value) =>
        Results.Redirect($"{config.FrontendUrl.TrimEnd('/')}/auth/callback#{key}={Uri.EscapeDataString(value)}");

    private static CookieOptions CookieOpts(AppConfig config) => new()
    {
        HttpOnly = true,
        Secure = !config.IsDevelopment,
        SameSite = SameSiteMode.Lax, // sent on the top-level GET redirect back from Entra
        Path = CookiePath,
        MaxAge = TimeSpan.FromMinutes(10),
    };

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static string EncodeTx(OAuthTx tx) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(tx))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static OAuthTx? DecodeTx(string value)
    {
        try
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            return JsonSerializer.Deserialize<OAuthTx>(Convert.FromBase64String(padded));
        }
        catch
        {
            return null;
        }
    }

    private sealed record OAuthTx(string State, string Nonce, string Verifier);
}
