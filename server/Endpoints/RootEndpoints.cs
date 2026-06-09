namespace Server.Endpoints;

public static class RootEndpoints
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public static void MapRootEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            name = "Simple SSO",
            version = "2.0.0",
            stack = ".NET + Minimal APIs",
            features = new
            {
                authentication = "Microsoft Entra ID (OAuth 2.0 + PKCE)",
                tokens = "Stateless JWT (access + refresh)",
                stateless = "No database",
            },
            endpoints = new
            {
                auth = new
                {
                    microsoft = "GET /api/auth/oauth/microsoft",
                    microsoftCallback = "GET /api/auth/oauth/microsoft/callback",
                    refresh = "POST /api/auth/refresh",
                    logout = "POST /api/auth/logout",
                    me = "GET /api/auth/me",
                },
            },
        }));

        app.MapMethods("/health", ["GET", "HEAD"], () => Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            uptime = (DateTime.UtcNow - StartTime).TotalSeconds,
            memory = new
            {
                alloc = GC.GetTotalMemory(forceFullCollection: false) / 1024 / 1024,
                totalAlloc = GC.GetTotalAllocatedBytes() / 1024 / 1024,
                sys = Environment.WorkingSet / 1024 / 1024,
            },
        }));
    }
}
