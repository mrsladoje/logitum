# Logitum Adaptive Ring Plugin

Logi Plugin SDK plugin for adaptive ring functionality on Logitech devices with MCP (Model Context Protocol) integration.

## Features

- **Multi-Registry MCP Support**: Queries 3 major MCP registries (ToolSDK, Official, Glama) with 4500+ total servers
- **Smart Caching**: Local SQLite database caches registry queries for instant lookups
- **Process Monitoring**: Detects app switches and automatically queries for relevant MCP servers
- **Massive Coverage**: Supports developer tools, enterprise apps, databases, cloud platforms, and more

## Structure

- `AdaptiveRingPlugin/` - Main plugin implementation
  - `src/Models/` - Data models for MCP servers
  - `src/Services/` - Core services (Database, Registry Client, Process Monitor)
- `ReferencePlugin/` - Example reference plugin

## Development

Build:
```bash
dotnet build
```

Run:
```bash
dotnet run --project AdaptiveRingPlugin
```

## Implementation Details

### MCP Registry Integration

The plugin queries 3 MCP registries in cascading order:

1. **ToolSDK** (4109+ servers) - Local index downloaded on first run
   - API: `https://toolsdk-ai.github.io/toolsdk-mcp-registry/indexes/packages-list.json`
   - Cached locally for 7 days

2. **Official MCP Registry** (380+ servers)
   - API: `https://registry.modelcontextprotocol.io/v0/servers`
   - Real-time search API

3. **Glama** (aggregated sources)
   - API: `https://glama.ai/api/mcp/v1/servers`
   - Community-driven registry

### Database Schema

SQLite database at: `C:\Users\panonit\AppData\Local\Logitum\adaptive_ring.db`

**Tables:**
- `mcp_cache` - Caches MCP lookup results (7-day TTL)
- `toolsdk_index` - Local copy of ToolSDK registry

### Key Files

- `AdaptiveRingPlugin.cs` - Main plugin logic with MCP integration
- `Services/ProcessMonitor.cs` - Detects app switches
- `Services/AppDatabase.cs` - SQLite database management
- `Services/MCPRegistryClient.cs` - Multi-registry query client
- `Models/MCPServerData.cs` - Data models for MCP servers
- `plugin.json` - Plugin metadata

## Deployment

Plugin deployed to: `C:\Users\panonit\AppData\Local\Logi\LogiPluginService\Plugins`

## Logs

Plugin logs available at: `C:\Users\panonit\AppData\Local\Logi\LogiPluginService\Logs\plugin_logs\AdaptiveRing.log`

## Next Steps

- Update Actions Ring with discovered MCP tools
- Implement AI-suggested workflow actions
- Add user preference learning
