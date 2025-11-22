// ===================================================================
// STUB CLASSES FOR DEVELOPMENT WITHOUT LOGITECH OPTIONS+ INSTALLED
// ===================================================================
// These classes allow compilation without PluginApi.dll
// They will be replaced by the real SDK when Logitech Options+ is installed
// ===================================================================

#if NO_PLUGIN_API

namespace Loupedeck.LogitumAdaptiveRing
{
    using System;

    /// <summary>
    /// Stub logger for development without SDK
    /// </summary>
    public class PluginLogger
    {
        public void Info(string message) => Console.WriteLine($"[INFO] {message}");
        public void Warning(string message) => Console.WriteLine($"[WARN] {message}");
        public void Error(string message) => Console.WriteLine($"[ERROR] {message}");
        public void Debug(string message) => Console.WriteLine($"[DEBUG] {message}");
    }

    /// <summary>
    /// Stub base plugin class for development without SDK
    /// This mimics the real Plugin class from PluginApi.dll
    /// </summary>
    public abstract class Plugin
    {
        protected PluginLogger Log { get; } = new PluginLogger();
        protected System.Reflection.Assembly Assembly => this.GetType().Assembly;

        // Plugin properties
        public abstract bool UsesApplicationApiOnly { get; }
        public abstract bool HasNoApplication { get; }

        // Plugin lifecycle methods
        public abstract void Load();
        public abstract void Unload();

        // Optional overridable methods
        public virtual void RunCommand(string commandName, string parameter) { }
        public virtual void ApplyApplicationResources(string name, string value) { }
    }

    /// <summary>
    /// Stub ClientApplication for development
    /// </summary>
    public abstract class ClientApplication
    {
        protected abstract string GetProcessName();
        protected abstract string GetBundleName();
        public abstract ClientApplicationStatus GetApplicationStatus();
    }

    /// <summary>
    /// Application status enum
    /// </summary>
    public enum ClientApplicationStatus
    {
        Unknown,
        Running,
        Stopped
    }
}

#endif
