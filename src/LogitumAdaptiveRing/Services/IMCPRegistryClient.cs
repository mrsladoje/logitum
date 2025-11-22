namespace Loupedeck.LogitumAdaptiveRing.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for MCP Registry API client.
    /// Provides methods to query the Model Context Protocol registry for available servers.
    /// </summary>
    public interface IMCPRegistryClient : IDisposable
    {
        /// <summary>
        /// Searches for MCP servers matching the specified application name.
        /// </summary>
        /// <param name="appName">The application name to search for (case-insensitive).</param>
        /// <param name="limit">Maximum number of results to return (default: 5).</param>
        /// <returns>A list of matching MCP servers, or empty list if none found or error occurs.</returns>
        /// <remarks>
        /// This method performs a case-insensitive substring search on server names.
        /// It automatically filters for latest versions only.
        /// Network errors and API failures return empty list (graceful degradation).
        /// </remarks>
        Task<List<MCPServer>> SearchServersAsync(string appName, int limit = 5);

        /// <summary>
        /// Gets detailed information for a specific MCP server by name.
        /// </summary>
        /// <param name="serverName">The exact server name to retrieve.</param>
        /// <returns>The server information, or null if not found or error occurs.</returns>
        Task<MCPServer> GetServerAsync(string serverName);

        /// <summary>
        /// Gets all available MCP servers (paginated).
        /// </summary>
        /// <param name="limit">Maximum number of results per page (default: 10).</param>
        /// <param name="cursor">Pagination cursor for subsequent pages (optional).</param>
        /// <returns>The registry response with servers and pagination metadata.</returns>
        Task<MCPRegistryResponse> GetAllServersAsync(int limit = 10, string cursor = null);

        /// <summary>
        /// Gets servers updated since a specific timestamp.
        /// Useful for cache invalidation and incremental updates.
        /// </summary>
        /// <param name="since">RFC3339 timestamp to filter by (e.g., "2025-08-07T13:15:04.280Z").</param>
        /// <param name="limit">Maximum number of results to return (default: 10).</param>
        /// <returns>List of servers updated since the specified time.</returns>
        Task<List<MCPServer>> GetUpdatedServersAsync(DateTime since, int limit = 10);
    }
}
