using System.Security.Claims;
using Server.Auth;
using Server.Models;

namespace Server.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/refresh", Refresh);

        var secured = group.MapGroup("").RequireAuthorization("access");
        secured.MapPost("/logout", Logout);
        secured.MapGet("/me", GetCurrentUser);
    }

    private static async Task<IResult> Refresh(RefreshTokenRequest req, JwtService jwt)
    {
        if (string.IsNullOrEmpty(req.RefreshToken))
            return Results.BadRequest(ApiResponse.Error("Invalid request body"));

        var claims = await jwt.ValidateAsync(req.RefreshToken);
        if (claims is null || claims.Type != "refresh")
            return Results.Json(ApiResponse.Error("Invalid or expired refresh token"),
                statusCode: StatusCodes.Status401Unauthorized);

        // The refresh token is the only record of the identity — rebuild it and re-issue.
        var user = new AuthUser(claims.UserId, claims.Email, claims.Name, claims.Role, claims.Groups);
        var response = new RefreshTokenResponse(
            jwt.GenerateAccessToken(user),
            (int)JwtService.AccessTokenExpiry.TotalSeconds);

        return Results.Ok(ApiResponse.Ok(response));
    }

    private static IResult Logout() =>
        // Stateless: tokens are short-lived and not server-tracked, so logout is client-side
        // (the SPA drops its stored tokens). This endpoint exists for symmetry.
        Results.Ok(ApiResponse.Ok(new { message = "Logged out successfully" }));

    private static IResult GetCurrentUser(HttpContext ctx)
    {
        var profile = new AuthUserProfile(
            Id: ctx.User.FindFirstValue("sub") ?? "",
            Email: ctx.User.FindFirstValue("email") ?? "",
            Name: ctx.User.FindFirstValue("name") ?? "",
            Role: ctx.User.FindFirstValue("role") ?? "user");

        var groups = ctx.User.FindAll("groups").Select(c => c.Value).ToArray();
        return Results.Ok(ApiResponse.Ok(new MeResponse(profile, groups)));
    }
}
