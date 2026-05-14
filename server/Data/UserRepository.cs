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
