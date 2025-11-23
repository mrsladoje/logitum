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

        // New services for semantic workflow processing
        private UIInteractionMonitor? _uiInteractionMonitor;
        private SemanticWorkflowProcessor? _semanticWorkflowProcessor;
        private VoyageAIClient? _voyageClient;
        private VectorClusteringService? _vectorClusteringService;
        private ActionRankingService? _actionRankingService;

        public ActionsRingManager ActionsRingManager { get; private set; } = null!;
        public MCPPromptExecutor McpPromptExecutor => _mcpPromptExecutor;
        public ActionPersistenceService ActionPersistenceService => _persistenceService;

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

            // Initialize semantic workflow processing services (optional - only if API keys are configured)
            InitializeSemanticWorkflowServices();

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

        private void InitializeSemanticWorkflowServices()
        {
            try
            {
                // Load API keys from environment variables
                var voyageApiKey = Environment.GetEnvironmentVariable("VOYAGEAI_API_KEY");

                if (string.IsNullOrEmpty(voyageApiKey))
                {
                    PluginLog.Info("⚠️ VoyageAI API key not configured - semantic workflow features disabled");
                    return;
                }

                // Initialize VoyageAI client
                _voyageClient = new VoyageAIClient(voyageApiKey);
                PluginLog.Info("✅ VoyageAI Client initialized");

                // Initialize Vector Clustering Service
                _vectorClusteringService = new VectorClusteringService(_database);
                PluginLog.Info("✅ Vector Clustering Service initialized");

                // Initialize Action Ranking Service
                _actionRankingService = new ActionRankingService(_database, _voyageClient);
                PluginLog.Info("✅ Action Ranking Service initialized");

                // Initialize UI Interaction Monitor
                _uiInteractionMonitor = new UIInteractionMonitor(_database, _processMonitor);
                _uiInteractionMonitor.Start();
                PluginLog.Info("✅ UI Interaction Monitor started");

                // Initialize Semantic Workflow Processor
                _semanticWorkflowProcessor = new SemanticWorkflowProcessor(
                    _database,
                    _geminiSuggestor,
                    _voyageClient,
                    _vectorClusteringService
                );
                _semanticWorkflowProcessor.Start();
                PluginLog.Info("✅ Semantic Workflow Processor started");

                PluginLog.Info("🧠 Semantic workflow processing enabled");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"Failed to initialize semantic workflow services: {ex.Message}");
                PluginLog.Info("⚠️ Semantic workflow features disabled due to initialization error");
            }
        }

        // This method is called when the plugin is unloaded.
        public override void Unload()
        {
            PluginLog.Info("🛑 AdaptiveRing Plugin unloading...");

            // Stop and dispose semantic workflow services
            if (_semanticWorkflowProcessor != null)
            {
                _semanticWorkflowProcessor.Stop();
                _semanticWorkflowProcessor.Dispose();
                _semanticWorkflowProcessor = null;
            }

            if (_uiInteractionMonitor != null)
            {
                _uiInteractionMonitor.Stop();
                _uiInteractionMonitor.Dispose();
                _uiInteractionMonitor = null;
            }

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
