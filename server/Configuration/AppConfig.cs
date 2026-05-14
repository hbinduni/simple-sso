namespace Server.Configuration;

/// <summary>Application configuration, loaded from environment variables (see .env.example).</summary>
public sealed class AppConfig
{
    public required string Environment { get; init; }
    public required string Port { get; init; }
    public required string DatabaseUrl { get; init; }
    public required string FrontendUrl { get; init; }
    public required string JwtSecret { get; init; }

    public bool IsDevelopment => Environment == "development";

    public string[] AllowedOrigins =>
    [
        FrontendUrl,
        "http://localhost:5173",
        "http://localhost:3000",
    ];

    public static AppConfig Load()
    {
        static string Env(string key, string fallback) =>
            System.Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

        return new AppConfig
        {
            Environment = Env("ENVIRONMENT", "development"),
            Port = Env("PORT", "3000"),
            DatabaseUrl = Env("DATABASE_URL", ""),
            FrontendUrl = Env("FRONTEND_URL", "http://localhost:5173"),
            JwtSecret = Env("JWT_SECRET", "your-secret-key-change-in-production"),
        };
    }
}
