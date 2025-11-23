using Loupedeck.AdaptiveRingPlugin.Models;
using Loupedeck.AdaptiveRingPlugin.Models.ActionData;
using Loupedeck.AdaptiveRingPlugin.Actions;
using System.Text.Json;
using System.Collections.Generic;

namespace Loupedeck.AdaptiveRingPlugin.Services;

/// <summary>
/// Manages the Actions Ring UI and updates it with app-specific actions.
/// Handles conversion from AppAction models to Ring SDK format.
/// </summary>
public class ActionsRingManager
{
    private readonly ActionPersistenceService _persistenceService;
    private readonly Plugin _plugin;
    private readonly AppAction?[] _currentActions;

    public event EventHandler? ActionsUpdated;

    public ActionsRingManager(ActionPersistenceService persistenceService, Plugin plugin)
    {
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

        // Initialize empty actions array
        _currentActions = new AppAction?[8];
        
        PluginLog.Info("ActionsRingManager initialized");
    }

    /// <summary>
    /// Get the action at the specified position.
    /// </summary>
    public AppAction? GetAction(int position)
    {
        if (position < 0 || position >= 8) return null;
        return _currentActions[position];
    }

    /// <summary>
    /// Load and display actions for a specific app on the Actions Ring.
    /// </summary>
    /// <param name="appName">App identifier</param>
    public async Task LoadActionsForAppAsync(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            PluginLog.Warning("LoadActionsForApp called with null or empty appName");
            return;
        }

        try
        {
            PluginLog.Info($"Loading actions for app: {appName}");

            // Get actions from persistence service
            var actions = await _persistenceService.GetAppActionsAsync(appName);

            // Clear current actions first
            Array.Clear(_currentActions, 0, 8);

            if (actions != null && actions.Count > 0)
            {
                PluginLog.Info($"Found {actions.Count} actions for app: {appName}");

                // Update internal state
                foreach (var action in actions)
                {
                    if (action.Position >= 0 && action.Position < 8)
                    {
                        _currentActions[action.Position] = action;
                        LogActionDetails(action.Position, action);
                    }
                }
            }
            else
            {
                PluginLog.Info($"No actions found for app: {appName}");
            }

            // Notify listeners (AdaptiveRingCommand)
            OnActionsUpdated();

            PluginLog.Info($"Successfully loaded actions for {appName}");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error loading actions for app '{appName}': {ex.Message}");
        }
    }

    /// <summary>
    /// Update a single ring position with an action.
    /// </summary>
    /// <param name="position">Ring position (0-7)</param>
    /// <param name="action">Action to display</param>
    public void UpdateRingPosition(int position, AppAction action)
    {
        if (position < 0 || position > 7)
        {
            PluginLog.Warning($"Invalid ring position: {position}. Must be 0-7.");
            return;
        }

        try
        {
            _currentActions[position] = action;
            
            if (action != null)
            {
                PluginLog.Verbose($"Updating ring position {position} with action: {action.ActionName}");
                LogActionDetails(position, action);
            }
            else
            {
                 PluginLog.Verbose($"Cleared ring position {position}");
            }

            OnActionsUpdated();
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error updating ring position {position}: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all actions from the Actions Ring.
    /// </summary>
    public void ClearActions()
    {
        try
        {
            PluginLog.Info("Clearing all ring actions");
            Array.Clear(_currentActions, 0, 8);
            OnActionsUpdated();
            PluginLog.Info("Ring actions cleared");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error clearing ring actions: {ex.Message}");
        }
    }

    private void OnActionsUpdated()
    {
        ActionsUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Check if there are any actions loaded for a specific app.
    /// </summary>
    /// <param name="appName">App identifier</param>
    /// <returns>True if actions exist, false otherwise</returns>
    public async Task<bool> HasActionsForAppAsync(string appName)
    {
        return await _persistenceService.HasRememberedAppAsync(appName);
    }

    /// <summary>
    /// Get action count for an app.
    /// </summary>
    /// <param name="appName">App identifier</param>
    /// <returns>Number of actions for the app</returns>
    public async Task<int> GetActionCountAsync(string appName)
    {
        var actions = await _persistenceService.GetAppActionsAsync(appName);
        return actions?.Count ?? 0;
    }

    // ============================================================
    // Synchronous wrapper methods for backward compatibility
    // ============================================================

    /// <summary>
    /// Synchronous wrapper for LoadActionsForAppAsync.
    /// </summary>
    public void LoadActionsForApp(string appName)
    {
        LoadActionsForAppAsync(appName).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Log details about an action for debugging.
    /// </summary>
    private void LogActionDetails(int position, AppAction action)
    {
        PluginLog.Verbose($"  Position: {position}");
        PluginLog.Verbose($"  Name: {action.ActionName}");
        PluginLog.Verbose($"  Type: {action.Type}");
        PluginLog.Verbose($"  Enabled: {action.Enabled}");

        // Deserialize and log action data based on type
        try
        {
            switch (action.Type)
            {
                case ActionType.Prompt:
                    var promptData = JsonSerializer.Deserialize<PromptActionData>(action.ActionDataJson);
                    if (promptData != null)
                    {
                        PluginLog.Verbose($"  MCP Server: {promptData.McpServerName}");
                        PluginLog.Verbose($"  Tool: {promptData.ToolName}");
                    }
                    break;

                case ActionType.Keybind:
                    var keybindData = JsonSerializer.Deserialize<KeybindActionData>(action.ActionDataJson);
                    if (keybindData != null && keybindData.Keys != null)
                    {
                        PluginLog.Verbose($"  Keys: {string.Join(" + ", keybindData.Keys)}");
                    }
                    break;

                case ActionType.Python:
                    var pythonData = JsonSerializer.Deserialize<PythonActionData>(action.ActionDataJson);
                    if (pythonData != null)
                    {
                        PluginLog.Verbose($"  Script: {pythonData.ScriptPath ?? "(inline)"}");
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"Could not deserialize action data: {ex.Message}");
        }
    }
}
