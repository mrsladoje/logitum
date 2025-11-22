-- MCP Adaptive Ring Database Schema
-- SQLite database for caching MCP server data and tracking application usage
-- Database Location: %LOCALAPPDATA%\Logitum\adaptivering.db

-- =============================================================================
-- Table: apps
-- Purpose: Track which applications have been queried and their usage patterns
-- =============================================================================
CREATE TABLE IF NOT EXISTS apps (
    app_name TEXT PRIMARY KEY,              -- Process name (e.g., "chrome", "vscode")
    mcp_server_name TEXT,                   -- Primary MCP server for this app (nullable)
    last_queried DATETIME,                  -- Last time we queried MCP Registry for this app
    times_used INTEGER DEFAULT 1            -- How many times user switched to this app
);

-- Index for quick lookups by last queried time (for cache invalidation)
CREATE INDEX IF NOT EXISTS idx_apps_last_queried ON apps(last_queried);

-- =============================================================================
-- Table: mcp_servers
-- Purpose: Cache MCP server metadata from the registry
-- =============================================================================
CREATE TABLE IF NOT EXISTS mcp_servers (
    server_name TEXT PRIMARY KEY,           -- Unique server identifier (e.g., "github", "slack")
    description TEXT,                       -- Human-readable description
    version TEXT,                           -- Server version (e.g., "1.0.0")
    repository_url TEXT,                    -- GitHub repository URL
    repository_source TEXT,                 -- Source type (e.g., "github")
    website_url TEXT,                       -- Documentation/homepage URL
    status TEXT DEFAULT 'active',           -- Server status (active, deprecated, deleted)
    published_at DATETIME,                  -- When server was first published
    updated_at DATETIME,                    -- When server was last updated
    is_latest BOOLEAN DEFAULT 1,            -- Whether this is the latest version
    last_cached DATETIME DEFAULT CURRENT_TIMESTAMP,  -- When we cached this data
    schema_url TEXT                         -- JSON schema URL
);

-- Index for finding active servers
CREATE INDEX IF NOT EXISTS idx_mcp_servers_status ON mcp_servers(status);

-- Index for cache invalidation (find stale entries)
CREATE INDEX IF NOT EXISTS idx_mcp_servers_last_cached ON mcp_servers(last_cached);

-- =============================================================================
-- Table: app_server_mapping
-- Purpose: Many-to-many relationship between apps and MCP servers
-- =============================================================================
CREATE TABLE IF NOT EXISTS app_server_mapping (
    app_name TEXT NOT NULL,                 -- Application process name
    server_name TEXT NOT NULL,              -- MCP server name
    relevance_score REAL DEFAULT 1.0,       -- How relevant this server is to this app (0.0-1.0)
    discovered_at DATETIME DEFAULT CURRENT_TIMESTAMP,  -- When we discovered this mapping
    PRIMARY KEY (app_name, server_name),
    FOREIGN KEY (app_name) REFERENCES apps(app_name) ON DELETE CASCADE,
    FOREIGN KEY (server_name) REFERENCES mcp_servers(server_name) ON DELETE CASCADE
);

-- Index for quick lookup of servers for a given app
CREATE INDEX IF NOT EXISTS idx_app_server_app ON app_server_mapping(app_name);

-- Index for quick lookup of apps using a given server
CREATE INDEX IF NOT EXISTS idx_app_server_server ON app_server_mapping(server_name);

-- Index for sorting by relevance
CREATE INDEX IF NOT EXISTS idx_app_server_relevance ON app_server_mapping(relevance_score DESC);

-- =============================================================================
-- Cache TTL Configuration
-- =============================================================================
-- Cache entries are considered stale after 24 hours
-- Application code should check: datetime('now', '-24 hours') < last_cached
