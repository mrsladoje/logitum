using System;

namespace Loupedeck.LogitumAdaptiveRing.Services
{
    /// <summary>
    /// Data model representing information about an active process/application
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// Gets or sets the process identifier
        /// </summary>
        public uint ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the process name (e.g., "chrome", "Code")
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full path to the executable
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the window title text
        /// </summary>
        public string WindowTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when this process was detected as active
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Returns a string representation of the process info
        /// </summary>
        public override string ToString()
        {
            return $"{ProcessName} (PID: {ProcessId}) - {WindowTitle}";
        }
    }
}
