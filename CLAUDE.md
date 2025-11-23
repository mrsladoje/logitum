# Logitum Adaptive Ring Plugin

Logi Plugin SDK plugin for adaptive ring functionality on Logitech devices with MCP (Model Context Protocol) integration.

## Features

- **Multi-Registry MCP Support**: Queries 3 major MCP registries (ToolSDK, Official, Glama) with 4500+ total servers
- **Smart Server Selection**: Intelligent ranking system selects the most general/relevant MCP server when multiple matches exist
- **Smart Caching**: Local SQLite database caches registry queries for instant lookups
- **Process Monitoring**: Detects app switches and automatically queries for relevant MCP servers
- **Massive Coverage**: Supports developer tools, enterprise apps, databases, cloud platforms, and more
- **Zero Build Warnings**: Fully nullable reference type compliant

## Structure

- `AdaptiveRingPlugin/` - Main plugin implementation
  - `src/Models/` - Data models for MCP servers
  - `src/Services/` - Core services (Database, Registry Client, Process Monitor)
- `ReferencePlugin/` - Example reference plugin

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

**Tables:**
- `mcp_cache` - Caches MCP lookup results (7-day TTL)
- `toolsdk_index` - Local copy of ToolSDK registry

### Key Files

- `AdaptiveRingPlugin.cs` - Main plugin logic with MCP integration
- `Services/ProcessMonitor.cs` - Detects app switches using Win32 API
- `Services/AppDatabase.cs` - SQLite database with caching and indexing
- `Services/MCPRegistryClient.cs` - Multi-registry query client with smart ranking
- `Models/MCPServerData.cs` - Data models for MCP servers
- `Helpers/PluginLog.cs` - Logging helper (nullable-compliant)
- `Helpers/PluginResources.cs` - Resource management helper
- `package/plugin.json` - Plugin metadata

### Code Quality

- **Nullable Reference Types**: Enabled and fully compliant (0 warnings)
- **Build Status**: Clean build with 0 warnings, 0 errors
- **C# Version**: .NET 8.0 with implicit usings

## Deployment

Plugin deployed to: `C:\Users\panonit\AppData\Local\Logi\LogiPluginService\Plugins`

## Logs

Plugin logs available at: `C:\Users\panonit\AppData\Local\Logi\LogiPluginService\Logs\plugin_logs\AdaptiveRing.log`

## Next Steps

- Update Actions Ring with discovered MCP tools
- Implement AI-suggested workflow actions
- Add user preference learning
