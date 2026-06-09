namespace Server.Configuration;

/// <summary>Application configuration, loaded from environment variables (see .env.example).</summary>
public sealed class AppConfig
{
    public required string Environment { get; init; }
    public required string Port { get; init; }
    public required string FrontendUrl { get; init; }
    public required string JwtSecret { get; init; }

    // Microsoft Entra ID (Azure AD) OAuth — all empty unless configured.
    public required string AzureTenantId { get; init; }
    public required string AzureClientId { get; init; }
    public required string AzureClientSecret { get; init; }
    public required string AzureRedirectUri { get; init; }

    public bool IsDevelopment => Environment == "development";

    /// <summary>True only when every Entra ID setting is present; gates the OAuth endpoints.</summary>
    public bool MicrosoftOAuthEnabled =>
        AzureTenantId.Length > 0 && AzureClientId.Length > 0 &&
        AzureClientSecret.Length > 0 && AzureRedirectUri.Length > 0;

    public string[] AllowedOrigins =>
    [
        FrontendUrl,
        "http://localhost:5173",
        "http://localhost:3000",
    ];

    /// <summary>Dev-only HS256 signing key. Never reaches production: <see cref="Load"/>
    /// throws when ENVIRONMENT != "development" unless a strong JWT_SECRET is supplied.</summary>
    private const string DevJwtSecret = "dev-secret-key-change-in-production";

    public static AppConfig Load()
    {
        static string Env(string key, string fallback) =>
            System.Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

        var environment = Env("ENVIRONMENT", "development");
        var isDevelopment = environment == "development";

        // HS256 requires a 256-bit (32-byte) key. Outside development we refuse to start with a
        // missing, weak, or placeholder secret: a known signing key lets anyone forge tokens,
        // which is a complete authentication bypass.
        var jwtSecret = Env("JWT_SECRET", isDevelopment ? DevJwtSecret : "");
        if (!isDevelopment && (jwtSecret.Length < 32 || jwtSecret == DevJwtSecret))
        {
            throw new InvalidOperationException(
                "JWT_SECRET must be set to a strong value (at least 32 characters) when ENVIRONMENT " +
                "is not 'development'. Generate one with: openssl rand -base64 32");
        }

        return new AppConfig
        {
            Environment = environment,
            Port = Env("PORT", "3000"),
            FrontendUrl = Env("FRONTEND_URL", "http://localhost:5173"),
            JwtSecret = jwtSecret,
            AzureTenantId = Env("AZURE_TENANT_ID", ""),
            AzureClientId = Env("AZURE_CLIENT_ID", ""),
            AzureClientSecret = Env("AZURE_CLIENT_SECRET", ""),
            AzureRedirectUri = Env("AZURE_REDIRECT_URI", ""),
        };
    }
}
