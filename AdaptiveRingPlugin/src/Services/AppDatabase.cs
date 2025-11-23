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
                cached_at TEXT,
                connection_type TEXT,
                stdio_command TEXT,
                sse_url TEXT
            );

            CREATE TABLE IF NOT EXISTS toolsdk_index (
                package_name TEXT PRIMARY KEY,
                category TEXT,
                validated INTEGER,
                tools_json TEXT,
                updated_at TEXT
            );

            CREATE TABLE IF NOT EXISTS remembered_apps (
                app_name TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                mcp_server_name TEXT,
                created_at INTEGER NOT NULL,
                last_seen_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS app_actions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                app_name TEXT NOT NULL,
                position INTEGER NOT NULL,
                action_type TEXT NOT NULL,
                action_name TEXT NOT NULL,
                action_data TEXT NOT NULL,
                enabled INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (app_name) REFERENCES remembered_apps(app_name) ON DELETE CASCADE,
                UNIQUE (app_name, position)
            );

            CREATE TABLE IF NOT EXISTS universal_defaults (
                position INTEGER PRIMARY KEY,
                action_type TEXT NOT NULL,
                action_name TEXT NOT NULL,
                action_data TEXT NOT NULL,
                enabled INTEGER NOT NULL DEFAULT 1
            );

            CREATE INDEX IF NOT EXISTS idx_cache_time ON mcp_cache(cached_at);
            CREATE INDEX IF NOT EXISTS idx_toolsdk_category ON toolsdk_index(category);
            CREATE INDEX IF NOT EXISTS idx_app_actions_app ON app_actions(app_name);
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

    // CRUD methods for remembered_apps and app_actions

    public async Task<RememberedApp?> GetRememberedApp(string appName)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT app_name, display_name, mcp_server_name, created_at, last_seen_at
            FROM remembered_apps
            WHERE app_name = $appName
        ";
        cmd.Parameters.AddWithValue("$appName", appName.ToLowerInvariant());

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var appNameValue = reader.GetString(0);
            var displayNameValue = reader.GetString(1);

            return new RememberedApp
            {
                AppName = appNameValue,
                DisplayName = displayNameValue,
                McpServerName = reader.IsDBNull(2) ? null : reader.GetString(2),
                CreatedAt = reader.GetInt64(3),
                LastSeenAt = reader.GetInt64(4)
            };
        }

        return null;
    }

    public async Task SaveRememberedApp(RememberedApp app)
    {
        var cmd = _connection.CreateCommand();

        // Check if app exists
        var existingApp = await GetRememberedApp(app.AppName);

        if (existingApp != null)
        {
            // Update existing app (only update last_seen_at)
            cmd.CommandText = @"
                UPDATE remembered_apps
                SET last_seen_at = $lastSeenAt
                WHERE app_name = $appName
            ";
            cmd.Parameters.AddWithValue("$appName", app.AppName.ToLowerInvariant());
            cmd.Parameters.AddWithValue("$lastSeenAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        else
        {
            // Insert new app
            cmd.CommandText = @"
                INSERT INTO remembered_apps (app_name, display_name, mcp_server_name, created_at, last_seen_at)
                VALUES ($appName, $displayName, $mcpServerName, $createdAt, $lastSeenAt)
            ";
            cmd.Parameters.AddWithValue("$appName", app.AppName.ToLowerInvariant());
            cmd.Parameters.AddWithValue("$displayName", app.DisplayName);
            cmd.Parameters.AddWithValue("$mcpServerName", app.McpServerName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("$lastSeenAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AppAction>> GetAppActions(string appName)
    {
        var actions = new List<AppAction>();

        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, app_name, position, action_type, action_name, action_data, enabled
            FROM app_actions
            WHERE app_name = $appName
            ORDER BY position
        ";
        cmd.Parameters.AddWithValue("$appName", appName.ToLowerInvariant());

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var appNameValue = reader.GetString(1);
            var actionNameValue = reader.GetString(4);
            var actionDataValue = reader.GetString(5);
            var actionTypeStr = reader.GetString(3);

            actions.Add(new AppAction
            {
                Id = reader.GetInt32(0),
                AppName = appNameValue,
                Position = reader.GetInt32(2),
                Type = Enum.TryParse<ActionType>(actionTypeStr, out var actionType) ? actionType : ActionType.Prompt,
                ActionName = Helpers.ActionNameSanitizer.Sanitize(actionNameValue),
                ActionDataJson = actionDataValue,
                Enabled = reader.GetInt32(6) == 1
            });
        }

        // Ensure we always return 8 items (positions 0-7)
        // Fill missing positions with empty actions
        for (int i = 0; i < 8; i++)
        {
            if (!actions.Any(a => a.Position == i))
            {
                actions.Add(new AppAction
                {
                    AppName = appName.ToLowerInvariant(),
                    Position = i,
                    Type = ActionType.Prompt,
                    ActionName = string.Empty,
                    ActionDataJson = "{}",
                    Enabled = false
                });
            }
        }

        return actions.OrderBy(a => a.Position).ToList();
    }

    public async Task SaveAppActions(string appName, List<AppAction> actions)
    {
        using var transaction = _connection.BeginTransaction();

        // Delete all existing actions for this app
        var deleteCmd = _connection.CreateCommand();
        deleteCmd.CommandText = @"
            DELETE FROM app_actions
            WHERE app_name = $appName
        ";
        deleteCmd.Parameters.AddWithValue("$appName", appName.ToLowerInvariant());
        await deleteCmd.ExecuteNonQueryAsync();

        // Insert all 8 actions
        foreach (var action in actions) 
        {
            // Skip empty/disabled actions if desired, or save all
            var insertCmd = _connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO app_actions (app_name, position, action_type, action_name, action_data, enabled)
                VALUES ($appName, $position, $actionType, $actionName, $actionData, $enabled)
            ";
            insertCmd.Parameters.AddWithValue("$appName", appName.ToLowerInvariant());
            insertCmd.Parameters.AddWithValue("$position", action.Position);
            insertCmd.Parameters.AddWithValue("$actionType", action.Type.ToString());
            insertCmd.Parameters.AddWithValue("$actionName", action.ActionName);
            insertCmd.Parameters.AddWithValue("$actionData", action.ActionDataJson);
            insertCmd.Parameters.AddWithValue("$enabled", action.Enabled ? 1 : 0);

            await insertCmd.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    public async Task DeleteApp(string appName)
    {
        using var transaction = _connection.BeginTransaction();

        // Delete all actions for this app
        var deleteActionsCmd = _connection.CreateCommand();
        deleteActionsCmd.CommandText = @"
            DELETE FROM app_actions
            WHERE app_name = $appName
        ";
        deleteActionsCmd.Parameters.AddWithValue("$appName", appName.ToLowerInvariant());
        await deleteActionsCmd.ExecuteNonQueryAsync();

        // Delete the app itself
        var deleteAppCmd = _connection.CreateCommand();
        deleteAppCmd.CommandText = @"
            DELETE FROM remembered_apps
            WHERE app_name = $appName
        ";
        deleteAppCmd.Parameters.AddWithValue("$appName", appName.ToLowerInvariant());
        await deleteAppCmd.ExecuteNonQueryAsync();

        transaction.Commit();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
