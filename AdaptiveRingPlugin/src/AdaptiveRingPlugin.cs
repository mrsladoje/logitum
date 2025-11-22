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
        private ActionPersistenceService _persistenceService = null!;
        private GeminiActionSuggestor _geminiSuggestor = null!;
        private MCPPromptExecutor _mcpPromptExecutor = null!;

        public ActionsRingManager ActionsRingManager { get; private set; } = null!;
        public MCPPromptExecutor McpPromptExecutor => _mcpPromptExecutor;

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

            // Initialize Gemini Action Suggestor
            _geminiSuggestor = new GeminiActionSuggestor();
            PluginLog.Info("✅ Gemini Action Suggestor initialized");

            // Initialize Persistence Service
            _persistenceService = new ActionPersistenceService(_database);
            PluginLog.Info("✅ Action Persistence Service initialized");

            // Initialize MCP Prompt Executor
            _mcpPromptExecutor = new MCPPromptExecutor(_database);
            PluginLog.Info("✅ MCP Prompt Executor initialized");

            // Initialize Actions Ring Manager
            ActionsRingManager = new ActionsRingManager(_persistenceService, this);
            PluginLog.Info("✅ Actions Ring Manager initialized");

            // Initialize the process monitor with all services
            _processMonitor = new ProcessMonitor(
                _persistenceService,
                ActionsRingManager,
                _geminiSuggestor,
                _mcpClient
            );
            _processMonitor.Start();

            PluginLog.Info("✅ ProcessMonitor started successfully with persistence workflow");
            PluginLog.Info("🚀 AdaptiveRing Plugin is now active!");
        }

        // This method is called when the plugin is unloaded.
        public override void Unload()
        {
            PluginLog.Info("🛑 AdaptiveRing Plugin unloading...");

            // Stop and dispose the process monitor
            if (_processMonitor != null)
            {
                _processMonitor.Stop();
                _processMonitor.Dispose();
                _processMonitor = null!;
            }

            // Cleanup MCP connections
            MCPPromptExecutor.Cleanup();

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
    }
}
