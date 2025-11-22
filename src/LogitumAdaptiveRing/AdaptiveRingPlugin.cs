namespace Loupedeck.LogitumAdaptiveRing
{
    using System;
    using Loupedeck.LogitumAdaptiveRing.Services;

    /// <summary>
    /// MCP Adaptive Ring - Main Plugin Class
    ///
    /// This plugin integrates Logitech MX devices with the Model Context Protocol ecosystem,
    /// providing context-aware actions through intelligent discovery and adaptation.
    /// </summary>
    public class AdaptiveRingPlugin : Plugin
    {
        private const string LogTag = "[MCP-AdaptiveRing]";

        // Phase 2: Process Monitoring
        private ProcessMonitor _processMonitor;

        /// <summary>
        /// Gets a value indicating whether this is an API-only plugin.
        /// MCP Adaptive Ring works across all applications, so it's API-only.
        /// </summary>
        public override bool UsesApplicationApiOnly => true;

        /// <summary>
        /// Gets a value indicating whether this is a Universal plugin (not tied to specific app).
        /// </summary>
        public override bool HasNoApplication => true;

        /// <summary>
        /// Initializes a new instance of the AdaptiveRingPlugin class.
        /// </summary>
        public AdaptiveRingPlugin()
        {
            // Initialize logging
            this.Log.Info($"{LogTag} Plugin constructor called");

            // Phase 2: Initialize process monitor
            this._processMonitor = new ProcessMonitor();
            this.Log.Info($"{LogTag} ProcessMonitor initialized");

            // TODO: Phase 2 - Initialize MCP registry client
            // TODO: Phase 3 - Initialize UI automation tracker
            // TODO: Phase 3 - Initialize SQLite database
        }

        /// <summary>
        /// Called when the plugin is loaded by Logi Plugin Service.
        /// This is where we start all our background services.
        /// </summary>
        public override void Load()
        {
            try
            {
                this.Log.Info($"{LogTag} Plugin loading...");

                // Phase 2: Start process monitor
                this._processMonitor.ApplicationChanged += this.OnApplicationChanged;
                this._processMonitor.Start();
                this.Log.Info($"{LogTag} ProcessMonitor started - monitoring active window changes");

                // TODO: Phase 2 - Start MCP registry queries
                // TODO: Phase 3 - Start UI automation tracking
                // TODO: Phase 4 - Initialize AI suggestion engine

                this.Log.Info($"{LogTag} Plugin loaded successfully ✅");
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR during plugin load: {ex.Message}");
                this.Log.Error($"{LogTag} Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Called when the plugin is unloaded.
        /// Clean up all resources here.
        /// </summary>
        public override void Unload()
        {
            try
            {
                this.Log.Info($"{LogTag} Plugin unloading...");

                // Phase 2: Stop process monitor
                if (this._processMonitor != null)
                {
                    this._processMonitor.ApplicationChanged -= this.OnApplicationChanged;
                    this._processMonitor.Stop();
                    this._processMonitor.Dispose();
                    this.Log.Info($"{LogTag} ProcessMonitor stopped and disposed");
                }

                // TODO: Phase 3 - Flush database
                // TODO: Phase 3 - Stop UI automation tracker
                // TODO: Phase 4 - Shutdown AI services

                this.Log.Info($"{LogTag} Plugin unloaded successfully");
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR during plugin unload: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the application exits.
        /// </summary>
        public override void RunCommand(string commandName, string parameter)
        {
            try
            {
                this.Log.Info($"{LogTag} Command received: {commandName} with parameter: {parameter}");

                // TODO: Phase 5 - Handle MCP action execution
                // TODO: Phase 6 - Handle user preferences
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR in RunCommand: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the plugin settings are updated.
        /// </summary>
        public override void ApplyApplicationResources(string name, string value)
        {
            try
            {
                this.Log.Info($"{LogTag} Resource updated: {name} = {value}");

                // TODO: Phase 4 - Handle settings changes (learning enabled, thresholds, etc.)
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR in ApplyApplicationResources: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for application/process changes.
        /// Called when the user switches to a different application.
        /// </summary>
        /// <param name="sender">The process monitor</param>
        /// <param name="info">Process information</param>
        private void OnApplicationChanged(object sender, ProcessInfo info)
        {
            try
            {
                this.Log.Info($"{LogTag} Active app changed: {info.ProcessName}");
                this.Log.Info($"{LogTag}   Window: {info.WindowTitle}");
                this.Log.Info($"{LogTag}   Path: {info.ExecutablePath}");

                // TODO: Phase 3 - Query MCP Registry for this application
                // TODO: Phase 4 - Request AI suggestions for this context
                // TODO: Phase 5 - Update Actions Ring with available actions
            }
            catch (Exception ex)
            {
                this.Log.Error($"{LogTag} ERROR in OnApplicationChanged: {ex.Message}");
            }
        }
    }
}
