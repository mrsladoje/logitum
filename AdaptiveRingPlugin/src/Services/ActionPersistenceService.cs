using System.Text.Json;
using Loupedeck.AdaptiveRingPlugin.Models;
using Microsoft.Data.Sqlite;

namespace Loupedeck.AdaptiveRingPlugin.Services;

/// <summary>
/// Service for persisting and retrieving app actions from the database.
/// Manages the remembered_apps and app_actions tables.
/// </summary>
public class ActionPersistenceService
{
    private readonly AppDatabase _database;

    public ActionPersistenceService(AppDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <summary>
    /// Load a remembered app from the database.
    /// </summary>
    /// <param name="appName">The app name to look up (case-insensitive)</param>
    /// <returns>RememberedApp object if found, null otherwise</returns>
    public async Task<RememberedApp?> GetRememberedAppAsync(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            PluginLog.Warning("GetRememberedApp called with null or empty appName");
            return null;
        }

        try
        {
            var connection = GetConnection();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT app_name, display_name, mcp_server_name, created_at, last_seen_at
                FROM remembered_apps
                WHERE LOWER(app_name) = LOWER($appName)
            ";
            cmd.Parameters.AddWithValue("$appName", appName);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new RememberedApp
                {
                    AppName = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    McpServerName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    CreatedAt = reader.GetInt64(3),
                    LastSeenAt = reader.GetInt64(4)
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error loading remembered app '{appName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save or update an app with its 8 actions atomically.
    /// </summary>
    /// <param name="appName">App identifier</param>
    /// <param name="displayName">Human-readable app name</param>
    /// <param name="actions">List of 8 actions (must be exactly 8)</param>
    /// <param name="mcpServerName">Optional MCP server name</param>
    public async Task SaveAppActionsAsync(string appName, string displayName, List<AppAction> actions, string? mcpServerName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            throw new ArgumentException("App name cannot be null or empty", nameof(appName));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be null or empty", nameof(displayName));
        }

        if (actions == null || actions.Count != 8)
        {
            throw new ArgumentException("Actions list must contain exactly 8 actions", nameof(actions));
        }

        // Validate positions 0-7
        for (int i = 0; i < 8; i++)
        {
            if (!actions.Any(a => a.Position == i))
            {
                throw new ArgumentException($"Actions list must contain action for position {i}", nameof(actions));
            }
        }

        try
        {
            var connection = GetConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Insert or update the remembered app
                var appCmd = connection.CreateCommand();
                appCmd.Transaction = transaction;
                appCmd.CommandText = @"
                    INSERT INTO remembered_apps (app_name, display_name, mcp_server_name, created_at, last_seen_at)
                    VALUES ($appName, $displayName, $mcpServerName, $now, $now)
                    ON CONFLICT(app_name) DO UPDATE SET
                        display_name = $displayName,
                        mcp_server_name = $mcpServerName,
                        last_seen_at = $now
                ";
                appCmd.Parameters.AddWithValue("$appName", appName);
                appCmd.Parameters.AddWithValue("$displayName", displayName);
                appCmd.Parameters.AddWithValue("$mcpServerName", mcpServerName ?? (object)DBNull.Value);
                appCmd.Parameters.AddWithValue("$now", now);

                await appCmd.ExecuteNonQueryAsync();

                // Delete existing actions for this app
                var deleteCmd = connection.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = "DELETE FROM app_actions WHERE app_name = $appName";
                deleteCmd.Parameters.AddWithValue("$appName", appName);
                await deleteCmd.ExecuteNonQueryAsync();

                // Insert all 8 actions
                foreach (var action in actions)
                {
                    var actionCmd = connection.CreateCommand();
                    actionCmd.Transaction = transaction;
                    actionCmd.CommandText = @"
                        INSERT INTO app_actions (app_name, position, action_type, action_name, action_data, enabled)
                        VALUES ($appName, $position, $actionType, $actionName, $actionData, $enabled)
                    ";
                    actionCmd.Parameters.AddWithValue("$appName", appName);
                    actionCmd.Parameters.AddWithValue("$position", action.Position);
                    actionCmd.Parameters.AddWithValue("$actionType", action.Type.ToString());
                    actionCmd.Parameters.AddWithValue("$actionName", action.ActionName);
                    actionCmd.Parameters.AddWithValue("$actionData", action.ActionDataJson);
                    actionCmd.Parameters.AddWithValue("$enabled", action.Enabled ? 1 : 0);

                    await actionCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                PluginLog.Info($"Successfully saved {actions.Count} actions for app '{appName}'");
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error saving app actions for '{appName}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Load all 8 actions for an app.
    /// </summary>
    /// <param name="appName">App identifier</param>
    /// <returns>List of actions, or empty list if app not found</returns>
    public async Task<List<AppAction>> GetAppActionsAsync(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            PluginLog.Warning("GetAppActions called with null or empty appName");
            return new List<AppAction>();
        }

        try
        {
            var connection = GetConnection();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, app_name, position, action_type, action_name, action_data, enabled
                FROM app_actions
                WHERE LOWER(app_name) = LOWER($appName)
                ORDER BY position
            ";
            cmd.Parameters.AddWithValue("$appName", appName);

            var actions = new List<AppAction>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var actionTypeStr = reader.GetString(3);
                var actionType = Enum.TryParse<ActionType>(actionTypeStr, out var parsedType)
                    ? parsedType
                    : ActionType.Prompt;

                actions.Add(new AppAction
                {
                    Id = reader.GetInt32(0),
                    AppName = reader.GetString(1),
                    Position = reader.GetInt32(2),
                    Type = actionType,
                    ActionName = reader.GetString(4),
                    ActionDataJson = reader.GetString(5),
                    Enabled = reader.GetInt32(6) == 1
                });
            }

            return actions;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error loading actions for app '{appName}': {ex.Message}");
            return new List<AppAction>();
        }
    }

    /// <summary>
    /// Quick check if an app exists in the database.
    /// </summary>
    /// <param name="appName">App identifier</param>
    /// <returns>True if app exists, false otherwise</returns>
    public async Task<bool> HasRememberedAppAsync(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            return false;
        }

        try
        {
            var connection = GetConnection();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM remembered_apps
                WHERE LOWER(app_name) = LOWER($appName)
            ";
            cmd.Parameters.AddWithValue("$appName", appName);

            var result = await cmd.ExecuteScalarAsync();
            return result != null && Convert.ToInt64(result) > 0;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error checking if app '{appName}' exists: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Update the last seen timestamp for an app.
    /// </summary>
    /// <param name="appName">App identifier</param>
    public async Task UpdateLastSeenAsync(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            return;
        }

        try
        {
            var connection = GetConnection();
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE remembered_apps
                SET last_seen_at = $now
                WHERE LOWER(app_name) = LOWER($appName)
            ";
            cmd.Parameters.AddWithValue("$appName", appName);
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error updating last seen for app '{appName}': {ex.Message}");
        }
    }

    // ============================================================
    // Synchronous wrapper methods for backward compatibility
    // ============================================================

    /// <summary>
    /// Synchronous wrapper for GetRememberedAppAsync.
    /// </summary>
    public RememberedApp? GetRememberedApp(string appName)
    {
        return GetRememberedAppAsync(appName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Synchronous wrapper for SaveAppActionsAsync.
    /// </summary>
    public void SaveAppActions(string appName, string displayName, List<AppAction> actions, string? mcpServerName)
    {
        SaveAppActionsAsync(appName, displayName, actions, mcpServerName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Synchronous wrapper for GetAppActionsAsync.
    /// </summary>
    public List<AppAction> GetAppActions(string appName)
    {
        return GetAppActionsAsync(appName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Synchronous wrapper for HasRememberedAppAsync.
    /// </summary>
    public bool HasRememberedApp(string appName)
    {
        return HasRememberedAppAsync(appName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Synchronous wrapper for UpdateLastSeenAsync.
    /// </summary>
    public void UpdateLastSeen(string appName)
    {
        UpdateLastSeenAsync(appName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Helper method to get the connection from AppDatabase using reflection.
    /// This is necessary because the connection is private in AppDatabase.
    /// </summary>
    private SqliteConnection GetConnection()
    {
        var connectionField = typeof(AppDatabase).GetField("_connection",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (connectionField == null)
        {
            throw new InvalidOperationException("Could not access database connection");
        }

        var connection = connectionField.GetValue(_database) as SqliteConnection;
        if (connection == null)
        {
            throw new InvalidOperationException("Database connection is null");
        }

        return connection;
    }
}
