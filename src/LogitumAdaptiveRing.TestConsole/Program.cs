using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Loupedeck.LogitumAdaptiveRing.Services;

namespace LogitumAdaptiveRing.TestConsole
{
    /// <summary>
    /// Test console application for ProcessMonitor and MCP Registry
    /// </summary>
    internal class Program
    {
        private static ProcessMonitor _monitor;
        private static readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);

        static async Task Main(string[] args)
        {
            // Check for test mode argument
            if (args.Length > 0 && args[0] == "--test-mcp")
            {
                await RunMCPQueryTest();
                return;
            }

            // Default: Process Monitor Test
            Console.WriteLine("MCP Adaptive Ring - Process Monitor Test");
            Console.WriteLine("==========================================");
            Console.WriteLine("Monitoring active window... Press Ctrl+C to exit");
            Console.WriteLine("TIP: Run with '--test-mcp' to test MCP Registry queries");
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

        /// <summary>
        /// Test MCP Registry queries with various application names
        /// </summary>
        private static async Task RunMCPQueryTest()
        {
            Console.WriteLine("MCP Adaptive Ring - MCP Registry Query Test");
            Console.WriteLine("============================================");
            Console.WriteLine();

            using (var client = new MCPRegistryClient())
            {
                // Test apps that likely have MCP servers
                var testApps = new[] { "github", "vscode", "slack", "postgres", "docker", "git", "notion", "figma" };

                foreach (var app in testApps)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"🔍 Searching for '{app}'...");
                    Console.ResetColor();

                    var servers = await client.SearchServersAsync(app, limit: 3);

                    if (servers.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($" ✅ Found {servers.Count} server(s)");
                        Console.ResetColor();

                        foreach (var server in servers)
                        {
                            Console.WriteLine($"   📦 {server.Name} v{server.Version}");
                            Console.WriteLine($"      {server.Description}");

                            if (!string.IsNullOrEmpty(server.Repository?.Url))
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine($"      Repository: {server.Repository.Url}");
                                Console.ResetColor();
                            }

                            if (!string.IsNullOrEmpty(server.WebsiteUrl))
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine($"      Website: {server.WebsiteUrl}");
                                Console.ResetColor();
                            }

                            Console.WriteLine();
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(" ⚠️  No servers found");
                        Console.ResetColor();
                        Console.WriteLine();
                    }
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ MCP Registry test complete!");
                Console.ResetColor();
            }
        }
    }
}
