using System;
using System.Threading;
using Loupedeck.LogitumAdaptiveRing.Services;

namespace LogitumAdaptiveRing.TestConsole
{
    /// <summary>
    /// Test console application for ProcessMonitor
    /// </summary>
    internal class Program
    {
        private static ProcessMonitor _monitor;
        private static readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            Console.WriteLine("MCP Adaptive Ring - Process Monitor Test");
            Console.WriteLine("==========================================");
            Console.WriteLine("Monitoring active window... Press Ctrl+C to exit");
            Console.WriteLine();

            // Set up Ctrl+C handler for graceful shutdown
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent immediate termination
                Console.WriteLine();
                Console.WriteLine("Shutting down...");
                _exitEvent.Set();
            };

            // Create and start the process monitor
            _monitor = new ProcessMonitor();
            _monitor.ApplicationChanged += OnApplicationChanged;

            // Display initial process
            var initialProcess = _monitor.GetCurrentProcess();
            if (initialProcess != null)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Initial: {initialProcess.ProcessName} ({initialProcess.ExecutablePath})");
                Console.WriteLine($"           Window: {initialProcess.WindowTitle}");
                Console.WriteLine();
            }

            _monitor.Start();

            // Wait for exit signal
            _exitEvent.WaitOne();

            // Clean up
            _monitor.Stop();
            _monitor.Dispose();

            Console.WriteLine("Process monitor stopped. Goodbye!");
        }

        /// <summary>
        /// Event handler for application changes
        /// </summary>
        private static void OnApplicationChanged(object sender, ProcessInfo info)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Switch -> {info.ProcessName}");
            Console.ResetColor();

            Console.WriteLine($"           Process: {info.ProcessName}.exe");
            Console.WriteLine($"           Path:    {(string.IsNullOrEmpty(info.ExecutablePath) ? "<access denied>" : info.ExecutablePath)}");
            Console.WriteLine($"           Window:  {info.WindowTitle}");
            Console.WriteLine($"           PID:     {info.ProcessId}");
            Console.WriteLine();
        }
    }
}
