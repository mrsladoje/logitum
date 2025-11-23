using System.Runtime.InteropServices;
using Loupedeck.AdaptiveRingPlugin.Models;

namespace Loupedeck.AdaptiveRingPlugin.Services;

/// <summary>
/// Monitors UI interactions using Windows UI Automation
/// </summary>
public class UIInteractionMonitor : IDisposable
{
    private readonly AppDatabase _database;
    private readonly ProcessMonitor _processMonitor;
    private System.Threading.Timer? _cleanupTimer;
    private bool _isRunning;

    // UI Automation COM imports
    [DllImport("oleacc.dll")]
    private static extern IntPtr GetProcessHandleFromHwnd(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    public UIInteractionMonitor(AppDatabase database, ProcessMonitor processMonitor)
    {
        _database = database;
        _processMonitor = processMonitor;
    }

    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;

        // Start cleanup timer - runs every 5 minutes
        _cleanupTimer = new System.Threading.Timer(
            async _ => await CleanupExpiredInteractions(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(5)
        );

        PluginLog.Info("UIInteractionMonitor started");
    }

    public void Stop()
    {
        _isRunning = false;
        _cleanupTimer?.Dispose();
        PluginLog.Info("UIInteractionMonitor stopped");
    }

    /// <summary>
    /// Captures a UI interaction event for the current active window
    /// </summary>
    public async Task CaptureInteractionAsync(string interactionType, string? elementName = null, string? controlType = null)
    {
        try
        {
            var currentApp = GetCurrentWindowTitle();
            if (string.IsNullOrEmpty(currentApp))
                return;

            // Create simplified description
            var simplifiedDescription = CreateSimplifiedDescription(controlType ?? "element", elementName ?? "unknown");

            // Calculate expiration time (15 minutes from now)
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiresAt = timestamp + 900; // 15 minutes = 900 seconds

            var interaction = new UIInteraction
            {
                AppName = currentApp,
                WindowTitle = currentApp,
                InteractionType = interactionType,
                ElementName = elementName,
                SimplifiedDescription = simplifiedDescription,
                Timestamp = timestamp,
                ExpiresAt = expiresAt
            };

            await _database.SaveUIInteractionAsync(interaction);
            PluginLog.Verbose($"Captured {interactionType} interaction for {currentApp}: {simplifiedDescription}");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to capture UI interaction: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a simplified description of the interaction
    /// </summary>
    private string CreateSimplifiedDescription(string controlType, string elementName)
    {
        return $"{controlType} {elementName}";
    }

    /// <summary>
    /// Gets the title of the currently active window
    /// </summary>
    private string GetCurrentWindowTitle()
    {
        try
        {
            var handle = GetForegroundWindow();
            var text = new System.Text.StringBuilder(256);
            GetWindowText(handle, text, 256);
            return text.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task CleanupExpiredInteractions()
    {
        try
        {
            await _database.CleanupExpiredInteractionsAsync();
            PluginLog.Verbose("Cleaned up expired UI interactions");
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to cleanup expired interactions: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
