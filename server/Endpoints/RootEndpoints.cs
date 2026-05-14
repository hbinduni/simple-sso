using Server.Data;

namespace Server.Endpoints;

public static class RootEndpoints
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public static void MapRootEndpoints(this IEndpointRouteBuilder app, Database? database)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            name = "Monorepo API",
            version = "2.0.0",
            stack = ".NET + Minimal APIs",
            features = new
            {
                authentication = "JWT (email/password)",
                oauth = "Google, Facebook, Twitter",
                ids = "TypeID (type-safe, K-sortable)",
                roles = "admin, user, moderator",
            },
            endpoints = new
            {
                auth = new
                {
                    register = "POST /api/auth/register",
                    login = "POST /api/auth/login",
                    refresh = "POST /api/auth/refresh",
                    logout = "POST /api/auth/logout",
                    me = "GET /api/auth/me",
                    sessions = "GET /api/auth/sessions",
                },
                items = new
                {
                    list = "GET /api/items",
                    get = "GET /api/items/{id}",
                    create = "POST /api/items",
                    update = "PUT /api/items/{id}",
                    delete = "DELETE /api/items/{id}",
                },
            },
        }));

        app.MapMethods("/health", ["GET", "HEAD"], async () =>
        {
            var health = new Dictionary<string, object?>
            {
                ["status"] = "healthy",
                ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["uptime"] = (DateTime.UtcNow - StartTime).TotalSeconds,
                ["memory"] = new
                {
                    alloc = GC.GetTotalMemory(forceFullCollection: false) / 1024 / 1024,
                    totalAlloc = GC.GetTotalAllocatedBytes() / 1024 / 1024,
                    sys = Environment.WorkingSet / 1024 / 1024,
                },
            };

            if (database is not null)
            {
                var ok = await database.HealthAsync();
                health["database"] = ok ? "healthy" : "unhealthy";
                if (!ok)
                    health["status"] = "degraded";
            }
            else
            {
                health["database"] = "not_configured";
            }

            return Results.Ok(health);
        });
    }
}
