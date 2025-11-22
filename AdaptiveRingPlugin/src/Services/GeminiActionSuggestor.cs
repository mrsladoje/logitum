namespace Loupedeck.AdaptiveRingPlugin.Services;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Loupedeck.AdaptiveRingPlugin.Models;
using Loupedeck.AdaptiveRingPlugin.Models.ActionData;

public class GeminiActionSuggestor
{
    private readonly string _scriptPath;
    private readonly string _pythonPath = "python"; // Assume python is in PATH

    public GeminiActionSuggestor()
    {
        try
        {
            _scriptPath = IntelligenceServiceLocator.GetScriptPath();
            PluginLog.Info($"GeminiActionSuggestor using script at: {_scriptPath}");
        }
        catch (Exception ex)
        {
            _scriptPath = string.Empty;
            PluginLog.Error($"GeminiActionSuggestor: Failed to prepare IntelligenceService.py: {ex.Message}");
        }
    }

    public async Task<List<AppAction>> SuggestActionsAsync(string appName, List<MCPServerData>? mcpServers, bool mcpAvailable = true)
    {
        try
        {
            PluginLog.Info($"Requesting AI suggestions for {appName} via Python service...");

            if (string.IsNullOrEmpty(_scriptPath) || !File.Exists(_scriptPath))
            {
                PluginLog.Error($"IntelligenceService script missing at {_scriptPath}. Using defaults.");
                return GetUniversalDefaultActions(appName);
            }

            var mcpServersJson = "[]";
            if (mcpServers != null && mcpServers.Count > 0)
            {
                mcpServersJson = JsonSerializer.Serialize(mcpServers);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{_scriptPath}\" --app \"{appName}\" --mcp-servers \"{mcpServersJson.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_scriptPath) // Set working dir to script location
            };
            
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask);
            process.WaitForExit();

            var output = outputTask.Result;
            var error = errorTask.Result;

            if (!string.IsNullOrWhiteSpace(error))
            {
                // Log stderr as info/warning since the script uses it for logging
                PluginLog.Info($"[Python Service] {error.Trim()}");
            }

            if (process.ExitCode != 0)
            {
                PluginLog.Error($"Python service exited with code {process.ExitCode}");
                return GetUniversalDefaultActions(appName);
            }

            return ParsePythonResponse(appName, output);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error executing Intelligence Service: {ex.Message}");
            return GetUniversalDefaultActions(appName);
        }
    }

    private List<AppAction> ParsePythonResponse(string appName, string responseText)
    {
        try
        {
            var cleanedText = responseText.Trim();
            if (string.IsNullOrEmpty(cleanedText))
            {
                PluginLog.Warning("Empty response from Python service");
                return GetUniversalDefaultActions(appName);
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var geminiActions = JsonSerializer.Deserialize<List<GeminiActionResponse>>(cleanedText, jsonOptions);

            if (geminiActions == null || geminiActions.Count != 8)
            {
                PluginLog.Warning($"Invalid response: Expected 8 actions, got {geminiActions?.Count ?? 0}");
                return GetUniversalDefaultActions(appName);
            }

            var actions = new List<AppAction>();

            foreach (var geminiAction in geminiActions)
            {
                try
                {
                    var action = ConvertGeminiAction(appName, geminiAction);
                    if (action != null)
                    {
                        actions.Add(action);
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Error converting action at position {geminiAction.Position}: {ex.Message}");
                }
            }

            return actions.Count == 8 ? actions : GetUniversalDefaultActions(appName);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error parsing Python response: {ex.Message}");
            PluginLog.Verbose($"Raw response: {responseText}");
            return GetUniversalDefaultActions(appName);
        }
    }

    private AppAction? ConvertGeminiAction(string appName, GeminiActionResponse geminiAction)
    {
        var actionType = geminiAction.Type.ToLower() switch
        {
            "keybind" => ActionType.Keybind,
            "prompt" => ActionType.Prompt,
            "python" => ActionType.Python,
            _ => ActionType.Keybind
        };

        string actionDataJson;

        switch (actionType)
        {
            case ActionType.Keybind:
                var keybindData = new KeybindActionData
                {
                    Keys = geminiAction.ActionData.Keys ?? new List<string> { "Ctrl", "C" },
                    Description = geminiAction.ActionData.Description
                };
                actionDataJson = JsonSerializer.Serialize(keybindData);
                break;

            case ActionType.Prompt:
                var promptData = new PromptActionData
                {
                    McpServerName = geminiAction.ActionData.McpServerName ?? "",
                    ToolName = geminiAction.ActionData.ToolName ?? "",
                    Parameters = geminiAction.ActionData.Parameters,
                    Description = geminiAction.ActionData.Description
                };
                actionDataJson = JsonSerializer.Serialize(promptData);
                break;

            case ActionType.Python:
                var pythonData = new PythonActionData
                {
                    ScriptCode = geminiAction.ActionData.ScriptCode,
                    ScriptPath = geminiAction.ActionData.ScriptPath,
                    Arguments = geminiAction.ActionData.Arguments,
                    Description = geminiAction.ActionData.Description
                };
                actionDataJson = JsonSerializer.Serialize(pythonData);
                break;

            default:
                return null;
        }

        return new AppAction
        {
            AppName = appName,
            Position = geminiAction.Position,
            Type = actionType,
            ActionName = geminiAction.ActionName,
            ActionDataJson = actionDataJson,
            Enabled = true
        };
    }

    private List<AppAction> GetUniversalDefaultActions(string appName)
    {
        var fallbackActions = new List<(string name, List<string> keys, string desc)>
        {
            ("Copy", new List<string> { "Ctrl", "C" }, "Copy selected text"),
            ("Paste", new List<string> { "Ctrl", "V" }, "Paste from clipboard"),
            ("Save", new List<string> { "Ctrl", "S" }, "Save current file"),
            ("Undo", new List<string> { "Ctrl", "Z" }, "Undo last action"),
            ("Find", new List<string> { "Ctrl", "F" }, "Find text"),
            ("Select All", new List<string> { "Ctrl", "A" }, "Select all"),
            ("New Tab", new List<string> { "Ctrl", "T" }, "New tab"),
            ("Close", new List<string> { "Ctrl", "W" }, "Smart close")
        };

        var actions = new List<AppAction>();

        for (int i = 0; i < 8; i++)
        {
            var (name, keys, desc) = fallbackActions[i];

            var keybindData = new KeybindActionData
            {
                Keys = keys,
                Description = desc
            };

            var action = new AppAction
            {
                AppName = appName,
                Position = i,
                Type = ActionType.Keybind,
                ActionName = name,
                ActionDataJson = JsonSerializer.Serialize(keybindData),
                Enabled = true
            };

            actions.Add(action);
        }

        return actions;
    }

    private class GeminiActionResponse
    {
        public int Position { get; set; }
        public string Type { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public GeminiActionData ActionData { get; set; } = new();
    }

    private class GeminiActionData
    {
        public List<string>? Keys { get; set; }
        public string? McpServerName { get; set; }
        public string? ToolName { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }
        public string? ScriptCode { get; set; }
        public string? ScriptPath { get; set; }
        public List<string>? Arguments { get; set; }
        public string? Description { get; set; }
    }
}
