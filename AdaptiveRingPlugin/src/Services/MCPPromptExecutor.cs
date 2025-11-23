namespace Loupedeck.AdaptiveRingPlugin.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Loupedeck.AdaptiveRingPlugin.Models;
    using Loupedeck.AdaptiveRingPlugin.Models.ActionData;

    /// <summary>
    /// Executes MCP (Model Context Protocol) prompt actions by:
    /// 1. Connecting to MCP servers via stdio
    /// 2. Using Python Intelligence Service to orchestrate tool calls
    /// 3. Executing the tools and returning results
    /// </summary>
    public class MCPPromptExecutor
    {
        private readonly AppDatabase _database;
        private readonly string _scriptPath;
        private readonly string _pythonPath = "python";
        private static readonly Dictionary<string, MCPClient> _connectionPool = new();
        private static readonly object _poolLock = new object();

        public MCPPromptExecutor(AppDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            try
            {
                _scriptPath = IntelligenceServiceLocator.GetScriptPath();
                PluginLog.Info($"MCPPromptExecutor using script at: {_scriptPath}");
            }
            catch (Exception ex)
            {
                _scriptPath = string.Empty;
                PluginLog.Error($"MCPPromptExecutor: Failed to prepare IntelligenceService.py: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute an MCP prompt action.
        /// </summary>
        public async void Execute(AppAction action)
        {
            if (action.Type != ActionType.Prompt) return;

            try
            {
                await ExecuteAsync(action);
            }
            catch (Exception ex)
            {
                PluginLog.Error($"MCPPromptExecutor: Execute failed: {ex.Message}");
                ShowNotification($"❌ MCP Execution Error: {ex.Message}");
            }
        }

        private async Task ExecuteAsync(AppAction action)
        {
            try
            {
                var promptData = JsonSerializer.Deserialize<PromptActionData>(action.ActionDataJson);
                if (promptData == null)
                {
                    PluginLog.Warning("MCPPromptExecutor: Failed to deserialize prompt data");
                    return;
                }

                PluginLog.Info($"MCPPromptExecutor: Executing on {promptData.McpServerName}");

                // Get MCP server connection info from database
                var serverData = await GetServerDataAsync(promptData.McpServerName);
                if (serverData == null || string.IsNullOrEmpty(serverData.StdioCommand))
                {
                    PluginLog.Error($"MCPPromptExecutor: No connection info for server '{promptData.McpServerName}'");
                    ShowNotification($"❌ MCP server '{promptData.McpServerName}' not configured");
                    return;
                }

                // Get or create MCP client connection
                var mcpClient = await GetOrCreateConnectionAsync(serverData.ServerName, serverData.StdioCommand);
                if (mcpClient == null || !mcpClient.IsConnected)
                {
                    PluginLog.Error($"MCPPromptExecutor: Failed to connect to MCP server");
                    ShowNotification($"❌ Failed to connect to {serverData.ServerName}");
                    return;
                }

                // Scenario 1: Direct tool call (tool name is specified)
                if (!string.IsNullOrEmpty(promptData.ToolName))
                {
                    await ExecuteDirectToolCallAsync(mcpClient, promptData);
                }
                // Scenario 2: LLM-orchestrated (only description provided)
                else if (!string.IsNullOrEmpty(promptData.Description))
                {
                    await ExecuteLLMOrchestratedAsync(mcpClient, promptData);
                }
                else
                {
                    PluginLog.Warning("MCPPromptExecutor: No tool name or description provided");
                    ShowNotification("❌ Invalid prompt configuration");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"MCPPromptExecutor: Execution error: {ex.Message}");
                ShowNotification($"❌ MCP Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute a specific tool directly with given parameters.
        /// </summary>
        private async Task ExecuteDirectToolCallAsync(MCPClient mcpClient, PromptActionData promptData)
        {
            PluginLog.Info($"MCPPromptExecutor: Calling tool '{promptData.ToolName}' directly");

            ShowNotification($"🔧 Executing {promptData.ToolName}...");

            try
            {
                var result = await mcpClient.CallToolAsync(promptData.ToolName, promptData.Parameters);

                if (result != null)
                {
                    if (result.IsError)
                    {
                        PluginLog.Error($"MCPPromptExecutor: Tool error: {result.Content}");
                        ShowNotification($"❌ {promptData.ToolName}: {TruncateResult(result.Content)}");
                    }
                    else
                    {
                        PluginLog.Info($"MCPPromptExecutor: Tool result: {result.Content}");
                        ShowNotification($"✅ {promptData.ToolName}: {TruncateResult(result.Content)}");
                    }
                }
                else
                {
                    PluginLog.Warning($"MCPPromptExecutor: Tool '{promptData.ToolName}' returned no result (may indicate initialization failure)");
                    ShowNotification($"❌ {promptData.ToolName}: No result (check logs)");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"MCPPromptExecutor: Exception calling tool '{promptData.ToolName}': {ex.Message}");
                ShowNotification($"❌ {promptData.ToolName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Use Python Intelligence Service to orchestrate which tools to call based on the description.
        /// </summary>
        private async Task ExecuteLLMOrchestratedAsync(MCPClient mcpClient, PromptActionData promptData)
        {
            if (string.IsNullOrEmpty(_scriptPath) || !File.Exists(_scriptPath))
            {
                PluginLog.Warning("MCPPromptExecutor: IntelligenceService script not found");
                ShowNotification("❌ Intelligence Service missing");
                return;
            }

            PluginLog.Info($"MCPPromptExecutor: Using Python Service to orchestrate: {promptData.Description}");
            ShowNotification($"🤖 Analyzing: {promptData.Description}...");

            // List available tools from MCP server
            var tools = await mcpClient.ListToolsAsync();
            if (tools.Count == 0)
            {
                PluginLog.Warning("MCPPromptExecutor: No tools available from MCP server");
                ShowNotification("❌ No tools available");
                return;
            }

            PluginLog.Info($"MCPPromptExecutor: Found {tools.Count} tools, asking Python Service...");

            try
            {
                var toolsJson = JsonSerializer.Serialize(tools);
                
                // Escape arguments for command line
                var toolsJsonEscaped = toolsJson.Replace("\\", "\\\\").Replace("\"", "\\\"");
                var description = promptData.Description ?? "";
                var promptEscaped = description.Replace("\\", "\\\\").Replace("\"", "\\\"");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"\"{_scriptPath}\" --mode orchestrate --tools \"{toolsJsonEscaped}\" --prompt \"{promptEscaped}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_scriptPath)
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, errorTask);
                process.WaitForExit();

                var responseText = outputTask.Result;
                var error = errorTask.Result;

                if (!string.IsNullOrWhiteSpace(error))
                {
                    PluginLog.Info($"[Python Service] {error.Trim()}");
                }

                if (process.ExitCode != 0)
                {
                    PluginLog.Error($"Python service exited with code {process.ExitCode}");
                    ShowNotification("❌ AI Service Error");
                    return;
                }

                PluginLog.Info($"MCPPromptExecutor: Python response: {responseText}");

                // Parse Python response
                var toolCall = ParseToolCall(responseText);
                if (toolCall != null && toolCall.Value.ToolName != "none")
                {
                    ShowNotification($"🔧 Calling {toolCall.Value.ToolName}...");

                    var result = await mcpClient.CallToolAsync(toolCall.Value.ToolName, toolCall.Value.Arguments);

                    if (result != null)
                    {
                        if (result.IsError)
                        {
                            ShowNotification($"❌ {result.Content}");
                        }
                        else
                        {
                            ShowNotification($"✅ {TruncateResult(result.Content)}");
                        }
                    }
                }
                else
                {
                    ShowNotification("❌ No appropriate tool found");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"MCPPromptExecutor: Orchestration failed: {ex.Message}");
                ShowNotification($"❌ Error: {ex.Message}");
            }
        }

        private async Task<MCPClient?> GetOrCreateConnectionAsync(string serverName, string stdioCommand)
        {
            lock (_poolLock)
            {
                if (_connectionPool.TryGetValue(serverName, out var existingClient))
                {
                    if (existingClient.IsConnected)
                    {
                        PluginLog.Info($"MCPPromptExecutor: Reusing connection to {serverName}");
                        // Note: CallToolAsync and ListToolsAsync will check initialization if needed
                        return existingClient;
                    }
                    else
                    {
                        // Remove dead connection
                        PluginLog.Warning($"MCPPromptExecutor: Removing dead connection to {serverName}");
                        existingClient.Dispose();
                        _connectionPool.Remove(serverName);
                    }
                }
            }

            // Create new connection (outside lock to avoid blocking)
            PluginLog.Info($"MCPPromptExecutor: Creating new connection to {serverName}");
            var newClient = new MCPClient(serverName, stdioCommand);

            if (!newClient.IsConnected)
            {
                newClient.Dispose();
                return null;
            }

            // Initialize the MCP connection
            try
            {
                var initialized = await newClient.InitializeAsync();
                if (!initialized)
                {
                    PluginLog.Error($"MCPPromptExecutor: Failed to initialize connection to {serverName}");
                    newClient.Dispose();
                    return null;
                }

                PluginLog.Info($"MCPPromptExecutor: Successfully initialized connection to {serverName}");

                lock (_poolLock)
                {
                    _connectionPool[serverName] = newClient;
                }

                return newClient;
            }
            catch (Exception ex)
            {
                PluginLog.Error($"MCPPromptExecutor: Error initializing connection to {serverName}: {ex.Message}");
                newClient.Dispose();
                return null;
            }
        }

        private async Task<MCPServerData?> GetServerDataAsync(string serverName)
        {
            // Try to find the server in the mcp_cache
            var cmd = GetConnection().CreateCommand();
            cmd.CommandText = @"
                SELECT server_json, connection_type, stdio_command, sse_url, server_name
                FROM mcp_cache
                WHERE server_name = $serverName OR LOWER(server_name) = LOWER($serverName)
                LIMIT 1
            ";
            cmd.Parameters.AddWithValue("$serverName", serverName);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var serverJson = reader.GetString(0);
                var serverData = JsonSerializer.Deserialize<MCPServerData>(serverJson);

                if (serverData != null)
                {
                    // Update with connection info from cache columns (prefer database columns over JSON)
                    if (!reader.IsDBNull(1))
                        serverData.ConnectionType = reader.GetString(1);
                    if (!reader.IsDBNull(2))
                        serverData.StdioCommand = reader.GetString(2);
                    if (!reader.IsDBNull(3))
                        serverData.SseUrl = reader.GetString(3);

                    // Ensure we have connection info - if not in database columns, use JSON values
                    if (string.IsNullOrEmpty(serverData.ConnectionType))
                        serverData.ConnectionType = "stdio"; // Default to stdio
                    if (string.IsNullOrEmpty(serverData.StdioCommand))
                    {
                        // Try to build stdio command from package name if available
                        if (!string.IsNullOrEmpty(serverData.PackageName))
                        {
                            serverData.StdioCommand = BuildStdioCommand(serverData.PackageName);
                        }
                        else if (!string.IsNullOrEmpty(serverData.ServerName))
                        {
                            // Fallback: try to build from server name
                            serverData.StdioCommand = BuildStdioCommand(serverData.ServerName);
                        }
                    }

                    PluginLog.Info($"MCPPromptExecutor: Retrieved server data for {serverData.ServerName}, stdio: {serverData.StdioCommand ?? "null"}");
                    return serverData;
                }
            }

            PluginLog.Warning($"MCPPromptExecutor: Server '{serverName}' not found in cache");
            return null;
        }

        private string BuildStdioCommand(string packageName)
        {
            // All MCP servers follow the npx pattern
            return $"npx {packageName}";
        }

        private Microsoft.Data.Sqlite.SqliteConnection GetConnection()
        {
            var connectionField = typeof(AppDatabase).GetField("_connection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (connectionField == null)
                throw new InvalidOperationException("Could not access database connection");

            var connection = connectionField.GetValue(_database) as Microsoft.Data.Sqlite.SqliteConnection;
            if (connection == null)
                throw new InvalidOperationException("Database connection is null");

            return connection;
        }

        private (string ToolName, Dictionary<string, object>? Arguments)? ParseToolCall(string responseText)
        {
            try
            {
                // Clean up markdown code blocks
                var cleaned = responseText.Trim();
                if (cleaned.StartsWith("```json"))
                    cleaned = cleaned.Substring(7);
                else if (cleaned.StartsWith("```"))
                    cleaned = cleaned.Substring(3);

                if (cleaned.EndsWith("```"))
                    cleaned = cleaned.Substring(0, cleaned.Length - 3);

                cleaned = cleaned.Trim();

                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cleaned);
                if (parsed == null) return null;

                var toolName = parsed.ContainsKey("tool") ? parsed["tool"].GetString() : null;
                if (string.IsNullOrEmpty(toolName)) return null;

                Dictionary<string, object>? arguments = null;
                if (parsed.ContainsKey("arguments"))
                {
                    var argsElement = parsed["arguments"];
                    arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argsElement.GetRawText());
                }

                return (toolName, arguments);
            }
            catch (Exception ex)
            {
                PluginLog.Error($"MCPPromptExecutor: Failed to parse tool call: {ex.Message}");
                return null;
            }
        }

        private string TruncateResult(string result, int maxLength = 100)
        {
            if (result.Length <= maxLength) return result;
            return result.Substring(0, maxLength) + "...";
        }

        private void ShowNotification(string message)
        {
            PluginLog.Info($"[NOTIFICATION] {message}");
        }

        public static void Cleanup()
        {
            lock (_poolLock)
            {
                foreach (var client in _connectionPool.Values)
                {
                    client.Dispose();
                }
                _connectionPool.Clear();
            }
        }
    }
}
