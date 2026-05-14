# Go → .NET Server Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Go/Fiber server in `server/` with an ASP.NET Core (.NET 10 LTS) Minimal API that keeps the existing HTTP contract, and update `Makefile`, `server/Dockerfile`, `k8s/`, `docker-compose.yml`, and root `package.json` to match.

**Architecture:** ASP.NET Core Minimal APIs. Data access via Dapper over `NpgsqlDataSource` — `db/schema.sql` stays the source of truth. JWT issued/validated by a `JwtService`; protected endpoints guarded by the `JwtBearer` middleware with a custom `access`-token policy and a `JwtBearerEvents` shim that emits the existing `{success,error}` envelope. TypeID generated from `Guid.CreateVersion7()` + Crockford base32.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, Dapper, Npgsql, BCrypt.Net-Next, Microsoft.AspNetCore.Authentication.JwtBearer, DotNetEnv, Microsoft.AspNetCore.OpenApi.

**Verification approach:** This is a port, not greenfield TDD. Each task's gate is `dotnet build` (compile) plus, where applicable, `dotnet run` + `curl` against the live endpoint. Task 11 does a full contract walk-through.

**Reference:** The Go source being ported lives in `server/` (`main.go`, `config/`, `database/`, `handlers/`, `middleware/`, `models/`, `routes/`, `utils/`, `version.go`). Read the matching Go file before porting each piece. Design spec: `docs/superpowers/specs/2026-05-14-dotnet-server-migration-design.md`.

---

## Task 1: Scaffold the .NET project and remove the Go server

**Files:**
- Delete: `server/main.go`, `server/version.go`, `server/go.mod`, `server/go.sum`, `server/.golangci.yml`, `server/config/`, `server/database/`, `server/handlers/`, `server/middleware/`, `server/models/`, `server/routes/`, `server/utils/`, `server/bin/`
- Keep: `server/.env.example`, `server/Dockerfile` (rewritten in Task 8)
- Create: `server/Server.csproj`
- Create: `server/Program.cs`
- Create: `server/appsettings.json`
- Create: `server/appsettings.Development.json`

- [ ] **Step 1: Remove the Go server files**

```bash
cd /home/binduni/projects-dotnet/bun-dotnet-react-monorepo
git rm -r server/config server/database server/handlers server/middleware server/models server/routes server/utils
git rm server/main.go server/version.go server/go.mod server/go.sum server/.golangci.yml
rm -rf server/bin
```

- [ ] **Step 2: Create `server/Server.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>Server</RootNamespace>
    <AssemblyName>server</AssemblyName>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

</Project>
```

- [ ] **Step 3: Add NuGet packages (resolves current versions automatically)**

Run:
```bash
cd server
dotnet add package Dapper
dotnet add package Npgsql
dotnet add package BCrypt.Net-Next
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package DotNetEnv
dotnet add package Microsoft.AspNetCore.OpenApi
```
Expected: each command writes a `<PackageReference>` into `Server.csproj`.

- [ ] **Step 4: Create `server/appsettings.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 5: Create `server/appsettings.Development.json`**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

- [ ] **Step 6: Create a minimal `server/Program.cs` (full wiring comes in Task 7)**

```csharp
var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } p ? p : "3000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { name = "Monorepo API", stack = ".NET + Minimal APIs" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapFallback((HttpContext ctx) =>
    Results.Json(
        new { success = false, error = $"Route not found: {ctx.Request.Method} {ctx.Request.Path}" },
        statusCode: StatusCodes.Status404NotFound));

Console.WriteLine($"🚀 Server starting on port {port}");
app.Run();

public partial class Program;
```

- [ ] **Step 7: Build and run to verify scaffold**

Run:
```bash
cd server && dotnet build
```
Expected: `Build succeeded`.

Run (in a second shell, or background then curl then kill):
```bash
cd server && dotnet run &
sleep 5
curl -s localhost:3000/ ; echo
curl -s localhost:3000/health ; echo
curl -s localhost:3000/nope -w ' [%{http_code}]' ; echo
kill %1
```
Expected: root + health JSON, and `{"success":false,"error":"Route not found: GET /nope"} [404]`.

- [ ] **Step 8: Commit**

```bash
cd /home/binduni/projects-dotnet/bun-dotnet-react-monorepo
git add -A server/
git commit -m "feat(server): scaffold .NET 10 minimal API, remove Go server"
```

---

## Task 2: Common helpers, configuration, and version

**Files:**
- Create: `server/Common/TypeId.cs`
- Create: `server/Common/Validation.cs`
- Create: `server/Common/ClientIp.cs`
- Create: `server/Configuration/AppConfig.cs`
- Create: `server/Version.cs`

Reference Go originals: `server` git history `utils/typeid.go`, `utils/validation.go`, `middleware/auth.go` (`GetClientIP`), `config/config.go`, `version.go`.

- [ ] **Step 1: Create `server/Common/TypeId.cs`**

```csharp
namespace Server.Common;

/// <summary>
/// TypeID generation: a type-prefixed, K-sortable identifier of the form
/// {prefix}_{26-char Crockford base32 of a UUIDv7}. Matches the format produced
/// by the Go go.jetify.com/typeid library that the previous server used.
/// </summary>
public static class TypeId
{
    // Crockford base32, lowercase — same alphabet the TypeID spec mandates.
    private const string Alphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    public static string NewUserId() => New("user");
    public static string NewItemId() => New("item");
    public static string NewSessionId() => New("sess");
    public static string NewOAuthAccountId() => New("oauth");

    public static string New(string prefix)
    {
        var guid = Guid.CreateVersion7();
        Span<byte> bytes = stackalloc byte[16];
        guid.TryWriteBytes(bytes, bigEndian: true, out _);
        return $"{prefix}_{EncodeBase32(bytes)}";
    }

    /// <summary>Loose validation: a non-empty prefix, an underscore, a non-empty suffix.</summary>
    public static bool IsValid(string id)
    {
        var idx = id.IndexOf('_');
        return idx > 0 && idx < id.Length - 1;
    }

    // 16 bytes (128 bits) -> 26 base32 chars. Bit layout per the TypeID spec.
    private static string EncodeBase32(ReadOnlySpan<byte> b)
    {
        Span<char> c = stackalloc char[26];
        c[0]  = Alphabet[(b[0] & 0xE0) >> 5];
        c[1]  = Alphabet[b[0] & 0x1F];
        c[2]  = Alphabet[(b[1] & 0xF8) >> 3];
        c[3]  = Alphabet[((b[1] & 0x07) << 2) | ((b[2] & 0xC0) >> 6)];
        c[4]  = Alphabet[(b[2] & 0x3E) >> 1];
        c[5]  = Alphabet[((b[2] & 0x01) << 4) | ((b[3] & 0xF0) >> 4)];
        c[6]  = Alphabet[((b[3] & 0x0F) << 1) | ((b[4] & 0x80) >> 7)];
        c[7]  = Alphabet[(b[4] & 0x7C) >> 2];
        c[8]  = Alphabet[((b[4] & 0x03) << 3) | ((b[5] & 0xE0) >> 5)];
        c[9]  = Alphabet[b[5] & 0x1F];
        c[10] = Alphabet[(b[6] & 0xF8) >> 3];
        c[11] = Alphabet[((b[6] & 0x07) << 2) | ((b[7] & 0xC0) >> 6)];
        c[12] = Alphabet[(b[7] & 0x3E) >> 1];
        c[13] = Alphabet[((b[7] & 0x01) << 4) | ((b[8] & 0xF0) >> 4)];
        c[14] = Alphabet[((b[8] & 0x0F) << 1) | ((b[9] & 0x80) >> 7)];
        c[15] = Alphabet[(b[9] & 0x7C) >> 2];
        c[16] = Alphabet[((b[9] & 0x03) << 3) | ((b[10] & 0xE0) >> 5)];
        c[17] = Alphabet[b[10] & 0x1F];
        c[18] = Alphabet[(b[11] & 0xF8) >> 3];
        c[19] = Alphabet[((b[11] & 0x07) << 2) | ((b[12] & 0xC0) >> 6)];
        c[20] = Alphabet[(b[12] & 0x3E) >> 1];
        c[21] = Alphabet[((b[12] & 0x01) << 4) | ((b[13] & 0xF0) >> 4)];
        c[22] = Alphabet[((b[13] & 0x0F) << 1) | ((b[14] & 0x80) >> 7)];
        c[23] = Alphabet[(b[14] & 0x7C) >> 2];
        c[24] = Alphabet[((b[14] & 0x03) << 3) | ((b[15] & 0xE0) >> 5)];
        c[25] = Alphabet[b[15] & 0x1F];
        return new string(c);
    }
}
```

- [ ] **Step 2: Create `server/Common/Validation.cs`**

```csharp
using System.Text.RegularExpressions;

namespace Server.Common;

public static partial class Validation
{
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    public static bool IsValidEmail(string email) => EmailRegex().IsMatch(email);

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValidPassword(string password) => password.Length >= 8;
}
```

- [ ] **Step 3: Create `server/Common/ClientIp.cs`**

```csharp
namespace Server.Common;

/// <summary>Extracts the client IP, honouring proxy headers (X-Forwarded-For, X-Real-IP).</summary>
public static class ClientIp
{
    public static string? Get(HttpContext ctx)
    {
        var xff = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xff))
        {
            var comma = xff.IndexOf(',');
            return comma >= 0 ? xff[..comma].Trim() : xff.Trim();
        }

        var xri = ctx.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xri))
            return xri;

        return ctx.Connection.RemoteIpAddress?.ToString();
    }
}
```

- [ ] **Step 4: Create `server/Configuration/AppConfig.cs`**

```csharp
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
```

- [ ] **Step 5: Create `server/Version.cs`**

```csharp
namespace Server;

/// <summary>Server version. The `Version = "x.y.z"` literal is bumped by `make version-up`.</summary>
public static class AppVersion
{
    public const string Version = "1.0.5";
}
```

- [ ] **Step 6: Build to verify it compiles**

Run: `cd server && dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add server/Common server/Configuration server/Version.cs
git commit -m "feat(server): add common helpers, config loader, version"
```

---

## Task 3: Models

**Files:**
- Create: `server/Models/Enums.cs`
- Create: `server/Models/User.cs`
- Create: `server/Models/Item.cs`
- Create: `server/Models/Session.cs`
- Create: `server/Models/OAuthAccount.cs`
- Create: `server/Models/Requests.cs`
- Create: `server/Models/Responses.cs`

Reference Go originals: `models/models.go`, `models/response.go`.

Notes:
- Timestamps are `DateTime` (not `DateTimeOffset`) — Npgsql maps `TIMESTAMP WITH TIME ZONE` to a UTC-kind `DateTime`. Always construct with `DateTime.UtcNow`.
- Enums are PascalCase in C#; they serialize to lowercase JSON via `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` configured in Task 7, and are written to the DB as lowercase strings explicitly in the repositories (Task 5).
- Property names are PascalCase; `Dapper.DefaultTypeMap.MatchNamesWithUnderscores` (set in Task 7) maps snake_case columns, and System.Text.Json's web defaults emit camelCase JSON.

- [ ] **Step 1: Create `server/Models/Enums.cs`**

```csharp
namespace Server.Models;

public enum UserRole { Admin, User, Moderator }

public enum ItemStatus { Active, Completed, Archived }

public enum OAuthProvider { Google, Facebook, Twitter }

public enum TokenType { Access, Refresh }
```

- [ ] **Step 2: Create `server/Models/User.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Server.Models;

public sealed class User
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";

    [JsonIgnore] // never sent to the client
    public string? PasswordHash { get; set; }

    public string Name { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 3: Create `server/Models/Item.cs`**

```csharp
namespace Server.Models;

public sealed class Item
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public ItemStatus Status { get; set; } = ItemStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 4: Create `server/Models/Session.cs`**

```csharp
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
```

- [ ] **Step 5: Create `server/Models/OAuthAccount.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Server.Models;

public sealed class OAuthAccount
{
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public OAuthProvider Provider { get; set; }
    public string ProviderAccountId { get; set; } = "";

    [JsonIgnore] // never sent to the client
    public string? AccessToken { get; set; }

    [JsonIgnore] // never sent to the client
    public string? RefreshToken { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 6: Create `server/Models/Requests.cs`**

```csharp
namespace Server.Models;

public record RegisterRequest(string? Email, string? Password, string? Name);

public record LoginRequest(string? Email, string? Password);

public record RefreshTokenRequest(string? RefreshToken);

/// <summary>Body for create/update item. Fields are nullable so "not provided" is distinguishable.</summary>
public record ItemRequest(string? Title, string? Description, ItemStatus? Status);
```

- [ ] **Step 7: Create `server/Models/Responses.cs`**

```csharp
using System.Text.Json.Serialization;

namespace Server.Models;

/// <summary>Standard API envelope: { success, data?, error?, message? }.</summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data) => new() { Success = true, Data = data };

    public static ApiResponse<object> Error(string message) => new() { Success = false, Error = message };
}

public record AuthResponse(User User, string AccessToken, string RefreshToken, int ExpiresIn);

public record RefreshTokenResponse(string AccessToken, int ExpiresIn);
```

- [ ] **Step 8: Build to verify it compiles**

Run: `cd server && dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 9: Commit**

```bash
git add server/Models
git commit -m "feat(server): add domain models, requests, response envelope"
```

---

## Task 4: Auth services (password + JWT)

**Files:**
- Create: `server/Auth/PasswordService.cs`
- Create: `server/Auth/JwtService.cs`

Reference Go originals: `utils/password.go`, `utils/jwt.go`.

- [ ] **Step 1: Create `server/Auth/PasswordService.cs`**

```csharp
namespace Server.Auth;

public static class PasswordService
{
    private const int WorkFactor = 12;

    public static string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public static bool Verify(string hash, string password)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // Malformed hash — treat as a failed verification rather than a 500.
            return false;
        }
    }
}
```

- [ ] **Step 2: Create `server/Auth/JwtService.cs`**

```csharp
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

    public string GenerateAccessToken(User user) => Generate(user, TokenType.Access, AccessTokenExpiry);

    public string GenerateRefreshToken(User user) => Generate(user, TokenType.Refresh, RefreshTokenExpiry);

    private string Generate(User user, TokenType type, TimeSpan expiry)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id,
                ["email"] = user.Email,
                ["role"] = user.Role.ToString().ToLowerInvariant(),
                ["type"] = type.ToString().ToLowerInvariant(),
            },
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
        return new JwtClaims(
            jwt.GetClaim("sub").Value,
            jwt.GetClaim("email").Value,
            jwt.GetClaim("role").Value,
            jwt.GetClaim("type").Value);
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

public record JwtClaims(string UserId, string Email, string Role, string Type);
```

- [ ] **Step 3: Build to verify it compiles**

Run: `cd server && dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add server/Auth
git commit -m "feat(server): add password hashing and JWT service"
```

---

## Task 5: Data layer (Database + repositories)

**Files:**
- Create: `server/Data/Database.cs`
- Create: `server/Data/UserRepository.cs`
- Create: `server/Data/SessionRepository.cs`
- Create: `server/Data/ItemRepository.cs`
- Create: `server/Data/OAuthRepository.cs`

Reference Go originals: `database/db.go`, `database/queries.go`.

Notes:
- `DATABASE_URL` is a URL (`postgresql://user:pass@host:port/db`). Npgsql wants a key/value connection string, so `Database` converts it.
- Enum params are written as lowercase strings (`.ToString().ToLowerInvariant()`) to satisfy the schema's `CHECK` constraints. Reading back, Dapper parses the text column into the enum case-insensitively.
- `RETURNING` rows are read via Dapper's dynamic API; columns come back with their exact (snake_case) names.

- [ ] **Step 1: Create `server/Data/Database.cs`**

```csharp
using Npgsql;

namespace Server.Data;

/// <summary>Owns the Npgsql connection pool. Built from a postgres URL (DATABASE_URL).</summary>
public sealed class Database : IAsyncDisposable
{
    public NpgsqlDataSource DataSource { get; }

    private Database(NpgsqlDataSource dataSource) => DataSource = dataSource;

    public static Database Connect(string databaseUrl)
    {
        var connectionString = ToConnectionString(databaseUrl);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        return new Database(builder.Build());
    }

    /// <summary>Lightweight liveness check used by /health.</summary>
    public async Task<bool> HealthAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var cmd = DataSource.CreateCommand("SELECT 1");
            await cmd.ExecuteScalarAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public ValueTask DisposeAsync() => DataSource.DisposeAsync();

    // Converts postgresql://user:pass@host:port/db?sslmode=... into an Npgsql
    // key/value connection string. Pass-through if it already looks like one.
    private static string ToConnectionString(string url)
    {
        if (!url.Contains("://"))
            return url;

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            Database = uri.AbsolutePath.TrimStart('/'),
            MaxPoolSize = 25,
            MinPoolSize = 5,
            ConnectionIdleLifetime = 1800, // 30 min
            ConnectionLifetime = 3600,     // 1 hour
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]);
            var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                csb.SslMode = Enum.Parse<SslMode>(val, ignoreCase: true);
        }

        return csb.ConnectionString;
    }
}
```

- [ ] **Step 2: Create `server/Data/UserRepository.cs`**

```csharp
using Dapper;
using Server.Models;

namespace Server.Data;

public sealed class UserRepository(Database db)
{
    public async Task CreateAsync(User user)
    {
        const string sql = """
            INSERT INTO users (id, email, password_hash, name, avatar_url, role, email_verified)
            VALUES (@Id, @Email, @PasswordHash, @Name, @AvatarUrl, @Role, @EmailVerified)
            RETURNING created_at, updated_at
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        var row = await conn.QuerySingleAsync(sql, new
        {
            user.Id,
            user.Email,
            user.PasswordHash,
            user.Name,
            user.AvatarUrl,
            Role = user.Role.ToString().ToLowerInvariant(),
            user.EmailVerified,
        });
        user.CreatedAt = (DateTime)row.created_at;
        user.UpdatedAt = (DateTime)row.updated_at;
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        const string sql = """
            SELECT id, email, password_hash, name, avatar_url, role, email_verified, created_at, updated_at
            FROM users WHERE id = @id
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { id });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = """
            SELECT id, email, password_hash, name, avatar_url, role, email_verified, created_at, updated_at
            FROM users WHERE email = @email
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<User>(sql, new { email });
    }
}
```

- [ ] **Step 3: Create `server/Data/SessionRepository.cs`**

```csharp
using Dapper;
using Server.Models;

namespace Server.Data;

public sealed class SessionRepository(Database db)
{
    public async Task CreateAsync(Session session)
    {
        const string sql = """
            INSERT INTO sessions (id, user_id, user_agent, ip_address, expires_at)
            VALUES (@Id, @UserId, @UserAgent, @IpAddress, @ExpiresAt)
            RETURNING created_at
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        var row = await conn.QuerySingleAsync(sql, new
        {
            session.Id,
            session.UserId,
            session.UserAgent,
            session.IpAddress,
            session.ExpiresAt,
        });
        session.CreatedAt = (DateTime)row.created_at;
    }

    public async Task<Session?> GetByIdAsync(string id)
    {
        const string sql = """
            SELECT id, user_id, user_agent, ip_address, expires_at, created_at
            FROM sessions WHERE id = @id
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Session>(sql, new { id });
    }

    public async Task<List<Session>> GetUserSessionsAsync(string userId)
    {
        const string sql = """
            SELECT id, user_id, user_agent, ip_address, expires_at, created_at
            FROM sessions WHERE user_id = @userId AND expires_at > NOW()
            ORDER BY created_at DESC
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        var rows = await conn.QueryAsync<Session>(sql, new { userId });
        return rows.AsList();
    }

    public async Task<bool> DeleteAsync(string id)
    {
        const string sql = "DELETE FROM sessions WHERE id = @id";
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await conn.ExecuteAsync(sql, new { id }) > 0;
    }
}
```

- [ ] **Step 4: Create `server/Data/ItemRepository.cs`**

```csharp
using Dapper;
using Server.Models;

namespace Server.Data;

public sealed class ItemRepository(Database db)
{
    public async Task CreateAsync(Item item)
    {
        const string sql = """
            INSERT INTO items (id, user_id, title, description, status)
            VALUES (@Id, @UserId, @Title, @Description, @Status)
            RETURNING created_at, updated_at
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        var row = await conn.QuerySingleAsync(sql, new
        {
            item.Id,
            item.UserId,
            item.Title,
            item.Description,
            Status = item.Status.ToString().ToLowerInvariant(),
        });
        item.CreatedAt = (DateTime)row.created_at;
        item.UpdatedAt = (DateTime)row.updated_at;
    }

    public async Task<Item?> GetByIdAsync(string id)
    {
        const string sql = """
            SELECT id, user_id, title, COALESCE(description, '') AS description,
                   status, created_at, updated_at
            FROM items WHERE id = @id
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<Item>(sql, new { id });
    }

    public async Task<List<Item>> GetUserItemsAsync(string userId)
    {
        const string sql = """
            SELECT id, user_id, title, COALESCE(description, '') AS description,
                   status, created_at, updated_at
            FROM items WHERE user_id = @userId
            ORDER BY created_at DESC
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        var rows = await conn.QueryAsync<Item>(sql, new { userId });
        return rows.AsList();
    }

    public async Task UpdateAsync(Item item)
    {
        const string sql = """
            UPDATE items
            SET title = @Title, description = @Description, status = @Status,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @Id
            RETURNING updated_at
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        var row = await conn.QuerySingleAsync(sql, new
        {
            item.Id,
            item.Title,
            item.Description,
            Status = item.Status.ToString().ToLowerInvariant(),
        });
        item.UpdatedAt = (DateTime)row.updated_at;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        const string sql = "DELETE FROM items WHERE id = @id";
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await conn.ExecuteAsync(sql, new { id }) > 0;
    }
}
```

- [ ] **Step 5: Create `server/Data/OAuthRepository.cs`**

```csharp
using Dapper;
using Server.Models;

namespace Server.Data;

public sealed class OAuthRepository(Database db)
{
    public async Task CreateAsync(OAuthAccount account)
    {
        const string sql = """
            INSERT INTO oauth_accounts
                (id, user_id, provider, provider_account_id, access_token, refresh_token, expires_at)
            VALUES (@Id, @UserId, @Provider, @ProviderAccountId, @AccessToken, @RefreshToken, @ExpiresAt)
            RETURNING created_at, updated_at
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        var row = await conn.QuerySingleAsync(sql, new
        {
            account.Id,
            account.UserId,
            Provider = account.Provider.ToString().ToLowerInvariant(),
            account.ProviderAccountId,
            account.AccessToken,
            account.RefreshToken,
            account.ExpiresAt,
        });
        account.CreatedAt = (DateTime)row.created_at;
        account.UpdatedAt = (DateTime)row.updated_at;
    }

    public async Task<OAuthAccount?> GetAsync(OAuthProvider provider, string providerAccountId)
    {
        const string sql = """
            SELECT id, user_id, provider, provider_account_id,
                   access_token, refresh_token, expires_at, created_at, updated_at
            FROM oauth_accounts
            WHERE provider = @provider AND provider_account_id = @providerAccountId
            """;
        await using var conn = await db.DataSource.OpenConnectionAsync();
        return await conn.QueryFirstOrDefaultAsync<OAuthAccount>(sql, new
        {
            provider = provider.ToString().ToLowerInvariant(),
            providerAccountId,
        });
    }
}
```

- [ ] **Step 6: Build to verify it compiles**

Run: `cd server && dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add server/Data
git commit -m "feat(server): add Npgsql database wrapper and Dapper repositories"
```

---

## Task 6: Endpoints (root, auth, items)

**Files:**
- Create: `server/Endpoints/RootEndpoints.cs`
- Create: `server/Endpoints/AuthEndpoints.cs`
- Create: `server/Endpoints/ItemEndpoints.cs`

Reference Go originals: `routes/routes.go`, `routes/auth.go`, `routes/items.go`, `handlers/auth.go`, `handlers/items.go`, `middleware/auth.go`.

Notes:
- Protected endpoints use `.RequireAuthorization("access")` — the policy (defined in Task 7) requires an authenticated user whose `type` claim equals `access`.
- The authenticated user id comes from `ctx.User.FindFirstValue("sub")`; Task 7 sets `MapInboundClaims = false` so claim names stay raw.
- These are extension methods; they are not invoked until Program.cs is wired in Task 7, so this task's gate is `dotnet build` only.

- [ ] **Step 1: Create `server/Endpoints/RootEndpoints.cs`**

```csharp
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

        app.MapGet("/health", async () =>
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
```

- [ ] **Step 2: Create `server/Endpoints/AuthEndpoints.cs`**

```csharp
using System.Security.Claims;
using Server.Auth;
using Server.Common;
using Server.Data;
using Server.Models;

namespace Server.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);

        var secured = group.MapGroup("").RequireAuthorization("access");
        secured.MapPost("/logout", Logout);
        secured.MapGet("/me", GetCurrentUser);
        secured.MapGet("/sessions", GetSessions);
    }

    private static async Task<IResult> Register(
        RegisterRequest req,
        HttpContext ctx,
        UserRepository users,
        SessionRepository sessions,
        JwtService jwt)
    {
        var email = Validation.NormalizeEmail(req.Email ?? "");
        if (!Validation.IsValidEmail(email))
            return Results.BadRequest(ApiResponse.Error("Invalid email address"));
        if (!Validation.IsValidPassword(req.Password ?? ""))
            return Results.BadRequest(ApiResponse.Error("Password must be at least 8 characters"));
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(ApiResponse.Error("Name is required"));

        if (await users.GetByEmailAsync(email) is not null)
            return Results.Json(ApiResponse.Error("Email already registered"),
                statusCode: StatusCodes.Status409Conflict);

        var user = new User
        {
            Id = TypeId.NewUserId(),
            Email = email,
            PasswordHash = PasswordService.Hash(req.Password!),
            Name = req.Name!,
            Role = UserRole.User,
            EmailVerified = false,
        };
        await users.CreateAsync(user);

        var session = new Session
        {
            Id = TypeId.NewSessionId(),
            UserId = user.Id,
            UserAgent = ctx.Request.Headers.UserAgent.FirstOrDefault(),
            IpAddress = ClientIp.Get(ctx),
            ExpiresAt = DateTime.UtcNow.Add(JwtService.RefreshTokenExpiry),
        };
        await sessions.CreateAsync(session);

        var response = new AuthResponse(
            user,
            jwt.GenerateAccessToken(user),
            jwt.GenerateRefreshToken(user),
            (int)JwtService.AccessTokenExpiry.TotalSeconds);

        return Results.Json(ApiResponse.Ok(response), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> Login(
        LoginRequest req,
        HttpContext ctx,
        UserRepository users,
        SessionRepository sessions,
        JwtService jwt)
    {
        var email = Validation.NormalizeEmail(req.Email ?? "");
        var user = await users.GetByEmailAsync(email);

        if (user is null ||
            user.PasswordHash is null ||
            !PasswordService.Verify(user.PasswordHash, req.Password ?? ""))
        {
            return Results.Json(ApiResponse.Error("Invalid email or password"),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var session = new Session
        {
            Id = TypeId.NewSessionId(),
            UserId = user.Id,
            UserAgent = ctx.Request.Headers.UserAgent.FirstOrDefault(),
            IpAddress = ClientIp.Get(ctx),
            ExpiresAt = DateTime.UtcNow.Add(JwtService.RefreshTokenExpiry),
        };
        await sessions.CreateAsync(session);

        var response = new AuthResponse(
            user,
            jwt.GenerateAccessToken(user),
            jwt.GenerateRefreshToken(user),
            (int)JwtService.AccessTokenExpiry.TotalSeconds);

        return Results.Ok(ApiResponse.Ok(response));
    }

    private static async Task<IResult> Refresh(
        RefreshTokenRequest req,
        UserRepository users,
        JwtService jwt)
    {
        if (string.IsNullOrEmpty(req.RefreshToken))
            return Results.BadRequest(ApiResponse.Error("Invalid request body"));

        var claims = await jwt.ValidateAsync(req.RefreshToken);
        if (claims is null)
            return Results.Json(ApiResponse.Error("Invalid or expired refresh token"),
                statusCode: StatusCodes.Status401Unauthorized);

        if (claims.Type != "refresh")
            return Results.Json(ApiResponse.Error("Invalid token type"),
                statusCode: StatusCodes.Status401Unauthorized);

        var user = await users.GetByIdAsync(claims.UserId);
        if (user is null)
            return Results.Json(ApiResponse.Error("User not found"),
                statusCode: StatusCodes.Status401Unauthorized);

        var response = new RefreshTokenResponse(
            jwt.GenerateAccessToken(user),
            (int)JwtService.AccessTokenExpiry.TotalSeconds);

        return Results.Ok(ApiResponse.Ok(response));
    }

    private static IResult Logout(HttpContext ctx)
    {
        // The session table is the only server-side state; access tokens are short-lived
        // and not blacklisted (same behaviour as the previous Go server).
        return Results.Ok(ApiResponse.Ok(new { message = "Logged out successfully" }));
    }

    private static async Task<IResult> GetCurrentUser(HttpContext ctx, UserRepository users)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var user = await users.GetByIdAsync(userId);
        if (user is null)
            return Results.Json(ApiResponse.Error("User not found"),
                statusCode: StatusCodes.Status404NotFound);

        return Results.Ok(ApiResponse.Ok(user));
    }

    private static async Task<IResult> GetSessions(HttpContext ctx, SessionRepository sessions)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var list = await sessions.GetUserSessionsAsync(userId);
        return Results.Ok(ApiResponse.Ok(list));
    }
}
```

- [ ] **Step 3: Create `server/Endpoints/ItemEndpoints.cs`**

```csharp
using System.Security.Claims;
using Server.Common;
using Server.Data;
using Server.Models;

namespace Server.Endpoints;

public static class ItemEndpoints
{
    public static void MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/items").RequireAuthorization("access");

        group.MapGet("", ListItems);
        group.MapGet("/{id}", GetItem);
        group.MapPost("", CreateItem);
        group.MapPut("/{id}", UpdateItem);
        group.MapDelete("/{id}", DeleteItem);
    }

    private static async Task<IResult> ListItems(HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var list = await items.GetUserItemsAsync(userId);
        return Results.Ok(ApiResponse.Ok(list));
    }

    private static async Task<IResult> GetItem(string id, HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var item = await items.GetByIdAsync(id);
        if (item is null)
            return Results.Json(ApiResponse.Error("Item not found"),
                statusCode: StatusCodes.Status404NotFound);
        if (item.UserId != userId)
            return Results.Json(ApiResponse.Error("Access denied"),
                statusCode: StatusCodes.Status403Forbidden);

        return Results.Ok(ApiResponse.Ok(item));
    }

    private static async Task<IResult> CreateItem(ItemRequest req, HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        if (string.IsNullOrWhiteSpace(req.Title))
            return Results.BadRequest(ApiResponse.Error("Title is required"));

        var item = new Item
        {
            Id = TypeId.NewItemId(),
            UserId = userId,
            Title = req.Title!,
            Description = req.Description ?? "",
            Status = req.Status ?? ItemStatus.Active,
        };
        await items.CreateAsync(item);

        return Results.Json(ApiResponse.Ok(item), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> UpdateItem(
        string id, ItemRequest req, HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var item = await items.GetByIdAsync(id);
        if (item is null)
            return Results.Json(ApiResponse.Error("Item not found"),
                statusCode: StatusCodes.Status404NotFound);
        if (item.UserId != userId)
            return Results.Json(ApiResponse.Error("Access denied"),
                statusCode: StatusCodes.Status403Forbidden);

        if (!string.IsNullOrEmpty(req.Title))
            item.Title = req.Title;
        if (!string.IsNullOrEmpty(req.Description))
            item.Description = req.Description;
        if (req.Status is { } status)
            item.Status = status;

        await items.UpdateAsync(item);
        return Results.Ok(ApiResponse.Ok(item));
    }

    private static async Task<IResult> DeleteItem(string id, HttpContext ctx, ItemRepository items)
    {
        var userId = ctx.User.FindFirstValue("sub")!;
        var item = await items.GetByIdAsync(id);
        if (item is null)
            return Results.Json(ApiResponse.Error("Item not found"),
                statusCode: StatusCodes.Status404NotFound);
        if (item.UserId != userId)
            return Results.Json(ApiResponse.Error("Access denied"),
                statusCode: StatusCodes.Status403Forbidden);

        await items.DeleteAsync(id);
        return Results.Ok(ApiResponse.Ok(new { message = "Item deleted successfully" }));
    }
}
```

- [ ] **Step 4: Build to verify it compiles**

Run: `cd server && dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add server/Endpoints
git commit -m "feat(server): add root, auth, and item endpoints"
```

---

## Task 7: Wire everything in Program.cs

**Files:**
- Modify (full rewrite): `server/Program.cs`

Reference Go original: `main.go`, `routes/routes.go`.

- [ ] **Step 1: Replace `server/Program.cs` with the full wiring**

```csharp
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Server.Auth;
using Server.Configuration;
using Server.Data;
using Server.Endpoints;
using Server.Models;

// Load .env when present (dev only) — mirrors the Go server's godotenv behaviour.
if (File.Exists(".env"))
    Env.Load();

// Map snake_case columns (created_at) to PascalCase properties (CreatedAt).
DefaultTypeMap.MatchNamesWithUnderscores = true;

var config = AppConfig.Load();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{config.Port}");

// --- Services -------------------------------------------------------------
builder.Services.AddSingleton(config);

var jwtService = new JwtService(config.JwtSecret);
builder.Services.AddSingleton(jwtService);

Database? database = config.DatabaseUrl.Length > 0
    ? Database.Connect(config.DatabaseUrl)
    : null;

if (database is not null)
{
    builder.Services.AddSingleton(database);
    builder.Services.AddScoped<UserRepository>();
    builder.Services.AddScoped<SessionRepository>();
    builder.Services.AddScoped<ItemRepository>();
    builder.Services.AddScoped<OAuthRepository>();
}
else
{
    Console.WriteLine("⚠️  No DATABASE_URL provided, running without database");
}

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep raw claim names: sub, role, type, email
        options.TokenValidationParameters = jwtService.ValidationParameters;
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                var message = ctx.Request.Headers.ContainsKey("Authorization")
                    ? "Invalid or expired token"
                    : "Missing authorization header";
                await ctx.Response.WriteAsJsonAsync(ApiResponse.Error(message));
            },
            OnForbidden = async ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(ApiResponse.Error("Insufficient permissions"));
            },
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("access", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("type", "access"));

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(config.AllowedOrigins)
    .AllowCredentials()
    .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
    .WithHeaders("Content-Type", "Authorization")
    .WithExposedHeaders("Content-Length", "X-Request-Id")));

builder.Services.AddOpenApi();

// --- Pipeline -------------------------------------------------------------
var app = builder.Build();

// Global error handler — emits the { success, error } envelope.
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(ApiResponse.Error("Internal server error"));
}));

// Request logging (development only).
if (config.IsDevelopment)
{
    app.Use(async (ctx, next) =>
    {
        var sw = Stopwatch.StartNew();
        await next();
        sw.Stop();
        Console.WriteLine(
            $"[{DateTime.Now:HH:mm:ss}] {ctx.Response.StatusCode} - " +
            $"{ctx.Request.Method} {ctx.Request.Path} ({sw.ElapsedMilliseconds}ms)");
    });
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapRootEndpoints(database);

if (database is not null)
{
    app.MapAuthEndpoints();
    app.MapItemEndpoints();
}
else
{
    // No database: items list returns empty data, auth routes are not mounted.
    app.MapGet("/api/items", () => Results.Ok(ApiResponse.Ok(Array.Empty<object>())));
}

app.MapFallback((HttpContext ctx) =>
    Results.Json(
        ApiResponse.Error($"Route not found: {ctx.Request.Method} {ctx.Request.Path}"),
        statusCode: StatusCodes.Status404NotFound));

Console.WriteLine($"🚀 Server starting on port {config.Port} (env: {config.Environment})");
app.Run();

public partial class Program;
```

- [ ] **Step 2: Build**

Run: `cd server && dotnet build`
Expected: `Build succeeded`.

- [ ] **Step 3: Run without a database and verify the no-DB path**

Run:
```bash
cd server
( unset DATABASE_URL; ENVIRONMENT=development dotnet run ) &
sleep 6
curl -s localhost:3000/health ; echo
curl -s localhost:3000/api/items ; echo
curl -s localhost:3000/api/auth/me -w ' [%{http_code}]' ; echo
kill %1
```
Expected: `/health` shows `"database":"not_configured"`; `/api/items` returns `{"success":true,"data":[]}`; `/api/auth/me` returns `{"success":false,"error":"Missing authorization header"} [401]`.

- [ ] **Step 4: Run with the database and verify the full auth flow**

First ensure the dev DB exists and is migrated (from repo root): `bun run db:fresh`.

Run:
```bash
cd server
dotnet run &
sleep 6
# register
curl -s -X POST localhost:3000/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"plan-test@example.com","password":"password123","name":"Plan Test"}' ; echo
# login
curl -s -X POST localhost:3000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"plan-test@example.com","password":"password123"}' ; echo
kill %1
```
Expected: register returns `{"success":true,"data":{"user":{...},"accessToken":"...","refreshToken":"...","expiresIn":900}}` with HTTP 201; login returns the same shape with HTTP 200. The `user` object has camelCase keys, `role":"user"`, and **no** `passwordHash`.

- [ ] **Step 5: Commit**

```bash
git add server/Program.cs
git commit -m "feat(server): wire DI, auth, CORS, and endpoint mapping in Program.cs"
```

---

## Task 8: Rewrite server/Dockerfile

**Files:**
- Modify (full rewrite): `server/Dockerfile`
- Modify: `.dockerignore` (repo root)

- [ ] **Step 1: Replace `server/Dockerfile`**

```dockerfile
# .NET Server Dockerfile - multi-stage build for a minimal production image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first and restore — keeps the restore layer cached.
COPY server/Server.csproj ./server/
RUN dotnet restore ./server/Server.csproj

# Copy the rest of the server source and publish.
COPY server/ ./server/
RUN dotnet publish ./server/Server.csproj -c Release -o /app/publish /p:UseAppHost=false

# Production image - minimal Alpine ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Runtime dependencies (wget is used by the healthcheck)
RUN apk --no-cache add ca-certificates tzdata wget

# Create non-root user
RUN addgroup -g 1001 -S appgroup && \
    adduser -u 1001 -S appuser -G appgroup

WORKDIR /app
COPY --from=build --chown=appuser:appgroup /app/publish ./

USER appuser

ENV ASPNETCORE_URLS=http://+:3000 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    PORT=3000

EXPOSE 3000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:3000/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "server.dll"]
```

- [ ] **Step 2: Add build artifact directories to `.dockerignore`**

In `.dockerignore`, find the `# Build outputs` section:
```
# Build outputs
dist
build
.next
out
*.local
```
Replace it with:
```
# Build outputs
dist
build
.next
out
*.local
**/bin
**/obj
```

- [ ] **Step 3: Build the image to verify**

Run (from repo root): `docker build -f server/Dockerfile -t dotnet-server-test .`
Expected: `naming to docker.io/library/dotnet-server-test` / build succeeds.

- [ ] **Step 4: Smoke-test the image (no DB)**

Run:
```bash
docker run --rm -d --name dotnet-server-smoke -p 3001:3000 dotnet-server-test
sleep 6
curl -s localhost:3001/health ; echo
docker rm -f dotnet-server-smoke
```
Expected: `/health` JSON with `"database":"not_configured"`.

- [ ] **Step 5: Commit**

```bash
git add server/Dockerfile .dockerignore
git commit -m "build(server): replace Go Dockerfile with .NET multi-stage build"
```

---

## Task 9: Update the Makefile

**Files:**
- Modify: `Makefile`

- [ ] **Step 1: Update `build-server` (lines ~69-72)**

Replace:
```makefile
build-server: ## Build Go server binary (server/bin/server)
	@echo "$(BLUE)Building Go server...$(NC)"
	cd server && go build -o bin/server .
	@echo "$(GREEN)✓ Server binary built: server/bin/server$(NC)"
```
With:
```makefile
build-server: ## Build .NET server (server/bin)
	@echo "$(BLUE)Building .NET server...$(NC)"
	cd server && dotnet publish -c Release -o bin
	@echo "$(GREEN)✓ Server built: server/bin$(NC)"
```

- [ ] **Step 2: Update the server half of `version-up` (lines ~146-154)**

Replace:
```makefile
	@echo "$(YELLOW)Updating server version...$(NC)"
	@CURRENT_VERSION=$$(grep 'Version = ' server/version.go | sed 's/.*"\(.*\)".*/\1/'); \
	MAJOR=$$(echo $$CURRENT_VERSION | cut -d. -f1); \
	MINOR=$$(echo $$CURRENT_VERSION | cut -d. -f2); \
	PATCH=$$(echo $$CURRENT_VERSION | cut -d. -f3); \
	NEW_PATCH=$$(($$PATCH + 1)); \
	NEW_VERSION="$$MAJOR.$$MINOR.$$NEW_PATCH"; \
	sed -i.bak "s/Version = \".*\"/Version = \"$$NEW_VERSION\"/" server/version.go && rm server/version.go.bak; \
	echo "$(GREEN)✓ Server version: $$CURRENT_VERSION → $$NEW_VERSION$(NC)"
```
With:
```makefile
	@echo "$(YELLOW)Updating server version...$(NC)"
	@CURRENT_VERSION=$$(grep 'Version = ' server/Version.cs | sed 's/.*"\(.*\)".*/\1/'); \
	MAJOR=$$(echo $$CURRENT_VERSION | cut -d. -f1); \
	MINOR=$$(echo $$CURRENT_VERSION | cut -d. -f2); \
	PATCH=$$(echo $$CURRENT_VERSION | cut -d. -f3); \
	NEW_PATCH=$$(($$PATCH + 1)); \
	NEW_VERSION="$$MAJOR.$$MINOR.$$NEW_PATCH"; \
	sed -i.bak "s/Version = \".*\"/Version = \"$$NEW_VERSION\"/" server/Version.cs && rm server/Version.cs.bak; \
	echo "$(GREEN)✓ Server version: $$CURRENT_VERSION → $$NEW_VERSION$(NC)"
```

- [ ] **Step 3: Update `fmt-server` (lines ~472-473)**

Replace:
```makefile
fmt-server: ## Format Go code
	cd server && gofmt -w .
```
With:
```makefile
fmt-server: ## Format .NET code
	cd server && dotnet format
```

- [ ] **Step 4: Update `lint-server` (lines ~480-481)**

Replace:
```makefile
lint-server: ## Lint Go code
	cd server && go vet ./...
```
With:
```makefile
lint-server: ## Lint .NET code (format check)
	cd server && dotnet format --verify-no-changes
```

- [ ] **Step 5: Update `check-server` (lines ~488-490)**

Replace:
```makefile
check-server: ## Check Go code (format + lint)
	cd server && gofmt -w .
	cd server && go vet ./...
```
With:
```makefile
check-server: ## Check .NET code (format + build)
	cd server && dotnet format
	cd server && dotnet build
```

- [ ] **Step 6: Update `deps-server` (lines ~507-511)**

Replace:
```makefile
deps-server: ## Upgrade Go server dependencies to latest versions
	@echo "$(BLUE)Upgrading Go dependencies...$(NC)"
	@cd server && go get -u ./...
	@cd server && go mod tidy
	@echo "$(GREEN)✓ Go dependencies upgraded$(NC)"
```
With:
```makefile
deps-server: ## List outdated .NET server dependencies
	@echo "$(BLUE)Checking .NET dependencies...$(NC)"
	@cd server && dotnet list package --outdated
	@echo "$(GREEN)✓ Dependency check complete (update versions in Server.csproj)$(NC)"
```

- [ ] **Step 7: Verify the Makefile targets work**

Run:
```bash
make build-server
make check-server
```
Expected: both succeed (`Build succeeded`, formatting clean).

- [ ] **Step 8: Commit**

```bash
git add Makefile
git commit -m "build: update Makefile server targets for .NET toolchain"
```

---

## Task 10: Update k8s, docker-compose, and package.json

**Files:**
- Modify: `k8s/configmap.yaml`
- Modify: `k8s/server-deployment.yaml`
- Modify: `docker-compose.yml`
- Modify: `package.json` (repo root)

- [ ] **Step 1: Check nothing else depends on the `NODE_ENV` ConfigMap key**

Run: `grep -rn "NODE_ENV" k8s/`
Expected: matches only in `k8s/configmap.yaml` and `k8s/server-deployment.yaml`. If `k8s/client-deployment.yaml` also references it, stop and reassess — the rename below would need to cover it too.

- [ ] **Step 2: Rename the ConfigMap key in `k8s/configmap.yaml`**

Replace:
```yaml
data:
  # Server configuration
  NODE_ENV: "production"
  SERVER_PORT: "3000"
```
With:
```yaml
data:
  # Server configuration
  ENVIRONMENT: "production"
  SERVER_PORT: "3000"
```

- [ ] **Step 3: Update the env var in `k8s/server-deployment.yaml`**

Replace:
```yaml
        env:
        # ConfigMap values
        - name: NODE_ENV
          valueFrom:
            configMapKeyRef:
              name: monorepo-config
              key: NODE_ENV
```
With:
```yaml
        env:
        # ConfigMap values
        - name: ENVIRONMENT
          valueFrom:
            configMapKeyRef:
              name: monorepo-config
              key: ENVIRONMENT
```

- [ ] **Step 4: Bump the server memory request in `k8s/server-deployment.yaml`**

Replace:
```yaml
        resources:
          requests:
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
```
With:
```yaml
        resources:
          requests:
            memory: "256Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
```

- [ ] **Step 5: Update the comment in `docker-compose.yml`**

Replace:
```yaml
  # Go/Fiber Server
  server:
```
With:
```yaml
  # .NET Server
  server:
```

- [ ] **Step 6: Update `package.json` server scripts and description**

In `package.json`, replace:
```json
  "description": "Full-stack monorepo with Go Fiber, React, PostgreSQL, and Docker",
```
With:
```json
  "description": "Full-stack monorepo with .NET, React, PostgreSQL, and Docker",
```

Replace:
```json
    "dev:server": "cd server && go run .",
```
With:
```json
    "dev:server": "cd server && dotnet watch run",
```

Replace:
```json
    "build:server": "cd server && go build -o bin/server .",
```
With:
```json
    "build:server": "cd server && dotnet publish -c Release -o bin",
```

Replace:
```json
    "clean": "rm -rf dist coverage server/bin client/dist",
```
With:
```json
    "clean": "rm -rf dist coverage server/bin server/obj client/dist",
```

- [ ] **Step 7: Validate the YAML and JSON**

Run:
```bash
kubectl apply --dry-run=client -f k8s/configmap.yaml -f k8s/server-deployment.yaml
node -e "JSON.parse(require('fs').readFileSync('package.json','utf8')); console.log('package.json OK')"
```
Expected: kubectl reports the objects as valid (`configured`/`created` dry-run output); `package.json OK`.

- [ ] **Step 8: Commit**

```bash
git add k8s/configmap.yaml k8s/server-deployment.yaml docker-compose.yml package.json
git commit -m "chore: update k8s, compose, and npm scripts for .NET server"
```

---

## Task 11: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Clean build from scratch**

Run:
```bash
cd server && rm -rf bin obj && dotnet build
```
Expected: `Build succeeded`, 0 warnings, 0 errors.

- [ ] **Step 2: Formatting check**

Run: `cd server && dotnet format --verify-no-changes`
Expected: exits 0 with no output (no formatting changes needed). If it reports changes, run `dotnet format` and re-commit.

- [ ] **Step 3: Full contract walk-through against a live server + DB**

From repo root: `bun run db:fresh` to get a clean DB. Then:
```bash
cd server && dotnet run &
sleep 6
BASE=localhost:3000

echo "--- root"
curl -s $BASE/ | head -c 200 ; echo
echo "--- health"
curl -s $BASE/health ; echo
echo "--- register"
REG=$(curl -s -X POST $BASE/api/auth/register -H 'Content-Type: application/json' \
  -d '{"email":"final@example.com","password":"password123","name":"Final Check"}')
echo "$REG"
ACCESS=$(echo "$REG" | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)
REFRESH=$(echo "$REG" | grep -o '"refreshToken":"[^"]*"' | cut -d'"' -f4)
echo "--- me (authorized)"
curl -s $BASE/api/auth/me -H "Authorization: Bearer $ACCESS" ; echo
echo "--- me (no token, expect 401 envelope)"
curl -s $BASE/api/auth/me -w ' [%{http_code}]' ; echo
echo "--- refresh"
curl -s -X POST $BASE/api/auth/refresh -H 'Content-Type: application/json' \
  -d "{\"refreshToken\":\"$REFRESH\"}" ; echo
echo "--- create item"
ITEM=$(curl -s -X POST $BASE/api/items -H "Authorization: Bearer $ACCESS" \
  -H 'Content-Type: application/json' -d '{"title":"Plan item"}')
echo "$ITEM"
ITEM_ID=$(echo "$ITEM" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "--- list items"
curl -s $BASE/api/items -H "Authorization: Bearer $ACCESS" ; echo
echo "--- update item"
curl -s -X PUT $BASE/api/items/$ITEM_ID -H "Authorization: Bearer $ACCESS" \
  -H 'Content-Type: application/json' -d '{"status":"completed"}' ; echo
echo "--- delete item"
curl -s -X DELETE $BASE/api/items/$ITEM_ID -H "Authorization: Bearer $ACCESS" ; echo
echo "--- 404"
curl -s $BASE/does-not-exist -w ' [%{http_code}]' ; echo
kill %1
```
Expected, checked against the Go server's behaviour:
- root JSON includes `"name":"Monorepo API"`
- `/health` includes `status`, `timestamp`, `uptime`, `memory`, `"database":"healthy"`
- register → HTTP 201, `{success:true,data:{user,accessToken,refreshToken,expiresIn:900}}`, no `passwordHash`
- `/api/auth/me` with token → `{success:true,data:{...user...}}`; without token → `{success:false,error:"Missing authorization header"} [401]`
- refresh → `{success:true,data:{accessToken,expiresIn:900}}`
- create item → HTTP 201, `{success:true,data:{id:"item_...",status:"active",...}}`
- list items → `{success:true,data:[...]}`
- update item → `data.status` is `"completed"`
- delete item → `{success:true,data:{message:"Item deleted successfully"}}`
- unknown route → `{success:false,error:"Route not found: GET /does-not-exist"} [404]`

- [ ] **Step 4: Run the existing test suite**

Run (from repo root): `bun test`
Expected: `tests/health-check.test.ts` passes (3 tests). It is language-agnostic and unaffected by the migration.

- [ ] **Step 5: Verify the Docker image one more time end to end**

Run (from repo root):
```bash
docker build -f server/Dockerfile -t dotnet-server-test .
docker run --rm -d --name dotnet-server-final -p 3002:3000 dotnet-server-test
sleep 6
docker ps --filter name=dotnet-server-final --format '{{.Status}}'
curl -s localhost:3002/health ; echo
docker rm -f dotnet-server-final
```
Expected: container `Status` shows `healthy` (or `health: starting` then healthy); `/health` returns JSON.

- [ ] **Step 6: Confirm no stale Go references remain**

Run: `grep -rn -iE "go run|gofmt|go vet|go build|fiber|golang" Makefile package.json docker-compose.yml k8s/ server/ --include='*.cs' --include='Makefile' --include='*.json' --include='*.yml' --include='*.yaml' || echo "clean"`
Expected: `clean`, or only harmless matches (e.g. the word "go" inside unrelated text). Any real Go-toolchain reference must be fixed before finishing.

- [ ] **Step 7: Final commit**

```bash
git add -A
git commit -m "test: verify .NET server migration end to end" --allow-empty
```

---

## Notes & Out of Scope

- **Project/image/namespace names** (`bun-hono-react*`, `PROJECT_NAME`, `K8S_NAMESPACE`) are intentionally left unchanged — renaming them is risky and was not requested.
- **README and other `.md` docs** (`README.md`, `DOCKER.md`, `KUBERNETES.md`, `MIGRATION.md`, `QUICK_START.md`, `TEMPLATE.md`, etc.) still describe the Go stack. They were not part of this request — flag them to the user as a follow-up.
- **OAuth endpoints** are not wired (the Go server didn't wire them either); `OAuthRepository` is ported for parity and registered in DI but unused.
- **`server/.env.example`** already matches the new server's variables (`ENVIRONMENT`, `PORT`, `DATABASE_URL`, `JWT_SECRET`, `FRONTEND_URL`) — no change needed.
- **Behavioural deltas from cleanup** (acceptable, non-breaking): a token with `type=refresh` used on a protected route returns HTTP 403 `"Insufficient permissions"` (Go returned 401 `"Invalid token type"`); a missing vs. malformed `Authorization` header both surface through `OnChallenge` with `"Missing authorization header"` / `"Invalid or expired token"` respectively. An OpenAPI document is now served at `/openapi/v1.json`.
