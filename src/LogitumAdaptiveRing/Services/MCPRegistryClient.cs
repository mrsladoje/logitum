namespace Loupedeck.LogitumAdaptiveRing.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP client for the official MCP Registry API.
    /// Handles all communication with https://registry.modelcontextprotocol.io
    /// </summary>
    public class MCPRegistryClient : IMCPRegistryClient
    {
        private const string BaseUrl = "https://registry.modelcontextprotocol.io";
        private const string ApiVersion = "v0";
        private const int DefaultTimeout = 5000; // 5 seconds

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the MCPRegistryClient class.
        /// </summary>
        public MCPRegistryClient()
        {
            this._httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromMilliseconds(DefaultTimeout)
            };

            // Configure JSON deserialization options
            this._jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
        }

        /// <summary>
        /// Searches for MCP servers matching the specified application name.
        /// </summary>
        /// <param name="appName">The application name to search for (case-insensitive).</param>
        /// <param name="limit">Maximum number of results to return (default: 5).</param>
        /// <returns>A list of matching MCP servers, or empty list if none found or error occurs.</returns>
        public async Task<List<MCPServer>> SearchServersAsync(string appName, int limit = 5)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(appName))
                {
                    return new List<MCPServer>();
                }

                // Build query URL: /v0/servers?search={appName}&version=latest&limit={limit}
                var queryUrl = $"/{ApiVersion}/servers?search={Uri.EscapeDataString(appName)}&version=latest&limit={limit}";

                var response = await this._httpClient.GetAsync(queryUrl);

                if (!response.IsSuccessStatusCode)
                {
                    // Log error but don't throw - graceful degradation
                    System.Diagnostics.Debug.WriteLine($"[MCP-Registry] API returned {response.StatusCode} for query: {appName}");
                    return new List<MCPServer>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<MCPRegistryResponse>(content, this._jsonOptions);

                if (result == null || result.Servers == null || !result.Servers.Any())
                {
                    return new List<MCPServer>();
                }

                // Extract the Server objects from the ServerResponse wrappers
                return result.Servers
                    .Where(sr => sr?.Server != null)
                    .Select(sr => sr.Server)
                    .ToList();
            }
            catch (HttpRequestException ex)
            {
                // Network error - user might be offline
                System.Diagnostics.Debug.WriteLine($"[MCP-Registry] Network error querying registry: {ex.Message}");
                return new List<MCPServer>();
            }
            catch (TaskCanceledException)
            {
                // Timeout
                System.Diagnostics.Debug.WriteLine($"[MCP-Registry] Request timeout for query: {appName}");
                return new List<MCPServer>();
            }
            catch (JsonException ex)
            {
                // Parse error - API format changed?
                System.Diagnostics.Debug.WriteLine($"[MCP-Registry] JSON parse error: {ex.Message}");
                return new List<MCPServer>();
            }
            catch (Exception ex)
            {
                // Unexpected error - log and return empty
                System.Diagnostics.Debug.WriteLine($"[MCP-Registry] Unexpected error: {ex.Message}");
                return new List<MCPServer>();
            }
        }

        /// <summary>
        /// Gets detailed information for a specific MCP server by name.
        /// </summary>
        /// <param name="serverName">The exact server name to retrieve.</param>
        /// <returns>The server information, or null if not found or error occurs.</returns>
        public async Task<MCPServer> GetServerAsync(string serverName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serverName))
                {
                    return null;
                }

                // Search for exact server name
                var servers = await this.SearchServersAsync(serverName, limit: 1);

                // Return the first result if it's an exact match (case-insensitive)
                return servers.FirstOrDefault(s =>
                    string.Equals(s.Name, serverName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MCP-Registry] Error getting server {serverName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all available MCP servers (paginated).
        /// </summary>
        /// <param name="limit">Maximum number of results per page (default: 10).</param>
        /// <param name="cursor">Pagination cursor for subsequent pages (optional).</param>
        /// <returns>The registry response with servers and pagination metadata.</returns>
        public async Task<MCPRegistryResponse> GetAllServersAsync(int limit = 10, string cursor = null)
        {
            try
            {
                // Build query URL with pagination
                var queryUrl = $"/{ApiVersion}/servers?version=latest&limit={limit}";

                if (!string.IsNullOrWhiteSpace(cursor))
                {
                    queryUrl += $"&cursor={Uri.EscapeDataString(cursor)}";
                }

                var response = await this._httpClient.GetAsync(queryUrl);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[MCP-Registry] API returned {response.StatusCode}");
                    return new MCPRegistryResponse();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<MCPRegistryResponse>(content, this._jsonOptions);

                return result ?? new MCPRegistryResponse();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MCP-Registry] Error getting all servers: {ex.Message}");
                return new MCPRegistryResponse();
            }
        }

        /// <summary>
        /// Gets servers updated since a specific timestamp.
        /// Useful for cache invalidation and incremental updates.
        /// </summary>
        /// <param name="since">RFC3339 timestamp to filter by.</param>
        /// <param name="limit">Maximum number of results to return (default: 10).</param>
        /// <returns>List of servers updated since the specified time.</returns>
        public async Task<List<MCPServer>> GetUpdatedServersAsync(DateTime since, int limit = 10)
        {
            try
            {
                // Convert DateTime to RFC3339 format (ISO 8601)
                var sinceStr = since.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                // Build query URL with updated_since filter
                var queryUrl = $"/{ApiVersion}/servers?updated_since={Uri.EscapeDataString(sinceStr)}&version=latest&limit={limit}";

                var response = await this._httpClient.GetAsync(queryUrl);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[MCP-Registry] API returned {response.StatusCode}");
                    return new List<MCPServer>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<MCPRegistryResponse>(content, this._jsonOptions);

                if (result == null || result.Servers == null || !result.Servers.Any())
                {
                    return new List<MCPServer>();
                }

                // Extract the Server objects
                return result.Servers
                    .Where(sr => sr?.Server != null)
                    .Select(sr => sr.Server)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MCP-Registry] Error getting updated servers: {ex.Message}");
                return new List<MCPServer>();
            }
        }

        /// <summary>
        /// Disposes the HTTP client.
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
                    this._httpClient?.Dispose();
                }

                this._disposed = true;
            }
        }
    }
}
