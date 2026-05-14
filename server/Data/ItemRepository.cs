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
