namespace Loupedeck.AdaptiveRingPlugin.Models;

/// <summary>
/// Represents a UI interaction event captured from an application window.
/// Tracks user interactions with UI elements for context-aware action suggestions.
/// </summary>
public class UIInteraction
{
    /// <summary>
    /// Unique identifier for the interaction record
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Name of the application where the interaction occurred
    /// </summary>
    public required string AppName { get; set; }

    /// <summary>
    /// Title of the window where the interaction occurred
    /// </summary>
    public string? WindowTitle { get; set; }

    /// <summary>
    /// Type of UI interaction (e.g., "Invoke", "Focus", "ValueChange", "SelectionChange")
    /// </summary>
    public required string InteractionType { get; set; }

    /// <summary>
    /// Name or identifier of the UI element that was interacted with
    /// </summary>
    public string? ElementName { get; set; }

    /// <summary>
    /// Simplified human-readable description of the interaction (e.g., "button Home", "textinput username")
    /// </summary>
    public required string SimplifiedDescription { get; set; }

    /// <summary>
    /// Unix timestamp (seconds) when the interaction occurred
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Unix timestamp (seconds) when this interaction record should expire
    /// Default TTL is 15 minutes from timestamp
    /// </summary>
    public long ExpiresAt { get; set; }
}
