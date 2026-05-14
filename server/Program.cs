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
