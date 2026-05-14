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
