namespace Loupedeck.AdaptiveRingPlugin.Services;

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// MCP (Model Context Protocol) client for communicating with MCP servers via stdio.
/// Implements JSON-RPC 2.0 protocol over standard input/output.
/// </summary>
public class MCPClient : IDisposable
{
    private readonly Process? _process;
    private readonly StreamWriter? _stdin;
    private readonly StreamReader? _stdout;
    private int _requestId = 0;
    private readonly object _lock = new object();
    private bool _initialized = false;

    public bool IsConnected { get; private set; }
    public string ServerName { get; }

    public MCPClient(string serverName, string stdioCommand)
    {
        ServerName = serverName;

        try
        {
            // Parse command (e.g., "npx @modelcontextprotocol/server-vscode")
            var parts = stdioCommand.Split(' ', 2);
            var executable = parts[0]; // "npx"
            var arguments = parts.Length > 1 ? parts[1] : ""; // "@modelcontextprotocol/server-vscode"

            PluginLog.Info($"MCPClient: Starting MCP server: {executable} {arguments}");

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardInputEncoding = Encoding.UTF8
                }
            };

            _process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    PluginLog.Warning($"MCPClient [{ServerName}] stderr: {e.Data}");
                }
            };

            _process.Start();
            _process.BeginErrorReadLine();

            _stdin = _process.StandardInput;
            _stdout = _process.StandardOutput;

            // Check if process is still running after a brief moment
            Task.Delay(500).Wait();
            if (_process.HasExited)
            {
                PluginLog.Error($"MCPClient: Process exited immediately with code {_process.ExitCode}");
                IsConnected = false;
                return;
            }

            IsConnected = true;
            PluginLog.Info($"MCPClient: Connected to {serverName}");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"MCPClient: Failed to start MCP server '{serverName}': {ex.Message}");
            IsConnected = false;
        }
    }

    /// <summary>
    /// Initialize the MCP server with client info.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        if (_initialized) return true;
        if (!IsConnected || _stdin == null || _stdout == null) return false;

        try
        {
            PluginLog.Info($"MCPClient: Initializing connection to {ServerName}");

            var initRequest = new
            {
                jsonrpc = "2.0",
                id = GetNextRequestId(),
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "AdaptiveRingPlugin",
                        version = "1.0.0"
                    }
                }
            };

            PluginLog.Verbose($"MCPClient: Sending initialize request");
            var response = await SendRequestAsync<JsonElement>(initRequest);
            PluginLog.Info($"MCPClient: Received initialize response");

            _initialized = true;

            if (_initialized)
            {
                // Send initialized notification
                await SendNotificationAsync("notifications/initialized", new { });
                PluginLog.Info($"MCPClient: Successfully initialized connection to {ServerName}");
            }

            return _initialized;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"MCPClient: Initialization failed: {ex.Message}");
            IsConnected = false; // Mark as disconnected on failure
            return false;
        }
    }

    /// <summary>
    /// List available tools from the MCP server.
    /// </summary>
    public async Task<List<MCPTool>> ListToolsAsync()
    {
        if (!_initialized && !await InitializeAsync())
        {
            return new List<MCPTool>();
        }

        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = GetNextRequestId(),
                method = "tools/list",
                @params = new { }
            };

            var response = await SendRequestAsync<ToolsListResponse>(request);

            if (response?.Tools != null)
            {
                PluginLog.Info($"MCPClient: Found {response.Tools.Count} tools from {ServerName}");
                return response.Tools;
            }

            return new List<MCPTool>();
        }
        catch (Exception ex)
        {
            PluginLog.Error($"MCPClient: Failed to list tools: {ex.Message}");
            return new List<MCPTool>();
        }
    }

    /// <summary>
    /// Call a tool on the MCP server.
    /// </summary>
    public async Task<MCPToolResult?> CallToolAsync(string toolName, Dictionary<string, object>? arguments = null)
    {
        if (!_initialized && !await InitializeAsync())
        {
            return null;
        }

        try
        {
            PluginLog.Info($"MCPClient: Calling tool '{toolName}' with {arguments?.Count ?? 0} arguments");

            var request = new
            {
                jsonrpc = "2.0",
                id = GetNextRequestId(),
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = arguments ?? new Dictionary<string, object>()
                }
            };

            var response = await SendRequestAsync<ToolCallResponse>(request);

            if (response?.Content != null && response.Content.Count > 0)
            {
                var firstContent = response.Content[0];
                PluginLog.Info($"MCPClient: Tool '{toolName}' returned {response.Content.Count} content items");

                return new MCPToolResult
                {
                    ToolName = toolName,
                    Content = firstContent.Text ?? JsonSerializer.Serialize(firstContent),
                    IsError = response.IsError ?? false
                };
            }

            PluginLog.Warning($"MCPClient: Tool '{toolName}' returned no content");
            return null;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"MCPClient: Tool call failed: {ex.Message}");
            return new MCPToolResult
            {
                ToolName = toolName,
                Content = $"Error: {ex.Message}",
                IsError = true
            };
        }
    }

    private async Task<T?> SendRequestAsync<T>(object request)
    {
        if (_stdin == null || _stdout == null || !IsConnected)
        {
            throw new InvalidOperationException("MCP client not connected");
        }

        try
        {
            var requestJson = JsonSerializer.Serialize(request);

            lock (_lock)
            {
                _stdin.WriteLine(requestJson);
                _stdin.Flush();
            }

            PluginLog.Verbose($"MCPClient: Sent request: {requestJson}");

            // Read response (timeout after 30 seconds)
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var responseTask = _stdout.ReadLineAsync();

            if (await Task.WhenAny(responseTask, Task.Delay(TimeSpan.FromSeconds(30), cts.Token)) == responseTask)
            {
                var responseLine = await responseTask;

                if (string.IsNullOrEmpty(responseLine))
                {
                    throw new InvalidOperationException("MCP server returned empty response");
                }

                // Trim whitespace that might be present
                responseLine = responseLine.Trim();
                PluginLog.Verbose($"MCPClient: Received response: {responseLine}");

                var response = JsonSerializer.Deserialize<JsonRpcResponse<T>>(responseLine);

                if (response?.Error != null)
                {
                    throw new InvalidOperationException($"MCP error: {response.Error.Message}");
                }

                return response!.Result;
            }
            else
            {
                throw new TimeoutException("MCP server response timeout");
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"MCPClient: Request failed: {ex.Message}");
            throw;
        }
    }

    private async Task SendNotificationAsync(string method, object parameters)
    {
        if (_stdin == null || !IsConnected) return;

        try
        {
            var notification = new
            {
                jsonrpc = "2.0",
                method = method,
                @params = parameters
            };

            var notificationJson = JsonSerializer.Serialize(notification);

            lock (_lock)
            {
                _stdin.WriteLine(notificationJson);
                _stdin.Flush();
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"MCPClient: Notification failed: {ex.Message}");
        }
    }

    private int GetNextRequestId()
    {
        lock (_lock)
        {
            return ++_requestId;
        }
    }

    public void Dispose()
    {
        try
        {
            IsConnected = false;

            _stdin?.Close();
            _stdout?.Close();

            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(1000);
            }

            _process?.Dispose();

            PluginLog.Info($"MCPClient: Disconnected from {ServerName}");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"MCPClient: Error during disposal: {ex.Message}");
        }
    }
}

// JSON-RPC response wrapper
public class JsonRpcResponse<T>
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}

public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
}

// MCP-specific response types
public class ToolsListResponse
{
    [JsonPropertyName("tools")]
    public List<MCPTool> Tools { get; set; } = new();
}

public class MCPTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("inputSchema")]
    public JsonElement? InputSchema { get; set; }
}

public class ToolCallResponse
{
    [JsonPropertyName("content")]
    public List<ContentItem> Content { get; set; } = new();

    [JsonPropertyName("isError")]
    public bool? IsError { get; set; }
}

public class ContentItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class MCPToolResult
{
    public string ToolName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsError { get; set; }
}
