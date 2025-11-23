using Loupedeck.AdaptiveRingPlugin.Models;

namespace Loupedeck.AdaptiveRingPlugin.Services;

/// <summary>
/// Service for ranking and prioritizing actions based on usage patterns and workflows
/// </summary>
public class ActionRankingService
{
    private readonly AppDatabase _database;
    private readonly VoyageAIClient _voyageClient;

    public ActionRankingService(AppDatabase database, VoyageAIClient voyageClient)
    {
        _database = database;
        _voyageClient = voyageClient;
    }

    /// <summary>
    /// Ranks actions for a given app based on usage patterns and semantic relevance
    /// </summary>
    public async Task<List<AppAction>> RankActionsAsync(string appName, List<AppAction> actions)
    {
        try
        {
            // Get workflow clusters for this app
            var clusters = await _database.GetWorkflowClustersForAppAsync(appName, 10);

            if (clusters.Count == 0)
            {
                // No workflow data yet, rank by usage only
                return RankByUsage(actions);
            }

            // Score each action based on semantic relevance to workflows
            var scoredActions = new List<(AppAction Action, double Score)>();

            foreach (var action in actions)
            {
                var score = await CalculateActionScoreAsync(action, clusters);
                scoredActions.Add((action, score));
            }

            // Sort by score descending
            return scoredActions
                .OrderByDescending(x => x.Score)
                .Select(x => x.Action)
                .ToList();
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to rank actions: {ex.Message}");
            return actions; // Return original order on error
        }
    }

    private List<AppAction> RankByUsage(List<AppAction> actions)
    {
        // Simple ranking by usage count - would need to read from database
        return actions.OrderBy(a => a.Position).ToList();
    }

    private async Task<double> CalculateActionScoreAsync(AppAction action, List<WorkflowCluster> clusters)
    {
        try
        {
            // Get embedding for the action
            var actionEmbedding = await _voyageClient.GetEmbeddingAsync(action.ActionName);

            if (actionEmbedding == null)
                return 0;

            // Calculate semantic similarity to each cluster
            double maxSimilarity = 0;

            foreach (var cluster in clusters)
            {
                var clusterEmbedding = await _voyageClient.GetEmbeddingAsync(cluster.RepresentativeWorkflowText);

                if (clusterEmbedding != null)
                {
                    var similarity = CosineSimilarity(actionEmbedding, clusterEmbedding);
                    
                    // Weight by cluster frequency
                    var weightedSimilarity = similarity * Math.Log(1 + cluster.WorkflowCount);
                    
                    maxSimilarity = Math.Max(maxSimilarity, weightedSimilarity);
                }
            }

            return maxSimilarity;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to calculate action score: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Calculates cosine similarity between two vectors
    /// </summary>
    private double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Suggests new actions based on identified workflows
    /// </summary>
    public async Task<List<string>> SuggestActionsAsync(string appName)
    {
        try
        {
            var clusters = await _database.GetWorkflowClustersForAppAsync(appName, 5);
            
            // Return the most common workflow patterns as suggestions
            return clusters
                .OrderByDescending(c => c.WorkflowCount)
                .Select(c => c.RepresentativeWorkflowText)
                .ToList();
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to suggest actions: {ex.Message}");
            return new List<string>();
        }
    }
}
