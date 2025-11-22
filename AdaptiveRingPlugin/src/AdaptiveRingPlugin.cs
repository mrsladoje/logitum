namespace Loupedeck.AdaptiveRingPlugin
{
    using System;
    using System.IO;
    using Loupedeck.AdaptiveRingPlugin.Services;

    // This class contains the plugin-level logic of the Loupedeck plugin.

    public class AdaptiveRingPlugin : Plugin
    {
        private ProcessMonitor _processMonitor = null!;
        private AppDatabase _database = null!;
        private MCPRegistryClient _mcpClient = null!;

        // Gets a value indicating whether this is an API-only plugin.
        public override Boolean UsesApplicationApiOnly => true;

        // Gets a value indicating whether this is a Universal plugin or an Application plugin.
        public override Boolean HasNoApplication => true;

        // Initializes a new instance of the plugin class.
        public AdaptiveRingPlugin()
        {
            // Initialize the plugin log.
            PluginLog.Init(this.Log);

            // Initialize the plugin resources.
            PluginResources.Init(this.Assembly);
        }

        // This method is called when the plugin is loaded.
        public override void Load()
        {
            PluginLog.Info("============================================");
            PluginLog.Info("🎯 AdaptiveRing Plugin Loading...");
            PluginLog.Info("============================================");

            // Initialize database
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Logitum",
                "adaptive_ring.db"
            );

            // Ensure directory exists
            var dbDir = Path.GetDirectoryName(dbPath);
            if (dbDir != null && !Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

            PluginLog.Info($"Database path: {dbPath}");
            _database = new AppDatabase(dbPath);
            PluginLog.Info("✅ Database initialized");

            // Initialize MCP Registry Client
            _mcpClient = new MCPRegistryClient(_database);
            PluginLog.Info("✅ MCP Registry Client initialized");

            // Download ToolSDK index asynchronously (don't block plugin loading)
            _ = InitializeToolSDKIndexAsync();

            // Initialize the process monitor
            _processMonitor = new ProcessMonitor();
            _processMonitor.AppSwitched += OnAppSwitched;
            _processMonitor.Start();

            PluginLog.Info("✅ ProcessMonitor started successfully");
            PluginLog.Info("🚀 AdaptiveRing Plugin is now active!");
        }

        // This method is called when the plugin is unloaded.
        public override void Unload()
        {
            PluginLog.Info("🛑 AdaptiveRing Plugin unloading...");

            // Stop and dispose the process monitor
            if (_processMonitor != null)
            {
                _processMonitor.AppSwitched -= OnAppSwitched;
                _processMonitor.Stop();
                _processMonitor.Dispose();
                _processMonitor = null!;
            }

            // Dispose database
            if (_database != null)
            {
                _database.Dispose();
                _database = null!;
            }

            PluginLog.Info("👋 AdaptiveRing Plugin unloaded");
        }

        private async System.Threading.Tasks.Task InitializeToolSDKIndexAsync()
        {
            try
            {
                await _mcpClient.InitializeToolSDKIndexAsync();
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Failed to initialize ToolSDK index: {ex.Message}");
            }
        }

        private async void OnAppSwitched(object? sender, AppSwitchedEventArgs e)
        {
            PluginLog.Info($"📱 App Switch Detected: {e.ProcessName}");
            PluginLog.Info($"   Window: {e.WindowTitle}");
            PluginLog.Info($"   PID: {e.ProcessId}");

            try
            {
                // Query MCP registries for this app
                var mcpServer = await _mcpClient.FindServerAsync(e.ProcessName);

                if (mcpServer != null)
                {
                    PluginLog.Info($"✅ MCP Server Found!");
                    PluginLog.Info($"   Name: {mcpServer.ServerName}");
                    PluginLog.Info($"   Package: {mcpServer.PackageName}");
                    PluginLog.Info($"   Registry: {mcpServer.RegistrySource}");
                    PluginLog.Info($"   Category: {mcpServer.Category}");
                    PluginLog.Info($"   Validated: {mcpServer.Validated}");

                    if (mcpServer.Tools != null && mcpServer.Tools.Count > 0)
                    {
                        PluginLog.Info($"   Tools: {mcpServer.Tools.Count} available");
                        foreach (var tool in mcpServer.Tools.Take(5))
                        {
                            PluginLog.Info($"     - {tool.Key}: {tool.Value.Description}");
                        }
                    }

                    // TODO: Update Actions Ring with MCP tools
                }
                else
                {
                    PluginLog.Info($"❌ No MCP server found for {e.ProcessName}");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Error querying MCP registries: {ex.Message}");
            }
        }
    }
}
