using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Server.Auth;
using Server.Configuration;
using Server.Endpoints;
using Server.Models;

// Load .env when present (dev only).
if (File.Exists(".env"))
    Env.Load();

var config = AppConfig.Load();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{config.Port}");

// --- Services -------------------------------------------------------------
builder.Services.AddSingleton(config);

var jwtService = new JwtService(config.JwtSecret);
builder.Services.AddSingleton(jwtService);

// Stateless SSO broker — no datastore. The Microsoft OAuth endpoints mount only when
// Entra is configured; the typed HttpClient is used for the code/token exchange + Graph.
if (config.MicrosoftOAuthEnabled)
    builder.Services.AddHttpClient<MicrosoftOAuthService>();
else
    Console.WriteLine("⚠️  Microsoft Entra ID not configured (AZURE_* unset); SSO endpoints disabled");

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
// A malformed request body surfaces as BadHttpRequestException before the
// handler runs; map that to 400 (matches the previous server's behaviour).
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    var error = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (error is BadHttpRequestException)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(ApiResponse.Error("Invalid request body"));
        return;
    }

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
app.MapRootEndpoints();
app.MapAuthEndpoints();

if (config.MicrosoftOAuthEnabled)
    app.MapOAuthEndpoints();

app.MapFallback((HttpContext ctx) =>
    Results.Json(
        ApiResponse.Error($"Route not found: {ctx.Request.Method} {ctx.Request.Path}"),
        statusCode: StatusCodes.Status404NotFound));

Console.WriteLine($"🚀 Server starting on port {config.Port} (env: {config.Environment})");
app.Run();

public partial class Program;
