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
