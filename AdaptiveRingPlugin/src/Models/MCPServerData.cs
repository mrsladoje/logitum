namespace Loupedeck.AdaptiveRingPlugin.Models;

public class MCPServerData
{
    public string ServerName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RegistrySource { get; set; } = string.Empty;
    public bool Validated { get; set; }
    public Dictionary<string, ToolInfo>? Tools { get; set; }
}

public class ToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ToolSDKPackage
{
    public string Category { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Validated { get; set; }
    public Dictionary<string, ToolSDKTool>? Tools { get; set; }
}

public class ToolSDKTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class OfficialRegistryResponse
{
    public List<OfficialServer> Servers { get; set; } = new();
    public OfficialMetadata? Metadata { get; set; }
}

public class OfficialServer
{
    public OfficialServerInfo? Server { get; set; }
}

public class OfficialServerInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Title { get; set; }
}

public class OfficialMetadata
{
    public int Count { get; set; }
}

public class GlamaResponse
{
    public List<GlamaServer> Servers { get; set; } = new();
}

public class GlamaServer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class RememberedApp
{
    public required string AppName { get; set; }
    public required string DisplayName { get; set; }
    public string? McpServerName { get; set; }
    public long CreatedAt { get; set; }
    public long LastSeenAt { get; set; }
}

public enum ActionType { Prompt, Keybind, Python }

public class AppAction
{
    public int Id { get; set; }
    public required string AppName { get; set; }
    public int Position { get; set; } // 0-7
    public ActionType Type { get; set; }
    public required string ActionName { get; set; }
    public required string ActionDataJson { get; set; } // Serialized action data
    public bool Enabled { get; set; } = true;
}
