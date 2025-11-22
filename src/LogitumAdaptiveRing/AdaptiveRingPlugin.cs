namespace Loupedeck.LogitumAdaptiveRing
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Loupedeck.LogitumAdaptiveRing.Services;
    using Loupedeck.LogitumAdaptiveRing.Data;

    /// <summary>
    /// MCP Adaptive Ring - Main Plugin Class
    ///
    /// This plugin integrates Logitech MX devices with the Model Context Protocol ecosystem,
    /// providing context-aware actions through intelligent discovery and adaptation.
    /// </summary>
    public class AdaptiveRingPlugin : Plugin
    {
        private const string LogTag = "[MCP-AdaptiveRing]";

        // Phase 2: Process Monitoring
        private ProcessMonitor _processMonitor;

        // Phase 3: MCP Registry Integration
        private MCPRegistryClient _mcpClient;
        private AppDatabase _database;

        // Removed UsesApplicationApiOnly and HasNoApplication - testing if these cause issues

        /// <summary>
        /// Initializes a new instance of the AdaptiveRingPlugin class.
        /// </summary>
        public AdaptiveRingPlugin()
        {
            // Initialize logging
            this.Log.Info($"{LogTag} Plugin constructor called");

            // Phase 2: Initialize process monitor
            this._processMonitor = new ProcessMonitor();
            this.Log.Info($"{LogTag} ProcessMonitor initialized");

            // Phase 3: Initialize MCP registry client and database
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Logitum", "adaptivering.db"
            );
            this._database = new AppDatabase(dbPath);
            this.Log.Info($"{LogTag} Database initialized at: {dbPath}");

            this._mcpClient = new MCPRegistryClient();
            this.Log.Info($"{LogTag} MCP Registry client initialized");

            // TODO: Phase 4 - Initialize UI automation tracker
        }

        /// <summary>
        /// Called when the plugin is loaded by Logi Plugin Service.
        /// This is where we start all our background services.
        /// </summary>
        public override void Load()
        {
            try
            {
                this.Log.Info($"{LogTag} Plugin loading...");

                // Phase 2: Start process monitor
                this._processMonitor.ApplicationChanged += this.OnApplicationChanged;
                this._processMonitor.Start();
                this.Log.Info($"{LogTag} ProcessMonitor started - monitoring active window changes");

                // TODO: Phase 2 - Start MCP registry queries
                // TODO: Phase 3 - Start UI automation tracking
                // TODO: Phase 4 - Initialize AI suggestion engine

                this.Log.Info($"{LogTag} Plugin loaded successfully ✅");
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR during plugin load: {ex.Message}");
                this.Log.Error($"{LogTag} Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Called when the plugin is unloaded.
        /// Clean up all resources here.
        /// </summary>
        public override void Unload()
        {
            try
            {
                this.Log.Info($"{LogTag} Plugin unloading...");

                // Phase 2: Stop process monitor
                if (this._processMonitor != null)
                {
                    this._processMonitor.ApplicationChanged -= this.OnApplicationChanged;
                    this._processMonitor.Stop();
                    this._processMonitor.Dispose();
                    this.Log.Info($"{LogTag} ProcessMonitor stopped and disposed");
                }

                // Phase 3: Cleanup MCP client and database
                if (this._mcpClient != null)
                {
                    this._mcpClient.Dispose();
                    this.Log.Info($"{LogTag} MCP Registry client disposed");
                }

                if (this._database != null)
                {
                    this._database.Dispose();
                    this.Log.Info($"{LogTag} Database connection disposed");
                }

                // TODO: Phase 4 - Stop UI automation tracker
                // TODO: Phase 5 - Shutdown AI services

                this.Log.Info($"{LogTag} Plugin unloaded successfully");
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR during plugin unload: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when a command is executed.
        /// </summary>
        public override void RunCommand(string commandName, string parameter)
        {
            try
            {
                this.Log.Info($"{LogTag} Command received: {commandName} with parameter: {parameter}");

                // TODO: Phase 5 - Handle MCP action execution
                // TODO: Phase 6 - Handle user preferences
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR in RunCommand: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for application/process changes.
        /// Called when the user switches to a different application.
        /// </summary>
        /// <param name="sender">The process monitor</param>
        /// <param name="info">Process information</param>
        private async void OnApplicationChanged(object sender, ProcessInfo info)
        {
            try
            {
                this.Log.Info($"{LogTag} Active app changed: {info.ProcessName}");
                this.Log.Info($"{LogTag}   Window: {info.WindowTitle}");
                this.Log.Info($"{LogTag}   Path: {info.ExecutablePath}");

                // Phase 3: Query MCP Registry for this application
                var servers = await this.QueryMCPServersForAppAsync(info.ProcessName);

                if (servers.Count > 0)
                {
                    this.Log.Info($"{LogTag} Found {servers.Count} MCP server(s) for {info.ProcessName}:");
                    foreach (var server in servers)
                    {
                        this.Log.Info($"{LogTag}   - {server.Name} v{server.Version}: {server.Description}");
                    }
                }
                else
                {
                    this.Log.Info($"{LogTag} No MCP servers found for {info.ProcessName}");
                }

                // TODO: Phase 4 - Request AI suggestions for this context
                // TODO: Phase 5 - Update Actions Ring with available actions
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR in OnApplicationChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Queries MCP servers for a given application, using cache when available.
        /// </summary>
        /// <param name="appName">Application process name.</param>
        /// <returns>List of MCP servers for this application.</returns>
        private async Task<List<MCPServer>> QueryMCPServersForAppAsync(string appName)
        {
            try
            {
                // Check cache first
                var cached = this._database.GetCachedServers(appName);
                if (cached.Count > 0)
                {
                    this.Log.Info($"{LogTag} Using cached MCP servers for {appName}");
                    return cached;
                }

                // Query MCP Registry API
                this.Log.Info($"{LogTag} Querying MCP Registry for {appName}...");
                var servers = await this._mcpClient.SearchServersAsync(appName, limit: 5);

                // Cache results
                if (servers.Count > 0)
                {
                    foreach (var server in servers)
                    {
                        this._database.CacheMCPServer(appName, server);
                    }
                    this.Log.Info($"{LogTag} Cached {servers.Count} server(s) for {appName}");
                }

                return servers;
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR querying MCP servers: {ex.Message}");
                return new List<MCPServer>();
            }
        }
    }
}
