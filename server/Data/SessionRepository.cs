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
