namespace Loupedeck.AdaptiveRingPlugin.Services
{
    using System;
    using System.Diagnostics;
    using System.Timers;

    /// <summary>
    /// Monitors the active foreground process and detects app switches
    /// </summary>
    public class ProcessMonitor : IDisposable
    {
        private readonly Timer _pollTimer;
        private readonly ActionPersistenceService? _persistenceService;
        private readonly ActionsRingManager? _actionsRingManager;
        private readonly GeminiActionSuggestor? _geminiSuggestor;
        private readonly MCPRegistryClient? _mcpClient;
        private string _currentProcessName = "";
        private IntPtr _currentWindowHandle = IntPtr.Zero;

        public event EventHandler<AppSwitchedEventArgs> AppSwitched = null!;

        // Constructor for backward compatibility (no services)
        public ProcessMonitor()
        {
            // Poll every 100ms for foreground window changes
            _pollTimer = new Timer(100);
            _pollTimer.Elapsed += CheckForegroundProcess;
            _pollTimer.AutoReset = true;
        }

        // Constructor with full service integration
        public ProcessMonitor(
            ActionPersistenceService persistenceService,
            ActionsRingManager actionsRingManager,
            GeminiActionSuggestor geminiSuggestor,
            MCPRegistryClient mcpClient)
        {
            _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
            _actionsRingManager = actionsRingManager ?? throw new ArgumentNullException(nameof(actionsRingManager));
            _geminiSuggestor = geminiSuggestor ?? throw new ArgumentNullException(nameof(geminiSuggestor));
            _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));

            // Poll every 100ms for foreground window changes
            _pollTimer = new Timer(100);
            _pollTimer.Elapsed += CheckForegroundProcess;
            _pollTimer.AutoReset = true;
        }

        public void Start()
        {
            PluginLog.Info("ProcessMonitor: Starting foreground app monitoring...");
            _pollTimer.Start();

            // Check immediately on start
            CheckForegroundProcess(null, null!);
        }

        public void Stop()
        {
            PluginLog.Info("ProcessMonitor: Stopping foreground app monitoring...");
            _pollTimer.Stop();
        }

        private async void CheckForegroundProcess(object? sender, ElapsedEventArgs? e)
        {
            try
            {
                // Get the foreground window handle
                IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();

                // Skip if no window or same as current
                if (foregroundWindow == IntPtr.Zero || foregroundWindow == _currentWindowHandle)
                {
                    return;
                }

                // Get the process ID
                uint processId;
                NativeMethods.GetWindowThreadProcessId(foregroundWindow, out processId);

                if (processId == 0)
                {
                    return;
                }

                // Get the process
                Process process = Process.GetProcessById((int)processId);
                string processName = process.ProcessName;
                string mainWindowTitle = process.MainWindowTitle;

                // Only fire event if the process actually changed
                if (processName != _currentProcessName)
                {
                    PluginLog.Info($"ProcessMonitor: App switched -> {processName} ({mainWindowTitle})");

                    _currentProcessName = processName;
                    _currentWindowHandle = foregroundWindow;

                    // NEW WORKFLOW: Check persistence before querying registries
                    if (_persistenceService != null && _actionsRingManager != null && _geminiSuggestor != null && _mcpClient != null)
                    {
                        await HandleAppSwitchWithPersistenceAsync(processName, mainWindowTitle);
                    }
                    else
                    {
                        // Legacy path: Fire the app switched event for external handling
                        AppSwitched?.Invoke(this, new AppSwitchedEventArgs
                        {
                            ProcessName = processName,
                            WindowTitle = mainWindowTitle,
                            ProcessId = (int)processId,
                            WindowHandle = foregroundWindow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't spam logs with errors, just log once per minute max
                PluginLog.Error($"ProcessMonitor: Error checking foreground process: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task HandleAppSwitchWithPersistenceAsync(string appName, string windowTitle)
        {
            try
            {
                // Step 1: Check if we have persisted actions for this app
                if (_persistenceService!.HasRememberedApp(appName))
                {
                    PluginLog.Info($"ProcessMonitor: Found persisted actions for {appName}, loading...");
                    _actionsRingManager!.LoadActionsForApp(appName);
                    PluginLog.Info($"ProcessMonitor: Actions loaded successfully for {appName}");
                    return; // DONE - we already have actions for this app
                }

                // Step 2: Show notification - setting up
                ShowNotification($"Setting up {appName}...");
                PluginLog.Info($"ProcessMonitor: No persisted actions for {appName}, querying MCP registries...");

                // Step 3: Query MCP registries (existing code)
                var mcpServer = await _mcpClient!.FindServerAsync(appName);

                if (mcpServer == null)
                {
                    PluginLog.Info($"ProcessMonitor: No MCP server found for {appName}");
                    PluginLog.Info($"ProcessMonitor: Will request keybind-only suggestions from Gemini");
                }
                else
                {
                    PluginLog.Info($"ProcessMonitor: MCP Server Found: {mcpServer.ServerName}");
                }

                // Step 4: Hybrid MCP connection (skipped for now)

                // Step 5: Call Gemini to suggest actions
                PluginLog.Info($"ProcessMonitor: Requesting AI-suggested actions for {appName}...");
                var mcpServers = mcpServer != null ? new List<Models.MCPServerData> { mcpServer } : new List<Models.MCPServerData>();
                var suggestedActions = await _geminiSuggestor!.SuggestActionsAsync(appName, mcpServers, mcpAvailable: mcpServer != null);

                if (suggestedActions == null || suggestedActions.Count == 0)
                {
                    PluginLog.Warning($"ProcessMonitor: No actions suggested for {appName}");
                    ShowNotification($"No actions suggested for {appName}");
                    return;
                }

                PluginLog.Info($"ProcessMonitor: {suggestedActions.Count} actions suggested for {appName}");

                // Step 6: Persist the actions
                var displayName = string.IsNullOrWhiteSpace(windowTitle) ? appName : windowTitle;
                _persistenceService!.SaveAppActions(appName, displayName, suggestedActions, mcpServer?.ServerName);
                PluginLog.Info($"ProcessMonitor: Actions persisted for {appName}");

                // Step 7: Update the actions ring
                _actionsRingManager!.LoadActionsForApp(appName);
                PluginLog.Info($"ProcessMonitor: Actions ring updated for {appName}");

                // Step 8: Show success notification
                ShowNotification($"Actions ready for {appName}!");
                PluginLog.Info($"ProcessMonitor: Setup complete for {appName}");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"ProcessMonitor: Error handling app switch with persistence: {ex.Message}");
                ShowNotification($"Error setting up {appName}");
            }
        }

        private void ShowNotification(string message)
        {
            // Use plugin logging as notification mechanism
            // This can be enhanced with Toast notifications when plugin SDK supports it
            PluginLog.Info($"[NOTIFICATION] {message}");
        }

        public void Dispose()
        {
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
        }
    }

    /// <summary>
    /// Event args for app switching events
    /// </summary>
    public class AppSwitchedEventArgs : EventArgs
    {
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public IntPtr WindowHandle { get; set; }
    }

    /// <summary>
    /// Native Windows API methods for process detection
    /// </summary>
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
