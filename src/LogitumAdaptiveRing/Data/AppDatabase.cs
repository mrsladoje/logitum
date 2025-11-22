namespace Loupedeck.LogitumAdaptiveRing.Data
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Loupedeck.LogitumAdaptiveRing.Services;
    using Microsoft.Data.Sqlite;

    /// <summary>
    /// SQLite database wrapper for caching MCP server data and tracking application usage.
    /// Database location: %LOCALAPPDATA%\Logitum\adaptivering.db
    /// </summary>
    public class AppDatabase : IDisposable
    {
        private const int CacheTTLHours = 24;
        private readonly string _connectionString;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the AppDatabase class.
        /// </summary>
        /// <param name="dbPath">Full path to the SQLite database file.</param>
        public AppDatabase(string dbPath)
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            this._connectionString = $"Data Source={dbPath}";
            this.InitializeDatabase();
        }

        /// <summary>
        /// Creates the database schema if it doesn't exist.
        /// </summary>
        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(this._connectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    // Create apps table
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS apps (
                            app_name TEXT PRIMARY KEY,
                            mcp_server_name TEXT,
                            last_queried DATETIME,
                            times_used INTEGER DEFAULT 1
                        );

                        CREATE INDEX IF NOT EXISTS idx_apps_last_queried ON apps(last_queried);
                    ";
                    command.ExecuteNonQuery();

                    // Create mcp_servers table
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS mcp_servers (
                            server_name TEXT PRIMARY KEY,
                            description TEXT,
                            version TEXT,
                            repository_url TEXT,
                            repository_source TEXT,
                            website_url TEXT,
                            status TEXT DEFAULT 'active',
                            published_at DATETIME,
                            updated_at DATETIME,
                            is_latest BOOLEAN DEFAULT 1,
                            last_cached DATETIME DEFAULT CURRENT_TIMESTAMP,
                            schema_url TEXT
                        );

                        CREATE INDEX IF NOT EXISTS idx_mcp_servers_status ON mcp_servers(status);
                        CREATE INDEX IF NOT EXISTS idx_mcp_servers_last_cached ON mcp_servers(last_cached);
                    ";
                    command.ExecuteNonQuery();

                    // Create app_server_mapping table
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS app_server_mapping (
                            app_name TEXT NOT NULL,
                            server_name TEXT NOT NULL,
                            relevance_score REAL DEFAULT 1.0,
                            discovered_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                            PRIMARY KEY (app_name, server_name)
                        );

                        CREATE INDEX IF NOT EXISTS idx_app_server_app ON app_server_mapping(app_name);
                        CREATE INDEX IF NOT EXISTS idx_app_server_server ON app_server_mapping(server_name);
                        CREATE INDEX IF NOT EXISTS idx_app_server_relevance ON app_server_mapping(relevance_score DESC);
                    ";
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Caches an MCP server and creates the mapping to an application.
        /// </summary>
        /// <param name="appName">Application process name.</param>
        /// <param name="server">MCP server to cache.</param>
        public void CacheMCPServer(string appName, MCPServer server)
        {
            if (string.IsNullOrWhiteSpace(appName) || server == null || string.IsNullOrWhiteSpace(server.Name))
            {
                return;
            }

            using (var connection = new SqliteConnection(this._connectionString))
            {
                connection.Open();

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Insert or update mcp_servers table
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                INSERT INTO mcp_servers
                                    (server_name, description, version, repository_url, repository_source,
                                     website_url, status, published_at, updated_at, is_latest,
                                     last_cached, schema_url)
                                VALUES
                                    (@name, @desc, @version, @repo_url, @repo_source,
                                     @website, @status, @published, @updated, @latest,
                                     datetime('now'), @schema)
                                ON CONFLICT(server_name) DO UPDATE SET
                                    description = @desc,
                                    version = @version,
                                    repository_url = @repo_url,
                                    repository_source = @repo_source,
                                    website_url = @website,
                                    status = @status,
                                    published_at = @published,
                                    updated_at = @updated,
                                    is_latest = @latest,
                                    last_cached = datetime('now'),
                                    schema_url = @schema
                            ";

                            command.Parameters.AddWithValue("@name", server.Name);
                            command.Parameters.AddWithValue("@desc", server.Description ?? string.Empty);
                            command.Parameters.AddWithValue("@version", server.Version ?? string.Empty);
                            command.Parameters.AddWithValue("@repo_url", server.Repository?.Url ?? string.Empty);
                            command.Parameters.AddWithValue("@repo_source", server.Repository?.Source ?? string.Empty);
                            command.Parameters.AddWithValue("@website", server.WebsiteUrl ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@status", "active"); // Default status
                            command.Parameters.AddWithValue("@published", DateTime.UtcNow);
                            command.Parameters.AddWithValue("@updated", DateTime.UtcNow);
                            command.Parameters.AddWithValue("@latest", true);
                            command.Parameters.AddWithValue("@schema", server.Schema ?? string.Empty);

                            command.ExecuteNonQuery();
                        }

                        // Insert app_server_mapping entry
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                INSERT INTO app_server_mapping (app_name, server_name, relevance_score)
                                VALUES (@app, @server, 1.0)
                                ON CONFLICT(app_name, server_name) DO UPDATE SET
                                    relevance_score = 1.0,
                                    discovered_at = datetime('now')
                            ";

                            command.Parameters.AddWithValue("@app", appName);
                            command.Parameters.AddWithValue("@server", server.Name);

                            command.ExecuteNonQuery();
                        }

                        // Update apps table last_queried timestamp
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = @"
                                INSERT INTO apps (app_name, last_queried, times_used)
                                VALUES (@app, datetime('now'), 1)
                                ON CONFLICT(app_name) DO UPDATE SET
                                    last_queried = datetime('now'),
                                    times_used = times_used + 1
                            ";

                            command.Parameters.AddWithValue("@app", appName);
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Gets cached MCP servers for an application.
        /// Returns cached data only if it's less than 24 hours old.
        /// </summary>
        /// <param name="appName">Application process name.</param>
        /// <returns>List of cached servers, or empty list if cache is stale or no data exists.</returns>
        public List<MCPServer> GetCachedServers(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return new List<MCPServer>();
            }

            var servers = new List<MCPServer>();

            using (var connection = new SqliteConnection(this._connectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    // Get servers for this app that were cached within the last 24 hours
                    command.CommandText = @"
                        SELECT
                            s.server_name, s.description, s.version, s.repository_url,
                            s.repository_source, s.website_url, s.status, s.published_at,
                            s.updated_at, s.is_latest, s.schema_url
                        FROM mcp_servers s
                        INNER JOIN app_server_mapping m ON s.server_name = m.server_name
                        WHERE m.app_name = @app
                          AND datetime(s.last_cached, '+' || @ttl || ' hours') > datetime('now')
                        ORDER BY m.relevance_score DESC
                    ";

                    command.Parameters.AddWithValue("@app", appName);
                    command.Parameters.AddWithValue("@ttl", CacheTTLHours);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var server = new MCPServer
                            {
                                Name = reader.GetString(0),
                                Description = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                Version = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Repository = new Repository
                                {
                                    Url = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    Source = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                                },
                                WebsiteUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                                Schema = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
                            };

                            servers.Add(server);
                        }
                    }
                }
            }

            return servers;
        }

        /// <summary>
        /// Checks if cache is stale for a given application.
        /// </summary>
        /// <param name="appName">Application process name.</param>
        /// <returns>True if cache is stale (older than 24 hours) or doesn't exist.</returns>
        public bool IsCacheStale(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return true;
            }

            using (var connection = new SqliteConnection(this._connectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT COUNT(*)
                        FROM apps
                        WHERE app_name = @app
                          AND datetime(last_queried, '+' || @ttl || ' hours') > datetime('now')
                    ";

                    command.Parameters.AddWithValue("@app", appName);
                    command.Parameters.AddWithValue("@ttl", CacheTTLHours);

                    var count = (long)command.ExecuteScalar();
                    return count == 0;
                }
            }
        }

        /// <summary>
        /// Gets the total number of cached servers.
        /// </summary>
        /// <returns>Count of cached servers.</returns>
        public int GetCachedServerCount()
        {
            using (var connection = new SqliteConnection(this._connectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM mcp_servers";
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        /// <summary>
        /// Clears all stale cache entries (older than 24 hours).
        /// </summary>
        /// <returns>Number of entries deleted.</returns>
        public int ClearStaleCache()
        {
            using (var connection = new SqliteConnection(this._connectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        DELETE FROM mcp_servers
                        WHERE datetime(last_cached, '+' || @ttl || ' hours') <= datetime('now')
                    ";

                    command.Parameters.AddWithValue("@ttl", CacheTTLHours);
                    return command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Disposes the database connection.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    // SQLite connections are pooled by Microsoft.Data.Sqlite
                    // No explicit cleanup needed here
                }

                this._disposed = true;
            }
        }
    }
}
