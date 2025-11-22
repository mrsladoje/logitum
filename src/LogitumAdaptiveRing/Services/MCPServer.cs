namespace Loupedeck.LogitumAdaptiveRing.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents the complete response from the MCP Registry API.
    /// API Endpoint: GET /v0/servers
    /// </summary>
    public class MCPRegistryResponse
    {
        /// <summary>
        /// Gets or sets the list of servers returned by the registry.
        /// </summary>
        [JsonPropertyName("servers")]
        public List<ServerResponse> Servers { get; set; } = new List<ServerResponse>();

        /// <summary>
        /// Gets or sets pagination metadata.
        /// </summary>
        [JsonPropertyName("metadata")]
        public PaginationMetadata Metadata { get; set; } = new PaginationMetadata();
    }

    /// <summary>
    /// Represents a single server entry in the registry response.
    /// </summary>
    public class ServerResponse
    {
        /// <summary>
        /// Gets or sets the server configuration and metadata.
        /// </summary>
        [JsonPropertyName("server")]
        public MCPServer Server { get; set; } = new MCPServer();

        /// <summary>
        /// Gets or sets registry-managed metadata.
        /// </summary>
        [JsonPropertyName("_meta")]
        public RegistryMeta Meta { get; set; } = new RegistryMeta();
    }

    /// <summary>
    /// Represents an MCP Server with its configuration and metadata.
    /// This is the primary data model for MCP servers.
    /// </summary>
    public class MCPServer
    {
        /// <summary>
        /// Gets or sets the JSON Schema URI reference.
        /// </summary>
        [JsonPropertyName("$schema")]
        public string Schema { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server name in reverse-DNS format (e.g., "github", "slack").
        /// This is the unique identifier for the server.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable description (1-100 characters).
        /// Example: "GitHub repository management and automation"
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the server version (semantic versioning recommended).
        /// Example: "1.0.0"
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name for UI (1-100 characters, nullable).
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the repository metadata.
        /// </summary>
        [JsonPropertyName("repository")]
        public Repository Repository { get; set; } = new Repository();

        /// <summary>
        /// Gets or sets the homepage or documentation URL (nullable).
        /// </summary>
        [JsonPropertyName("websiteUrl")]
        public string WebsiteUrl { get; set; }

        /// <summary>
        /// Gets or sets the package configurations (nullable).
        /// </summary>
        [JsonPropertyName("packages")]
        public List<Package> Packages { get; set; }

        /// <summary>
        /// Gets or sets the remote transport configurations (nullable).
        /// </summary>
        [JsonPropertyName("remotes")]
        public List<Transport> Remotes { get; set; }

        /// <summary>
        /// Gets or sets the icons for UI display (nullable).
        /// </summary>
        [JsonPropertyName("icons")]
        public List<Icon> Icons { get; set; }

        /// <summary>
        /// Gets or sets publisher-provided metadata.
        /// </summary>
        [JsonPropertyName("_meta")]
        public ServerPublisherMeta PublisherMeta { get; set; }

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            return $"{Name} v{Version}: {Description}";
        }
    }

    /// <summary>
    /// Represents repository information for an MCP server.
    /// </summary>
    public class Repository
    {
        /// <summary>
        /// Gets or sets the repository URL.
        /// Example: "https://github.com/modelcontextprotocol/servers"
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source type (e.g., "github").
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents registry-managed metadata for a server.
    /// </summary>
    public class RegistryMeta
    {
        /// <summary>
        /// Gets or sets the official registry metadata.
        /// </summary>
        [JsonPropertyName("io.modelcontextprotocol.registry/official")]
        public OfficialRegistryMeta Official { get; set; } = new OfficialRegistryMeta();
    }

    /// <summary>
    /// Represents the official MCP Registry metadata.
    /// </summary>
    public class OfficialRegistryMeta
    {
        /// <summary>
        /// Gets or sets the server status (active, deprecated, or deleted).
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "active";

        /// <summary>
        /// Gets or sets when the server was first published.
        /// </summary>
        [JsonPropertyName("publishedAt")]
        public DateTime PublishedAt { get; set; }

        /// <summary>
        /// Gets or sets when the server was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets whether this is the latest version.
        /// </summary>
        [JsonPropertyName("isLatest")]
        public bool IsLatest { get; set; }
    }

    /// <summary>
    /// Represents pagination metadata for server list responses.
    /// </summary>
    public class PaginationMetadata
    {
        /// <summary>
        /// Gets or sets the number of items in the current page.
        /// </summary>
        [JsonPropertyName("count")]
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the cursor for the next page (nullable).
        /// Null indicates this is the last page.
        /// </summary>
        [JsonPropertyName("nextCursor")]
        public string NextCursor { get; set; }
    }

    /// <summary>
    /// Represents a package configuration for an MCP server.
    /// </summary>
    public class Package
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a remote transport configuration.
    /// </summary>
    public class Transport
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an icon for UI display.
    /// </summary>
    public class Icon
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public int Size { get; set; }
    }

    /// <summary>
    /// Represents publisher-provided metadata.
    /// </summary>
    public class ServerPublisherMeta
    {
        // Placeholder for publisher-specific metadata
        // Extend as needed based on actual usage
    }
}
