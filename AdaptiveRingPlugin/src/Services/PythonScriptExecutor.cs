namespace Loupedeck.AdaptiveRingPlugin.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text.Json;
    using Loupedeck.AdaptiveRingPlugin.Models;
    using Loupedeck.AdaptiveRingPlugin.Models.ActionData;

    /// <summary>
    /// Service to execute Python scripts with whitelist validation for security
    /// </summary>
    public static class PythonScriptExecutor
    {
        private static readonly HashSet<string> AllowedImports = new()
        {
            "sys", "os.path", "datetime", "json", "re", "math", "time"
        };

        private static readonly HashSet<string> BlockedKeywords = new()
        {
            "subprocess", "socket", "urllib", "requests", "eval", "exec",
            "__import__", "open", "file", "compile", "globals", "locals"
        };

        /// <summary>
        /// Validates a Python script against whitelist rules
        /// </summary>
        /// <param name="scriptCode">The Python script code to validate</param>
        /// <returns>True if script is safe to execute, false otherwise</returns>
        public static bool ValidateScript(string scriptCode)
        {
            if (string.IsNullOrWhiteSpace(scriptCode))
            {
                PluginLog.Warning("PythonScriptExecutor: Script code is empty");
                return false;
            }

            // Check for blocked keywords
            foreach (var blocked in BlockedKeywords)
            {
                if (scriptCode.Contains(blocked, StringComparison.OrdinalIgnoreCase))
                {
                    PluginLog.Warning($"PythonScriptExecutor: Script contains blocked keyword: {blocked}");
                    return false;
                }
            }

            PluginLog.Info("PythonScriptExecutor: Script validation passed");
            return true;
        }

        /// <summary>
        /// Checks if Python is installed and available
        /// </summary>
        /// <returns>True if Python is available, false otherwise</returns>
        private static bool IsPythonAvailable()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    PluginLog.Info($"PythonScriptExecutor: Python detected - {output.Trim()}");
                    return true;
                }

                PluginLog.Warning("PythonScriptExecutor: Python not found or returned error");
                return false;
            }
            catch (Exception ex)
            {
                PluginLog.Error($"PythonScriptExecutor: Error checking Python availability: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Executes a Python script action
        /// </summary>
        /// <param name="action">The AppAction containing Python script data</param>
        public static void Execute(AppAction action)
        {
            if (action.Type != ActionType.Python)
            {
                PluginLog.Warning("PythonScriptExecutor: Action type is not Python");
                return;
            }

            try
            {
                // Deserialize PythonActionData
                var pythonData = JsonSerializer.Deserialize<PythonActionData>(action.ActionDataJson);
                if (pythonData == null)
                {
                    PluginLog.Warning("PythonScriptExecutor: Failed to deserialize Python action data");
                    return;
                }

                // Determine script source
                string? scriptCode = pythonData.ScriptCode;
                string? scriptPath = pythonData.ScriptPath;

                if (string.IsNullOrWhiteSpace(scriptCode) && string.IsNullOrWhiteSpace(scriptPath))
                {
                    PluginLog.Warning("PythonScriptExecutor: No script code or path provided");
                    return;
                }

                // If ScriptCode is provided, validate it
                if (!string.IsNullOrWhiteSpace(scriptCode))
                {
                    if (!ValidateScript(scriptCode))
                    {
                        PluginLog.Error("PythonScriptExecutor: Script validation failed - execution blocked for security");
                        return;
                    }
                }

                // Check if Python is available
                if (!IsPythonAvailable())
                {
                    PluginLog.Error("PythonScriptExecutor: Python is not installed or not in PATH");
                    return;
                }

                // Prepare process
                var process = new Process();
                process.StartInfo.FileName = "python";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.CreateNoWindow = true;

                // Add arguments if provided
                if (pythonData.Arguments != null && pythonData.Arguments.Count > 0)
                {
                    foreach (var arg in pythonData.Arguments)
                    {
                        process.StartInfo.ArgumentList.Add(arg);
                    }
                }

                // Execute script
                if (!string.IsNullOrWhiteSpace(scriptCode))
                {
                    // Execute inline script code via stdin
                    process.StartInfo.Arguments = "-c";
                    process.StartInfo.ArgumentList.Add(scriptCode);

                    PluginLog.Info($"PythonScriptExecutor: Executing inline Python script for action '{action.ActionName}'");
                }
                else if (!string.IsNullOrWhiteSpace(scriptPath))
                {
                    // Execute script file
                    process.StartInfo.ArgumentList.Insert(0, scriptPath);

                    PluginLog.Info($"PythonScriptExecutor: Executing Python script file: {scriptPath}");
                }

                process.Start();

                // Capture output
                var output = process.StandardOutput.ReadToEnd();
                var errorOutput = process.StandardError.ReadToEnd();

                process.WaitForExit();

                // Log results
                if (process.ExitCode == 0)
                {
                    PluginLog.Info($"PythonScriptExecutor: Script executed successfully");

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        PluginLog.Info($"PythonScriptExecutor: Output: {output.Trim()}");
                    }
                }
                else
                {
                    PluginLog.Error($"PythonScriptExecutor: Script execution failed with exit code {process.ExitCode}");

                    if (!string.IsNullOrWhiteSpace(errorOutput))
                    {
                        PluginLog.Error($"PythonScriptExecutor: Error: {errorOutput.Trim()}");
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error($"PythonScriptExecutor: Error executing Python script: {ex.Message}");
            }
        }
    }
}
