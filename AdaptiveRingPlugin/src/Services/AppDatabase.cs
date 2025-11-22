using System.Text.Json;
using Loupedeck.AdaptiveRingPlugin.Models;
using Microsoft.Data.Sqlite;

namespace Loupedeck.AdaptiveRingPlugin.Services;

public class AppDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private const int CACHE_DAYS = 7;

    public AppDatabase(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var createTablesCmd = _connection.CreateCommand();
        createTablesCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS mcp_cache (
                app_name TEXT PRIMARY KEY,
                registry_source TEXT,
                server_name TEXT,
                server_json TEXT,
                cached_at TEXT
            );

            CREATE TABLE IF NOT EXISTS toolsdk_index (
                package_name TEXT PRIMARY KEY,
                category TEXT,
                validated INTEGER,
                tools_json TEXT,
                updated_at TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_cache_time ON mcp_cache(cached_at);
            CREATE INDEX IF NOT EXISTS idx_toolsdk_category ON toolsdk_index(category);
        ";
        createTablesCmd.ExecuteNonQuery();
    }

    public async Task<MCPServerData?> GetCachedAsync(string appName)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT server_name, registry_source, server_json
            FROM mcp_cache
            WHERE app_name = $appName
            AND datetime(cached_at) > datetime('now', '-' || $cacheDays || ' days')
        ";
        cmd.Parameters.AddWithValue("$appName", appName.ToLowerInvariant());
        cmd.Parameters.AddWithValue("$cacheDays", CACHE_DAYS);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var serverName = reader.GetString(0);
            var registrySource = reader.GetString(1);
            var serverJson = reader.GetString(2);

            if (serverName == "NOT_FOUND")
            {
                return null; // Cached negative result
            }

            var serverData = JsonSerializer.Deserialize<MCPServerData>(serverJson);
            if (serverData != null)
            {
                serverData.RegistrySource = registrySource;
            }
            return serverData;
        }

        return null;
    }

    public async Task SaveResultAsync(string appName, string registrySource, MCPServerData serverData)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO mcp_cache (app_name, registry_source, server_name, server_json, cached_at)
            VALUES ($appName, $registrySource, $serverName, $serverJson, datetime('now'))
        ";
        cmd.Parameters.AddWithValue("$appName", appName.ToLowerInvariant());
        cmd.Parameters.AddWithValue("$registrySource", registrySource);
        cmd.Parameters.AddWithValue("$serverName", serverData.ServerName);
        cmd.Parameters.AddWithValue("$serverJson", JsonSerializer.Serialize(serverData));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkAsNotFoundAsync(string appName)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO mcp_cache (app_name, registry_source, server_name, server_json, cached_at)
            VALUES ($appName, 'NONE', 'NOT_FOUND', '{}', datetime('now'))
        ";
        cmd.Parameters.AddWithValue("$appName", appName.ToLowerInvariant());

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveToolSDKIndexAsync(Dictionary<string, ToolSDKPackage> packages)
    {
        using var transaction = _connection.BeginTransaction();

        foreach (var (packageName, package) in packages)
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO toolsdk_index (package_name, category, validated, tools_json, updated_at)
                VALUES ($packageName, $category, $validated, $toolsJson, datetime('now'))
            ";
            cmd.Parameters.AddWithValue("$packageName", packageName);
            cmd.Parameters.AddWithValue("$category", package.Category ?? "unknown");
            cmd.Parameters.AddWithValue("$validated", package.Validated ? 1 : 0);
            cmd.Parameters.AddWithValue("$toolsJson", JsonSerializer.Serialize(package.Tools));

            await cmd.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    public async Task<List<MCPServerData>> SearchToolSDKIndexAsync(string appName)
    {
        var searchTerm = appName.ToLowerInvariant();
        var results = new List<MCPServerData>();

        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT package_name, category, validated, tools_json
            FROM toolsdk_index
            WHERE LOWER(package_name) LIKE '%' || $searchTerm || '%'
            ORDER BY validated DESC, package_name
            LIMIT 10
        ";
        cmd.Parameters.AddWithValue("$searchTerm", searchTerm);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var packageName = reader.GetString(0);
            var category = reader.GetString(1);
            var validated = reader.GetInt32(2) == 1;
            var toolsJson = reader.GetString(3);

            var tools = JsonSerializer.Deserialize<Dictionary<string, ToolSDKTool>>(toolsJson);

            results.Add(new MCPServerData
            {
                ServerName = packageName,
                PackageName = packageName,
                Category = category,
                Validated = validated,
                RegistrySource = "ToolSDK",
                Tools = tools?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ToolInfo { Name = kvp.Value.Name, Description = kvp.Value.Description }
                )
            });
        }

        return results;
    }

    public async Task<bool> IsToolSDKIndexFreshAsync()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) as count, MAX(updated_at) as last_update
            FROM toolsdk_index
        ";

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var count = reader.GetInt32(0);
            if (count == 0) return false;

            var lastUpdateStr = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (lastUpdateStr == null) return false;

            if (DateTime.TryParse(lastUpdateStr, out var lastUpdate))
            {
                return (DateTime.UtcNow - lastUpdate).TotalDays < CACHE_DAYS;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
