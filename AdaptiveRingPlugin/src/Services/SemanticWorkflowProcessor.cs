using System.Text.Json;
using Loupedeck.AdaptiveRingPlugin.Models;

namespace Loupedeck.AdaptiveRingPlugin.Services;

/// <summary>
/// Processes UI interactions to identify semantic workflows using AI
/// </summary>
public class SemanticWorkflowProcessor : IDisposable
{
    private readonly AppDatabase _database;
    private readonly GeminiActionSuggestor _geminiSuggestor;
    private readonly VoyageAIClient _voyageClient;
    private readonly VectorClusteringService _clusteringService;
    private System.Threading.Timer? _processingTimer;
    private bool _isRunning;

    private const int INTERACTION_WINDOW_MINUTES = 15;

    public SemanticWorkflowProcessor(
        AppDatabase database,
        GeminiActionSuggestor geminiSuggestor,
        VoyageAIClient voyageClient,
        VectorClusteringService clusteringService)
    {
        _database = database;
        _geminiSuggestor = geminiSuggestor;
        _voyageClient = voyageClient;
        _clusteringService = clusteringService;
    }

    public void Start()
    {
        if (_isRunning)
            return;

        _isRunning = true;

        // Start processing timer - runs every 15 minutes
        _processingTimer = new System.Threading.Timer(
            async _ => await ProcessRecentInteractionsAsync(),
            null,
            TimeSpan.FromMinutes(1), // First run after 1 minute
            TimeSpan.FromMinutes(15) // Then every 15 minutes
        );

        PluginLog.Info("SemanticWorkflowProcessor started");
    }

    public void Stop()
    {
        _isRunning = false;
        _processingTimer?.Dispose();
        PluginLog.Info("SemanticWorkflowProcessor stopped");
    }

    /// <summary>
    /// Processes recent UI interactions to identify workflows
    /// </summary>
    private async Task ProcessRecentInteractionsAsync()
    {
        try
        {
            // Get all apps that have recent interactions
            var apps = await GetAppsWithRecentInteractionsAsync();

            foreach (var appName in apps)
            {
                await ProcessInteractionsForAppAsync(appName);
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to process recent interactions: {ex.Message}");
        }
    }

    private async Task<List<string>> GetAppsWithRecentInteractionsAsync()
    {
        // This would need a database query to get distinct apps
        // For now, return empty list as placeholder
        return new List<string>();
    }

    /// <summary>
    /// Processes interactions for a specific app to identify workflows
    /// </summary>
    private async Task ProcessInteractionsForAppAsync(string appName)
    {
        try
        {
            // Get recent interactions
            var interactions = await _database.GetRecentUIInteractionsAsync(appName, INTERACTION_WINDOW_MINUTES);

            if (interactions.Count < 3) // Need at least 3 interactions to form a workflow
                return;

            // Use Gemini to analyze interactions and identify workflows
            var workflowText = await AnalyzeInteractionsAsync(interactions);

            if (string.IsNullOrEmpty(workflowText))
                return;

            // Save the workflow
            var workflow = new SemanticWorkflow
            {
                AppName = appName,
                WorkflowText = workflowText,
                RawInteractionIds = JsonSerializer.Serialize(interactions.Select(i => i.Id).ToList()),
                Confidence = 0.8 // Placeholder confidence score
            };

            var workflowId = await _database.SaveSemanticWorkflowAsync(workflow);

            // Generate embedding for the workflow
            var embedding = await _voyageClient.GetEmbeddingAsync(workflowText);

            if (embedding != null)
            {
                // Cluster the workflow
                var clusterId = await _clusteringService.ClusterWorkflowAsync(appName, embedding);

                // Save embedding
                await _database.SaveWorkflowEmbeddingAsync(workflowId, appName, embedding, clusterId);

                PluginLog.Info($"Identified workflow for {appName}: {workflowText}");
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to process interactions for {appName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Uses Gemini AI to analyze interactions and identify workflow patterns
    /// </summary>
    private async Task<string> AnalyzeInteractionsAsync(List<UIInteraction> interactions)
    {
        try
        {
            var interactionSummary = string.Join("\n", interactions.Select(i =>
                $"- {i.InteractionType} on {i.ElementName ?? "unknown element"} at {DateTimeOffset.FromUnixTimeSeconds(i.Timestamp):HH:mm:ss}"));

            var prompt = $@"Analyze the following UI interactions and identify if they represent a meaningful workflow.
If they do, provide a concise description of the workflow (1-2 sentences).
If they don't form a meaningful workflow, respond with 'NO_WORKFLOW'.

Interactions:
{interactionSummary}

Workflow description:";

            // This would call Gemini API - for now return placeholder
            // In production, integrate with GeminiActionSuggestor or similar
            return string.Empty;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to analyze interactions: {ex.Message}");
            return string.Empty;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
