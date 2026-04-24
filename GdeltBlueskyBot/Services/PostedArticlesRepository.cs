using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GdeltBlueskyBot.Services;

public sealed class PostedArticlesRepository(
    IConfiguration configuration,
    ILogger<PostedArticlesRepository> logger)
{
    private readonly string _connectionString = BuildConnectionString(
        configuration["Bot:DatabasePath"] ?? "posted_articles.db");

    private static string BuildConnectionString(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path }.ToString();

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS PostedArticles (
                Url       TEXT NOT NULL PRIMARY KEY,
                PostedAt  TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        logger.LogInformation("Database ready at {Path}", _connectionString);
    }

    public async Task<bool> HasBeenPostedAsync(string url, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM PostedArticles WHERE Url = $url LIMIT 1;";
        cmd.Parameters.AddWithValue("$url", url);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null;
    }

    public async Task MarkAsPostedAsync(string url, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO PostedArticles (Url) VALUES ($url);";
        cmd.Parameters.AddWithValue("$url", url);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
