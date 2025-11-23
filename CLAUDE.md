# Logitum Adaptive Ring Plugin

Logi Plugin SDK plugin for adaptive ring functionality on Logitech devices with MCP (Model Context Protocol) integration.

## Features

- **Multi-Registry MCP Support**: Queries 3 major MCP registries (ToolSDK, Official, Glama) with 4500+ total servers
- **Smart Server Selection**: Intelligent ranking system selects the most general/relevant MCP server when multiple matches exist
- **Smart Caching**: Local SQLite database caches registry queries for instant lookups
- **Process Monitoring**: Detects app switches and automatically queries for relevant MCP servers
- **Actions Ring Management**: Dynamically updates Actions Ring with app-specific actions (keybinds, MCP prompts, Python scripts)
- **AI-Powered Suggestions**: Gemini AI suggests relevant workflows and actions based on app context and available MCP servers
- **MCP Tool Execution**: Executes MCP prompts, keybinds, and Python scripts through the Actions Ring
- **Usage Pattern Learning**: 5-layer intelligent system tracks UI interactions, processes workflows, clusters patterns, and ranks actions by frequency
- **Vector Clustering**: DBSCAN clustering of semantic workflows using VoyageAI embeddings for pattern discovery
- **Action Ranking**: Composite scoring algorithm (frequency + recency) automatically reorders actions by usage
- **Massive Coverage**: Supports developer tools, enterprise apps, databases, cloud platforms, and more
- **Zero Build Warnings**: Fully nullable reference type compliant

## Structure

- `AdaptiveRingPlugin/` - Main plugin implementation
  - `src/Models/` - Data models (MCP servers, actions, workflows, UI interactions)
  - `src/Services/` - Core services (Database, Registry Client, Process Monitor, Actions Ring Manager, AI services)
  - `src/Actions/` - Action implementations (AdaptiveRingCommand, CounterCommand, etc.)
  - `src/Helpers/` - Utility helpers (logging, resources, sanitization)
  - `src/Scripts/` - Python intelligence service for Gemini AI integration
- `DbCleaner/` - Utility for database maintenance

## Development

Build (run from `AdaptiveRingPlugin/src/` directory to avoid duplicate assembly errors):
```bash
cd AdaptiveRingPlugin/src
dotnet build
```

Or specify the full path:
```bash
dotnet build AdaptiveRingPlugin/src/AdaptiveRingPlugin.csproj
```

Clean:
```bash
cd AdaptiveRingPlugin/src
dotnet clean
```

## Implementation Details

### MCP Registry Integration

The plugin queries 3 MCP registries in cascading order:

1. **ToolSDK** (4109+ servers) - Local index downloaded on first run
   - API: `https://toolsdk-ai.github.io/toolsdk-mcp-registry/indexes/packages-list.json`
   - Cached locally for 7 days
   - Returns up to 10 matches for ranking

2. **Official MCP Registry** (380+ servers)
   - API: `https://registry.modelcontextprotocol.io/v0/servers`
   - Real-time search API
   - Collects all matches across search variants

3. **Glama** (aggregated sources)
   - API: `https://glama.ai/api/mcp/v1/servers`
   - Community-driven registry
   - Aggregates matches from multiple search terms

### Smart Server Selection

When multiple MCP servers match a query, the system ranks them by generality:

**Scoring Algorithm:**
- Exact match: +1000 points (e.g., "vscode" matches "vscode")
- Starts with search term: +700 points (e.g., "vscode-extension")
- Ends with search term: +600 points (e.g., "microsoft-vscode")
- Contains search term: +300 points
- Validated package: +200 points
- Feature-specific keywords: -200 points per keyword (api, extension, plugin, manager, etc.)
- Extra words after search term: -50 points per word
- Length penalty: -2 points per character over 8
- Special characters: -10 points each (-, _, .)
- Version suffixes: -50 points

**Example:** For "chrome", the system prefers "chrome" over "chrome-google-search-api" or "puppeteer-chrome-extension"

### Database Schema

SQLite database at: `C:\Users\panonit\AppData\Local\Logitum\adaptive_ring.db`

**Core Tables:**
- `remembered_apps` - Tracked applications
- `mcp_cache` - Caches MCP lookup results (7-day TTL)
- `toolsdk_index` - Local copy of ToolSDK registry
- `app_actions` - Actions for each app (keybinds, prompts, Python scripts) with usage tracking
- `ui_interactions` - Raw UI interaction events (24-hour TTL)
- `semantic_workflows` - AI-processed workflow descriptions
- `workflow_embeddings` - Vector embeddings for workflows (VoyageAI)
- `workflow_clusters` - Clustered workflow patterns with frequency counts

### Key Files

**Core Plugin:**
- `AdaptiveRingPlugin.cs` - Main plugin logic with service orchestration

**Services:**
- `Services/ProcessMonitor.cs` - Detects app switches using Win32 API
- `Services/AppDatabase.cs` - SQLite database with caching, indexing, and migrations
- `Services/MCPRegistryClient.cs` - Multi-registry query client with smart ranking
- `Services/ActionsRingManager.cs` - Manages Actions Ring UI and updates
- `Services/ActionPersistenceService.cs` - Persists and tracks action usage
- `Services/GeminiActionSuggestor.cs` - AI-powered action suggestions via Python service
- `Services/MCPPromptExecutor.cs` - Executes MCP prompts and manages connections
- `Services/MCPClient.cs` - MCP protocol client implementation
- `Services/MCPConnectionManager.cs` - Manages MCP server connections
- `Services/UIInteractionMonitor.cs` - Tracks UI interactions via Windows UI Automation
- `Services/SemanticWorkflowProcessor.cs` - Processes raw interactions into semantic workflows
- `Services/VoyageAIClient.cs` - Generates vector embeddings for workflows
- `Services/VectorClusteringService.cs` - DBSCAN clustering of workflow patterns
- `Services/ActionRankingService.cs` - Ranks actions by frequency and recency
- `Services/KeybindExecutor.cs` - Executes keyboard shortcuts
- `Services/PythonScriptExecutor.cs` - Executes Python script actions

**Actions:**
- `Actions/AdaptiveRingCommand.cs` - Main command handler with usage tracking
- `Actions/CounterCommand.cs` - Counter adjustment actions
- `Actions/CounterAdjustment.cs` - Counter command implementation

**Models:**
- `Models/MCPServerData.cs` - MCP server data models
- `Models/ActionData/` - Action data models (KeybindActionData, PromptActionData, PythonActionData)
- `Models/UIInteraction.cs` - UI interaction event models
- `Models/SemanticWorkflow.cs` - Workflow data models
- `Models/WorkflowCluster.cs` - Workflow cluster models

**Helpers:**
- `Helpers/PluginLog.cs` - Logging helper (nullable-compliant)
- `Helpers/PluginResources.cs` - Resource management helper
- `Helpers/ActionNameSanitizer.cs` - Sanitizes action names for display

**Scripts:**
- `Scripts/IntelligenceService.py` - Python service for Gemini AI integration
- `Scripts/requirements.txt` - Python dependencies

### Code Quality

- **Nullable Reference Types**: Enabled and fully compliant (0 warnings)
- **Build Status**: Clean build with 0 warnings, 0 errors
- **C# Version**: .NET 8.0 with implicit usings

## Deployment

Plugin deployed to: `C:\Users\panonit\AppData\Local\Logi\LogiPluginService\Plugins`

## Logs

Plugin logs available at: `C:\Users\panonit\AppData\Local\Logi\LogiPluginService\Logs\plugin_logs\AdaptiveRing.log`

## Intelligent Usage Pattern Learning System

The plugin includes a comprehensive 5-layer system for learning and adapting to user behavior:

### Layer 1: UI Interaction Capture
- **Service**: `UIInteractionMonitor.cs`
- Monitors user interactions using Windows UI Automation API
- Captures app-scoped interactions (e.g., `"chrome.exe: button Home"`)
- Stores in `ui_interactions` table with 24-hour TTL
- Automatic cleanup every 5 minutes

### Layer 2: Semantic Processing
- **Service**: `SemanticWorkflowProcessor.cs`
- Timer-based processing every 15 minutes
- Groups raw interactions by app
- Sends to Gemini AI to identify workflows
- Example: `"chrome.exe: user logs in to gmail"`
- Parallel processing across multiple apps

### Layer 3: Vector Embedding & Clustering
- **Services**: `VoyageAIClient.cs`, `VectorClusteringService.cs`
- Model: `voyage-3` (1024 dimensions)
- DBSCAN clustering with cosine similarity (ε=0.3, minPoints=2)
- Per-app clustering isolation
- Stores embeddings in `workflow_embeddings` table

### Layer 4: Frequency Tracking & Ranking
- **Service**: `ActionRankingService.cs`
- Composite scoring: `(frequency * 0.6) + (recency * 0.4)`
- Per-app ranking
- Converts high-frequency workflows to MCP prompts
- Reorders existing actions by usage

### Layer 5: Integration & Orchestration
- All services initialized in `AdaptiveRingPlugin.cs`
- Graceful degradation if API keys not configured
- Background tasks run automatically

## Configuration

### Environment Variables (Required for Semantic Features)
```bash
GEMINI_API_KEY=your_gemini_api_key
VOYAGEAI_API_KEY=your_voyageai_api_key
MILVUS_URI=your_milvus_uri  # Optional (SQLite fallback active)
MILVUS_TOKEN=your_milvus_token  # Optional
```

### Action Types

The plugin supports three types of actions:

1. **Keybind Actions**: Execute keyboard shortcuts
   - Data model: `KeybindActionData`
   - Executor: `KeybindExecutor.cs`

2. **MCP Prompt Actions**: Execute MCP tool prompts
   - Data model: `PromptActionData`
   - Executor: `MCPPromptExecutor.cs`
   - Manages MCP server connections automatically

3. **Python Script Actions**: Execute Python scripts
   - Data model: `PythonActionData`
   - Executor: `PythonScriptExecutor.cs`

## Background Tasks

1. **UI Interaction Cleanup** - Timer every 5 minutes
2. **Semantic Workflow Processing** - Timer every 15 minutes (first run after 1 minute)
3. **UI Interaction Monitor** - Event-driven, runs continuously
4. **ToolSDK Index Download** - Async on plugin load (7-day cache)

## Usage Tracking

Every action execution is automatically tracked:
- `usage_count` incremented
- `last_used_at` updated
- Actions reordered by composite score (frequency + recency)

## Known Limitations

1. **Milvus Integration**: `VectorDatabase.cs` is a stub implementation (SQLite fallback active)
2. **UI Automation**: Simplified implementation using Win32 API
3. **Embedding Storage**: Currently stored as JSON in SQLite (can be optimized to BLOB)
4. **Cluster Naming**: Auto-generated labels (no semantic naming yet)
