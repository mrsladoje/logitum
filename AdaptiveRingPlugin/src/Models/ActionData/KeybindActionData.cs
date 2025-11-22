namespace Loupedeck.AdaptiveRingPlugin.Models.ActionData;

public class KeybindActionData
{
    public required List<string> Keys { get; set; } // e.g., ["Ctrl", "C"]
    public string? Description { get; set; }
}
