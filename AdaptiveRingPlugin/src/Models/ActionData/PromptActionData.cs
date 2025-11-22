namespace Loupedeck.AdaptiveRingPlugin.Models.ActionData;

public class PromptActionData
{
    public required string McpServerName { get; set; }
    public required string ToolName { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string? Description { get; set; }
}
