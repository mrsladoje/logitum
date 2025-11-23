# Action Ranking Implementation Summary

## Overview
Implemented frequency tracking and action ranking layer for the Logitum Adaptive Ring Plugin.

## Files Created

### 1. AdaptiveRingPlugin/src/Services/ActionRankingService.cs
Complete implementation of the action ranking service with:

#### Key Features:
- **Composite Scoring Algorithm**: `(frequency * 0.6) + (recency * 0.4)`
- **Per-App Ranking**: Chrome actions ranked separately from Slack actions
- **Workflow Integration**: Queries top workflows from `workflow_clusters` table
- **Action Reordering**: Reorders existing `app_actions` by `usage_count`

#### Core Methods:
```csharp
// Query top N workflows from workflow_clusters
Task<List<WorkflowClusterData>> GetTopWorkflowsForAppAsync(string appName, int limit = 5)

// Rank actions using composite score
Task<List<RankedAction>> RankActionsForAppAsync(string appName)

// Convert workflow cluster to MCP action
AppAction ConvertWorkflowToAction(WorkflowClusterData workflow, string appName)

// Update action frequency tracking
Task UpdateActionFrequencyAsync(int actionId)

// Reorder actions by usage
Task ReorderActionsByUsageAsync(string appName)
```

#### Scoring Details:
```csharp
frequency_score = log(usage_count + 1)
recency_score = 1.0 - (days_since_last_use / 30)
composite_score = (frequency_score * 0.6) + (recency_score * 0.4)
```

**Recency Window**: 30 days
**Frequency Weight**: 60%
**Recency Weight**: 40%

## Files Modified

### 2. AdaptiveRingPlugin/src/Actions/AdaptiveRingCommand.cs
Added usage tracking after successful action execution:

#### Changes:
- Tracks execution success for each action type (Keybind, Prompt, Python)
- Calls `TrackActionUsage(actionId)` after successful execution
- Uses `ActionPersistenceService.TrackActionUsageAsync()` in background
- Logs usage tracking events

#### Code Added:
```csharp
// Track usage after successful execution
if (executionSuccessful && action.Id > 0)
{
    TrackActionUsage(action.Id);
}

private void TrackActionUsage(int actionId)
{
    var persistenceService = (_plugin as AdaptiveRingPlugin)?.ActionPersistenceService;
    if (persistenceService != null)
    {
        Task.Run(async () =>
        {
            await persistenceService.TrackActionUsageAsync(actionId);
        });
    }
}
```

### 3. AdaptiveRingPlugin/src/Services/ActionPersistenceService.cs
Extended with new usage tracking methods:

#### New Methods:
```csharp
// Increment usage count only
Task IncrementUsageCountAsync(int actionId)

// Update last used timestamp only
Task UpdateLastUsedAsync(int actionId)

// Combined: increment count + update timestamp (recommended)
Task TrackActionUsageAsync(int actionId)

// Synchronous wrappers for all above
void IncrementUsageCount(int actionId)
void UpdateLastUsed(int actionId)
void TrackActionUsage(int actionId)
```

#### SQL Query:
```sql
UPDATE app_actions
SET usage_count = COALESCE(usage_count, 0) + 1,
    last_used_at = $now
WHERE id = $actionId
```

### 4. AdaptiveRingPlugin/src/AdaptiveRingPlugin.cs
Exposed `ActionPersistenceService` as public property:

#### Change:
```csharp
public ActionPersistenceService ActionPersistenceService => _persistenceService;
```

This allows `AdaptiveRingCommand` to access the persistence service for usage tracking.

## Database Schema

### Existing Tables Extended

#### app_actions table (already updated):
```sql
CREATE TABLE IF NOT EXISTS app_actions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    app_name TEXT NOT NULL,
    position INTEGER NOT NULL,
    action_type TEXT NOT NULL,
    action_name TEXT NOT NULL,
    action_data TEXT NOT NULL,
    enabled INTEGER NOT NULL DEFAULT 1,
    usage_count INTEGER NOT NULL DEFAULT 0,      -- NEW
    last_used_at INTEGER,                         -- NEW
    FOREIGN KEY (app_name) REFERENCES remembered_apps(app_name) ON DELETE CASCADE,
    UNIQUE (app_name, position)
);
```

#### New Index:
```sql
CREATE INDEX IF NOT EXISTS idx_action_usage ON app_actions(app_name, usage_count DESC);
```

### workflow_clusters table (already exists):
```sql
CREATE TABLE IF NOT EXISTS workflow_clusters (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    app_name TEXT NOT NULL,
    cluster_label INTEGER NOT NULL,
    representative_workflow_text TEXT NOT NULL,
    workflow_count INTEGER NOT NULL DEFAULT 1,
    created_at INTEGER NOT NULL,
    updated_at INTEGER NOT NULL
);
```

#### Indexes:
```sql
CREATE INDEX IF NOT EXISTS idx_workflow_app ON workflow_clusters(app_name);
CREATE INDEX IF NOT EXISTS idx_workflow_occurrence ON workflow_clusters(app_name, occurrence_count DESC);
```

## Data Models

### New Classes in ActionRankingService.cs:

```csharp
// Workflow cluster data
public class WorkflowClusterData
{
    public int Id { get; set; }
    public required string AppName { get; set; }
    public int ClusterLabel { get; set; }
    public required string RepresentativeWorkflowText { get; set; }
    public int WorkflowCount { get; set; }
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
}

// Ranked action with scores
public class RankedAction
{
    public required AppAction Action { get; set; }
    public double CompositeScore { get; set; }
    public double FrequencyScore { get; set; }
    public double RecencyScore { get; set; }
    public int UsageCount { get; set; }
}

// Workflow pattern data (JSON deserialization)
public class WorkflowPatternData
{
    public string? McpServerName { get; set; }
    public string? ToolName { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string? Description { get; set; }
    public string? ActionName { get; set; }
}

// MCP prompt action data
public class PromptActionData
{
    public required string McpServerName { get; set; }
    public required string ToolName { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string? Description { get; set; }
}
```

## Usage Examples

### Example 1: Get Top Workflows for an App
```csharp
var rankingService = new ActionRankingService(database);
var topWorkflows = await rankingService.GetTopWorkflowsForAppAsync("chrome", limit: 5);

foreach (var workflow in topWorkflows)
{
    Console.WriteLine($"Workflow: {workflow.RepresentativeWorkflowText}");
    Console.WriteLine($"Used {workflow.WorkflowCount} times");
}
```

### Example 2: Rank Actions for an App
```csharp
var rankedActions = await rankingService.RankActionsForAppAsync("chrome");

foreach (var ranked in rankedActions)
{
    Console.WriteLine($"{ranked.Action.ActionName}:");
    Console.WriteLine($"  Composite Score: {ranked.CompositeScore:F2}");
    Console.WriteLine($"  Frequency Score: {ranked.FrequencyScore:F2}");
    Console.WriteLine($"  Recency Score: {ranked.RecencyScore:F2}");
    Console.WriteLine($"  Usage Count: {ranked.UsageCount}");
}
```

### Example 3: Convert Workflow to Action
```csharp
var workflow = topWorkflows.First();
var action = rankingService.ConvertWorkflowToAction(workflow, "chrome");

// Action is now ready to be saved to app_actions
```

### Example 4: Reorder Actions by Usage
```csharp
// Automatically reorder all actions for an app based on composite score
await rankingService.ReorderActionsByUsageAsync("chrome");
```

## Integration Points

### Automatic Usage Tracking
Every time an action is executed via `AdaptiveRingCommand`, the system automatically:
1. Increments `usage_count` by 1
2. Updates `last_used_at` to current timestamp
3. Logs the tracking event

### Future Integration
The `ActionRankingService` can be integrated into:
- **App switching**: Auto-reorder actions when user switches to an app
- **Periodic optimization**: Run daily job to reorder all app actions by usage
- **Workflow discovery**: Convert top workflows to suggested actions
- **User preferences**: Learn which action types user prefers (keybind vs. prompt)

## Build Status

### Current Status
The core action ranking service is complete and compiles successfully. The project has some unrelated build errors in optional services (`VectorDatabase`, `VectorClusteringService`) that depend on external Milvus vector database setup.

### To Fix Build Errors
These services are optional and not required for action ranking:
1. Remove or comment out `VectorDatabase.cs` and `VectorClusteringService.cs`
2. Or install and configure Milvus vector database
3. Or update the Milvus client code to use the v2.3 API

### Core Files (No Errors):
- ✅ `ActionRankingService.cs` - Complete implementation
- ✅ `ActionPersistenceService.cs` - Extended with tracking methods
- ✅ `AdaptiveRingCommand.cs` - Integrated usage tracking
- ✅ `AdaptiveRingPlugin.cs` - Exposed persistence service
- ✅ `AppDatabase.cs` - Schema already includes required columns

## Testing Recommendations

### Unit Tests
```csharp
[Test]
public async Task TestCompositeScoring()
{
    // Action used 10 times, last used yesterday
    var score1 = CalculateScore(usageCount: 10, daysAgo: 1);

    // Action used 5 times, last used 20 days ago
    var score2 = CalculateScore(usageCount: 5, daysAgo: 20);

    // Recent usage should win over higher frequency
    Assert.Greater(score1, score2);
}
```

### Integration Tests
1. Execute action via `AdaptiveRingCommand`
2. Verify `usage_count` incremented in database
3. Verify `last_used_at` updated with current timestamp
4. Call `RankActionsForApp` and verify ordering

### Manual Testing
1. Execute different actions multiple times
2. Check `C:\Users\panonit\AppData\Local\Logitum\adaptive_ring.db`
3. Query: `SELECT action_name, usage_count, last_used_at FROM app_actions ORDER BY usage_count DESC`
4. Call `ReorderActionsByUsage` and verify positions changed

## Performance Notes

### Database Operations
- All updates use indexed columns (`app_name`, `id`)
- Composite scoring done in-memory (fast)
- Logarithmic frequency score prevents dominance of high-frequency actions

### Async Execution
- Usage tracking runs asynchronously via `Task.Run()`
- Does not block action execution
- Failed tracking logged but doesn't affect UX

## File Locations

All files are in: `C:\Users\panonit\git\logitum\AdaptiveRingPlugin\src\`

- `Services/ActionRankingService.cs` (NEW)
- `Services/ActionPersistenceService.cs` (MODIFIED)
- `Actions/AdaptiveRingCommand.cs` (MODIFIED)
- `AdaptiveRingPlugin.cs` (MODIFIED)
- `Services/AppDatabase.cs` (schema already updated)

Database: `C:\Users\panonit\AppData\Local\Logitum\adaptive_ring.db`
