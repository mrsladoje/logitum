namespace Loupedeck.AdaptiveRingPlugin.Models;

/// <summary>
/// Represents a semantic workflow identified from user interactions
/// </summary>
public class SemanticWorkflow
{
    /// <summary>
    /// Unique identifier for this workflow
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The application name this workflow belongs to
    /// </summary>
    public required string AppName { get; set; }

    /// <summary>
    /// Human-readable description of the workflow
    /// </summary>
    public required string WorkflowText { get; set; }

    /// <summary>
    /// JSON array of raw interaction IDs that comprise this workflow
    /// </summary>
    public required string RawInteractionIds { get; set; }

    /// <summary>
    /// Timestamp when this workflow was created (Unix epoch seconds)
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// Confidence score from the AI analysis (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; }
}
