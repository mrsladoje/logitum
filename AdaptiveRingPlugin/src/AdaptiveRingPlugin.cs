namespace Loupedeck.AdaptiveRingPlugin
{
    using System;
    using Loupedeck.AdaptiveRingPlugin.Services;

    // This class contains the plugin-level logic of the Loupedeck plugin.

    public class AdaptiveRingPlugin : Plugin
    {
        private ProcessMonitor _processMonitor;

        // Gets a value indicating whether this is an API-only plugin.
        public override Boolean UsesApplicationApiOnly => true;

        // Gets a value indicating whether this is a Universal plugin or an Application plugin.
        public override Boolean HasNoApplication => true;

        // Initializes a new instance of the plugin class.
        public AdaptiveRingPlugin()
        {
            // Initialize the plugin log.
            PluginLog.Init(this.Log);

            // Initialize the plugin resources.
            PluginResources.Init(this.Assembly);
        }

        // This method is called when the plugin is loaded.
        public override void Load()
        {
            PluginLog.Info("============================================");
            PluginLog.Info("🎯 AdaptiveRing Plugin Loading...");
            PluginLog.Info("============================================");

            // Initialize the process monitor
            _processMonitor = new ProcessMonitor();
            _processMonitor.AppSwitched += OnAppSwitched;
            _processMonitor.Start();

            PluginLog.Info("✅ ProcessMonitor started successfully");
            PluginLog.Info("🚀 AdaptiveRing Plugin is now active!");
        }

        // This method is called when the plugin is unloaded.
        public override void Unload()
        {
            PluginLog.Info("🛑 AdaptiveRing Plugin unloading...");

            // Stop and dispose the process monitor
            if (_processMonitor != null)
            {
                _processMonitor.AppSwitched -= OnAppSwitched;
                _processMonitor.Stop();
                _processMonitor.Dispose();
                _processMonitor = null;
            }

            PluginLog.Info("👋 AdaptiveRing Plugin unloaded");
        }

        private void OnAppSwitched(object sender, AppSwitchedEventArgs e)
        {
            PluginLog.Info($"📱 App Switch Detected: {e.ProcessName}");
            PluginLog.Info($"   Window: {e.WindowTitle}");
            PluginLog.Info($"   PID: {e.ProcessId}");

            // TODO: In next phase, this will:
            // 1. Query MCP Registry for this app
            // 2. Get AI-suggested actions
            // 3. Update the Actions Ring
        }
    }
}
