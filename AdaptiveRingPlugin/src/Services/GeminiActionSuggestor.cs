namespace Loupedeck.AdaptiveRingPlugin.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Loupedeck.AdaptiveRingPlugin.Models;
using Loupedeck.AdaptiveRingPlugin.Models.ActionData;
using Mscc.GenerativeAI;

public class GeminiActionSuggestor
{
    private readonly string? _apiKey;
    private readonly GenerativeModel? _model;

    public GeminiActionSuggestor()
    {
        // Load API key from environment variable
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            PluginLog.Warning("GEMINI_API_KEY environment variable not set. Will use fallback actions.");
            // Uncomment to disable fallback and force no-op if key is missing
            // _model = null;
            // For now, we continue without a model which will trigger fallback logic in SuggestActionsAsync
            _model = null;
        }
        else
        {
            try
            {
                var googleAI = new GoogleAI(_apiKey);
                _model = googleAI.GenerativeModel(model: "gemini-2.5-flash");
                PluginLog.Info("Gemini API initialized successfully with model: gemini-2.5-flash");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Failed to initialize Gemini API: {ex.Message}");
                _model = null;
            }
        }
    }

    public async Task<List<AppAction>> SuggestActionsAsync(string appName, List<MCPServerData>? mcpServers)
    {
        // If API key is missing or model failed to initialize, return fallback
        if (_model == null)
        {
            PluginLog.Info($"Using fallback actions for {appName}");
            return GetFallbackActions(appName);
        }

        try
        {
            PluginLog.Info($"Requesting Gemini AI suggestions for {appName}...");

            // Build the prompt
            var prompt = BuildPrompt(appName, mcpServers);
            PluginLog.Verbose($"Prompt: {prompt}");

            // Call Gemini API
            var response = await _model.GenerateContent(prompt);
            var responseText = response.Text ?? string.Empty;

            PluginLog.Info($"Gemini response received: {responseText.Length} characters");
            PluginLog.Verbose($"Full response: {responseText}");

            // Parse the response
            var actions = ParseGeminiResponse(appName, responseText);

            if (actions.Count == 8)
            {
                PluginLog.Info($"Successfully generated {actions.Count} actions for {appName}");
                return actions;
            }
            else
            {
                PluginLog.Warning($"Gemini returned {actions.Count} actions instead of 8. Using fallback.");
                return GetFallbackActions(appName);
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error calling Gemini API: {ex.Message}");
            PluginLog.Info($"Using fallback actions for {appName}");
            return GetFallbackActions(appName);
        }
    }

    private string BuildPrompt(string appName, List<MCPServerData>? mcpServers)
    {
        var prompt = $@"You are an expert at creating productivity workflows for {appName}.

Generate exactly 8 actions for the Actions Ring (positions 0-7).

Priority order for action types:
1. Keybind (fastest execution) - Use for common shortcuts
2. Prompt (when MCP tools are available) - Use for AI-assisted tasks
3. Python (for complex automation) - Use for advanced scripting

";

        // Add MCP tools information if available
        if (mcpServers != null && mcpServers.Count > 0)
        {
            prompt += "Available MCP tools:\n";
            foreach (var server in mcpServers)
            {
                prompt += $"  - Server: {server.ServerName} ({server.PackageName})\n";
                if (server.Tools != null && server.Tools.Count > 0)
                {
                    foreach (var tool in server.Tools.Take(10))
                    {
                        prompt += $"    * {tool.Key}: {tool.Value.Description}\n";
                    }
                }
            }
            prompt += "\n";
        }

        prompt += @"Return ONLY a valid JSON array with exactly 8 objects in this exact format:
[
  {
    ""position"": 0,
    ""type"": ""Keybind"",
    ""actionName"": ""Copy"",
    ""actionData"": {
      ""keys"": [""Ctrl"", ""C""],
      ""description"": ""Copy selected text""
    }
  },
  {
    ""position"": 1,
    ""type"": ""Prompt"",
    ""actionName"": ""Analyze Code"",
    ""actionData"": {
      ""mcpServerName"": ""vscode"",
      ""toolName"": ""analyze"",
      ""parameters"": {},
      ""description"": ""Analyze selected code""
    }
  },
  {
    ""position"": 2,
    ""type"": ""Python"",
    ""actionName"": ""Custom Script"",
    ""actionData"": {
      ""scriptCode"": ""print('Hello')"",
      ""arguments"": [],
      ""description"": ""Run custom script""
    }
  }
]

Important rules:
- Return ONLY the JSON array, no markdown formatting, no code blocks, no explanations
- Each action must have position (0-7), type (Keybind/Prompt/Python), actionName, and actionData
- For Keybind: actionData must have ""keys"" array and optional ""description""
- For Prompt: actionData must have ""mcpServerName"", ""toolName"", optional ""parameters"" dict, and optional ""description""
- For Python: actionData must have ""scriptCode"" or ""scriptPath"", optional ""arguments"" array, and optional ""description""
- Make actions relevant and useful for {appName}
- Prioritize common workflows and frequent tasks

Generate 8 optimal actions now:";

        return prompt;
    }

    private List<AppAction> ParseGeminiResponse(string appName, string responseText)
    {
        try
        {
            // Clean up the response text (remove markdown code blocks if present)
            var cleanedText = responseText.Trim();
            if (cleanedText.StartsWith("```json"))
            {
                cleanedText = cleanedText.Substring(7);
            }
            else if (cleanedText.StartsWith("```"))
            {
                cleanedText = cleanedText.Substring(3);
            }
            if (cleanedText.EndsWith("```"))
            {
                cleanedText = cleanedText.Substring(0, cleanedText.Length - 3);
            }
            cleanedText = cleanedText.Trim();

            // Parse the JSON response
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var geminiActions = JsonSerializer.Deserialize<List<GeminiActionResponse>>(cleanedText, jsonOptions);

            if (geminiActions == null || geminiActions.Count != 8)
            {
                PluginLog.Warning($"Invalid Gemini response: Expected 8 actions, got {geminiActions?.Count ?? 0}");
                return GetFallbackActions(appName);
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

            return actions.Count == 8 ? actions : GetFallbackActions(appName);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Error parsing Gemini response: {ex.Message}");
            return GetFallbackActions(appName);
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

    private List<AppAction> GetFallbackActions(string appName)
    {
        var fallbackActions = new List<(string name, List<string> keys, string desc)>
        {
            ("Copy", new List<string> { "Ctrl", "C" }, "Copy selected text"),
            ("Paste", new List<string> { "Ctrl", "V" }, "Paste from clipboard"),
            ("Save", new List<string> { "Ctrl", "S" }, "Save current file"),
            ("Undo", new List<string> { "Ctrl", "Z" }, "Undo last action"),
            ("Find", new List<string> { "Ctrl", "F" }, "Find text"),
            ("New", new List<string> { "Ctrl", "N" }, "Create new"),
            ("Open", new List<string> { "Ctrl", "O" }, "Open file"),
            ("Close", new List<string> { "Ctrl", "W" }, "Close window")
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

        PluginLog.Info($"Generated {actions.Count} fallback actions for {appName}");
        return actions;
    }

    // Helper classes for JSON parsing
    private class GeminiActionResponse
    {
        public int Position { get; set; }
        public string Type { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public GeminiActionData ActionData { get; set; } = new();
    }

    private class GeminiActionData
    {
        // Keybind fields
        public List<string>? Keys { get; set; }

        // Prompt fields
        public string? McpServerName { get; set; }
        public string? ToolName { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }

        // Python fields
        public string? ScriptCode { get; set; }
        public string? ScriptPath { get; set; }
        public List<string>? Arguments { get; set; }

        // Common field
        public string? Description { get; set; }
    }
}
