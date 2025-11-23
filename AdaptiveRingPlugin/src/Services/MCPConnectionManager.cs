namespace Loupedeck.AdaptiveRingPlugin.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Loupedeck.AdaptiveRingPlugin.Models;

    public enum MCPConnectionType
    {
        Unknown,
        Stdio,
        SSE
    }

    public class MCPConnection
    {
        public string ServerName { get; set; } = string.Empty;
        public MCPConnectionType Type { get; set; }
        public Process? StdioProcess { get; set; }
        public string? SseUrl { get; set; }
        public bool IsConnected { get; set; }
    }

    public class MCPConnectionManager : IDisposable
    {
        private readonly Dictionary<string, MCPConnection> _connections = new();

        public MCPConnection? GetOrCreateConnection(MCPServerData serverData)
        {
            if (_connections.TryGetValue(serverData.ServerName, out var existing))
            {
                if (existing.IsConnected)
                    return existing;
            }

            // TODO: Implement actual connection logic
            // For now, return null
            PluginLog.Info($"MCPConnectionManager: Would connect to {serverData.ServerName}");
            return null;
        }

        public void Dispose()
        {
            foreach (var connection in _connections.Values)
            {
                if (connection.StdioProcess != null)
                {
                    try
                    {
                        connection.StdioProcess.Kill();
                        connection.StdioProcess.Dispose();
                    }
                    catch { }
                }
            }
            _connections.Clear();
        }
    }
}
