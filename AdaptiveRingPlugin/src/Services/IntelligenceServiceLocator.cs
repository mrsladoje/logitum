namespace Loupedeck.AdaptiveRingPlugin.Services;

using System;
using System.IO;
using System.Reflection;

/// <summary>
/// Ensures the Python Intelligence Service script is available on disk and returns its absolute path.
/// </summary>
internal static class IntelligenceServiceLocator
{
    private const string ScriptFileName = "IntelligenceService.py";
    private const string EmbeddedResourceName = "Loupedeck.AdaptiveRingPlugin.Scripts.IntelligenceService.py";
    private static readonly Lazy<string> _scriptPath = new Lazy<string>(ExtractScriptToDisk, isThreadSafe: true);

    public static string GetScriptPath() => _scriptPath.Value;

    private static string ExtractScriptToDisk()
    {
        try
        {
            var targetDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Logitum",
                "AdaptiveRing",
                "Scripts");

            Directory.CreateDirectory(targetDirectory);

            var targetPath = Path.Combine(targetDirectory, ScriptFileName);
            var assembly = Assembly.GetExecutingAssembly();

            using var resourceStream = assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (resourceStream == null)
            {
                PluginLog.Error($"IntelligenceServiceLocator: Could not find embedded resource '{EmbeddedResourceName}'.");
                return targetPath;
            }

            using var reader = new StreamReader(resourceStream);
            var scriptContents = reader.ReadToEnd();

            if (!File.Exists(targetPath) || File.ReadAllText(targetPath) != scriptContents)
            {
                File.WriteAllText(targetPath, scriptContents);
                PluginLog.Info($"IntelligenceServiceLocator: Wrote IntelligenceService.py to {targetPath}");
            }

            return targetPath;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"IntelligenceServiceLocator: Failed to extract script: {ex.Message}");
            throw;
        }
    }
}

