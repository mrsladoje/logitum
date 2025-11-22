using System;

namespace Loupedeck.LogitumAdaptiveRing.Services
{
    /// <summary>
    /// Interface for monitoring active application/process changes
    /// </summary>
    public interface IProcessMonitor : IDisposable
    {
        /// <summary>
        /// Event fired when the active application changes
        /// </summary>
        event EventHandler<ProcessInfo> ApplicationChanged;

        /// <summary>
        /// Starts monitoring for active window changes
        /// </summary>
        void Start();

        /// <summary>
        /// Stops monitoring for active window changes
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets the currently active process information
        /// </summary>
        /// <returns>Current process info, or null if unable to determine</returns>
        ProcessInfo GetCurrentProcess();
    }
}
