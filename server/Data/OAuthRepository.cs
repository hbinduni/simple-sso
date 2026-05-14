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
