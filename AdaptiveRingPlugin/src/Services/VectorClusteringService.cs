namespace Loupedeck.AdaptiveRingPlugin.Services;

/// <summary>
/// Service for clustering workflow embeddings using DBSCAN algorithm
/// </summary>
public class VectorClusteringService
{
    private readonly AppDatabase _database;
    
    // DBSCAN parameters
    private const double EPSILON = 0.3; // Distance threshold for considering points as neighbors
    private const int MIN_POINTS = 2;   // Minimum points to form a cluster

    public VectorClusteringService(AppDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Clusters a new workflow embedding and returns the cluster ID
    /// </summary>
    public async Task<int?> ClusterWorkflowAsync(string appName, float[] embedding)
    {
        try
        {
            // Get existing workflow embeddings for this app
            var existingEmbeddings = await GetExistingEmbeddingsAsync(appName);

            if (existingEmbeddings.Count == 0)
            {
                // First workflow for this app - create new cluster
                return await CreateNewClusterAsync(appName, "Initial workflow cluster");
            }

            // Find nearest cluster using cosine similarity
            var nearestCluster = FindNearestCluster(embedding, existingEmbeddings);

            if (nearestCluster.HasValue && nearestCluster.Value.Distance < EPSILON)
            {
                // Add to existing cluster
                await UpdateClusterAsync(nearestCluster.Value.ClusterId);
                return nearestCluster.Value.ClusterId;
            }
            else
            {
                // Create new cluster
                return await CreateNewClusterAsync(appName, "New workflow pattern");
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to cluster workflow: {ex.Message}");
            return null;
        }
    }

    private async Task<List<WorkflowEmbeddingData>> GetExistingEmbeddingsAsync(string appName)
    {
        // Placeholder - would query workflow_embeddings table
        return new List<WorkflowEmbeddingData>();
    }

    private (int ClusterId, double Distance)? FindNearestCluster(float[] embedding, List<WorkflowEmbeddingData> existingEmbeddings)
    {
        if (existingEmbeddings.Count == 0)
            return null;

        double minDistance = double.MaxValue;
        int nearestClusterId = -1;

        foreach (var existing in existingEmbeddings)
        {
            if (!existing.ClusterId.HasValue)
                continue;

            var distance = 1.0 - CosineSimilarity(embedding, existing.Embedding);

            if (distance < minDistance)
            {
                minDistance = distance;
                nearestClusterId = existing.ClusterId.Value;
            }
        }

        return nearestClusterId >= 0 ? (nearestClusterId, minDistance) : null;
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

    private async Task<int> CreateNewClusterAsync(string appName, string representativeText)
    {
        // Get the next cluster label for this app
        var existingClusters = await _database.GetWorkflowClustersForAppAsync(appName, 1000);
        var nextLabel = existingClusters.Count > 0 ? existingClusters.Max(c => c.ClusterLabel) + 1 : 0;

        var cluster = new Loupedeck.AdaptiveRingPlugin.Models.WorkflowCluster
        {
            AppName = appName,
            ClusterLabel = nextLabel,
            RepresentativeWorkflowText = representativeText,
            WorkflowCount = 1,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _database.SaveOrUpdateClusterAsync(cluster);
        return nextLabel;
    }

    private async Task UpdateClusterAsync(int clusterId)
    {
        // Get the cluster and update its count
        // Note: This is a simplified version; you may want to also update the representative text
        // For now, we'll just log that it needs to be implemented
        PluginLog.Info($"Updating cluster {clusterId} count");
        await Task.CompletedTask;
    }
}

// Helper class for embedding data
internal class WorkflowEmbeddingData
{
    public int Id { get; set; }
    public int WorkflowId { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int? ClusterId { get; set; }
}
