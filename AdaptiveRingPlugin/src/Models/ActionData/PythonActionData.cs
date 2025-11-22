namespace Loupedeck.AdaptiveRingPlugin.Models.ActionData;

public class PythonActionData
{
    public string? ScriptPath { get; set; }
    public string? ScriptCode { get; set; }
    public List<string>? Arguments { get; set; }
    public string? Description { get; set; }
}
