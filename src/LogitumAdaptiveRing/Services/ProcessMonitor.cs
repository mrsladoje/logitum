using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Loupedeck.LogitumAdaptiveRing.Helpers;

namespace Loupedeck.LogitumAdaptiveRing.Services
{
    /// <summary>
    /// Monitors active window/application changes using Windows APIs
    /// </summary>
    public class ProcessMonitor : IProcessMonitor
    {
        private Timer _timer;
        private ProcessInfo _lastActiveProcess;
        private readonly object _lock = new object();
        private bool _isRunning = false;

        /// <summary>
        /// Polling interval in milliseconds (1000ms = 1 second)
        /// </summary>
        private const int POLLING_INTERVAL_MS = 1000;

        /// <summary>
        /// Maximum length for window title text
        /// </summary>
        private const int MAX_TITLE_LENGTH = 256;

        /// <inheritdoc/>
        public event EventHandler<ProcessInfo> ApplicationChanged;

        /// <summary>
        /// Starts monitoring for active window changes
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    return; // Already running
                }

                _timer = new Timer(CheckActiveWindow, null, 0, POLLING_INTERVAL_MS);
                _isRunning = true;
            }
        }

        /// <summary>
        /// Stops monitoring for active window changes
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning)
                {
                    return; // Not running
                }

                if (_timer != null)
                {
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
                _isRunning = false;
            }
        }

        /// <summary>
        /// Gets the currently active process information
        /// </summary>
        /// <returns>Current process info, or null if unable to determine</returns>
        public ProcessInfo GetCurrentProcess()
        {
            try
            {
                var hwnd = Win32Api.GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    return null;
                }

                return GetProcessInfoFromWindow(hwnd);
            }
            catch
            {
                // Silent error handling as requested
                return null;
            }
        }

        /// <summary>
        /// Timer callback that checks the active window
        /// </summary>
        private void CheckActiveWindow(object state)
        {
            try
            {
                var currentProcess = GetCurrentProcess();
                if (currentProcess == null)
                {
                    return; // Unable to get current process, skip this iteration
                }

                // Check if the process has changed (case-insensitive comparison)
                bool hasChanged = _lastActiveProcess == null ||
                    !string.Equals(_lastActiveProcess.ProcessName, currentProcess.ProcessName,
                        StringComparison.OrdinalIgnoreCase);

                if (hasChanged)
                {
                    _lastActiveProcess = currentProcess;
                    ApplicationChanged?.Invoke(this, currentProcess);
                }
            }
            catch
            {
                // Silent error handling - log and skip
                // In a production environment, this would be logged to a proper logging system
            }
        }

        /// <summary>
        /// Extracts process information from a window handle
        /// </summary>
        /// <param name="hwnd">Window handle</param>
        /// <returns>Process information</returns>
        private ProcessInfo GetProcessInfoFromWindow(IntPtr hwnd)
        {
            try
            {
                // Get process ID from window handle
                Win32Api.GetWindowThreadProcessId(hwnd, out uint processId);
                if (processId == 0)
                {
                    return null;
                }

                // Get process details
                using var process = Process.GetProcessById((int)processId);

                // Get window title
                string windowTitle = GetWindowTitle(hwnd);

                // Get executable path (may fail for some processes)
                string executablePath = string.Empty;
                try
                {
                    executablePath = process.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    // Some system processes don't allow access to MainModule
                    // Skip silently as requested
                }

                return new ProcessInfo
                {
                    ProcessId = processId,
                    ProcessName = process.ProcessName,
                    ExecutablePath = executablePath,
                    WindowTitle = windowTitle,
                    DetectedAt = DateTime.UtcNow
                };
            }
            catch
            {
                // Silent error handling for privileged processes
                return null;
            }
        }

        /// <summary>
        /// Gets the window title from a window handle
        /// </summary>
        /// <param name="hwnd">Window handle</param>
        /// <returns>Window title text</returns>
        private string GetWindowTitle(IntPtr hwnd)
        {
            try
            {
                int length = Win32Api.GetWindowTextLength(hwnd);
                if (length == 0)
                {
                    return string.Empty;
                }

                var builder = new StringBuilder(Math.Min(length + 1, MAX_TITLE_LENGTH));
                Win32Api.GetWindowText(hwnd, builder, builder.Capacity);
                return builder.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Disposes of resources used by the ProcessMonitor
        /// </summary>
        public void Dispose()
        {
            Stop();
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }
        }
    }
}
