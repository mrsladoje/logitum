namespace Loupedeck.AdaptiveRingPlugin.Models;

/// <summary>
/// Represents a cluster of similar workflow actions for a specific application.
/// Clusters are discovered through DBSCAN clustering of vector embeddings.
/// Matches the workflow_clusters table schema.
/// </summary>
public class WorkflowCluster
{
    /// <summary>
    /// Unique identifier for the cluster.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The application name this cluster belongs to (e.g., "chrome", "vscode").
    /// Clustering is performed separately per app.
    /// </summary>
    public required string AppName { get; set; }

    /// <summary>
    /// Numeric cluster label assigned by DBSCAN (0, 1, 2, ...).
    /// </summary>
    public int ClusterLabel { get; set; }

    /// <summary>
    /// Representative text describing the most typical workflow in this cluster.
    /// Used for display and understanding what the cluster represents.
    /// </summary>
    public required string RepresentativeWorkflowText { get; set; }

    /// <summary>
    /// Number of workflows in this cluster.
    /// Higher count indicates more common patterns.
    /// </summary>
    public int WorkflowCount { get; set; }

    /// <summary>
    /// Unix timestamp of when this cluster was created.
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    /// Unix timestamp of when this cluster was last updated.
    /// </summary>
    public long UpdatedAt { get; set; }
}
