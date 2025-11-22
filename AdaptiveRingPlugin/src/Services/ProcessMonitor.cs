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
        private string _currentProcessName = "";
        private IntPtr _currentWindowHandle = IntPtr.Zero;

        public event EventHandler<AppSwitchedEventArgs> AppSwitched = null!;

        public ProcessMonitor()
        {
            // Poll every 500ms for foreground window changes
            _pollTimer = new Timer(500);
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

        private void CheckForegroundProcess(object? sender, ElapsedEventArgs? e)
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

                    // Fire the app switched event
                    AppSwitched?.Invoke(this, new AppSwitchedEventArgs
                    {
                        ProcessName = processName,
                        WindowTitle = mainWindowTitle,
                        ProcessId = (int)processId,
                        WindowHandle = foregroundWindow
                    });
                }
            }
            catch (Exception ex)
            {
                // Don't spam logs with errors, just log once per minute max
                PluginLog.Error($"ProcessMonitor: Error checking foreground process: {ex.Message}");
            }
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
