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
