# Next Steps: Milestone 4 - AI Workflow Suggestions

**Current Status**: ✅ Milestone 3 Complete - MCP Registry Integration Working
**Next Goal**: Use Claude API to generate context-aware workflow suggestions based on discovered MCP servers
**Branch**: `feat/process-monitoring` (current)

---

## What We're Building

An **AI Action Suggester** that:
1. Takes MCP server metadata (name, description, version)
2. Sends to Claude API with context about the active application
3. Receives 6-8 suggested common workflows/actions
4. Parses and logs suggestions (Actions Ring update in Milestone 5)
5. Caches suggestions per app to reduce API costs

**Key Insight**: Since MCP Registry doesn't return tool lists, Claude will infer likely workflows from server descriptions (e.g., "GitHub repository management" → suggests "Create PR", "Commit", "Push")

---

## Implementation Plan

### Phase 1: Claude API Client (1-2 hours)

**Files to Create**:
```
src/LogitumAdaptiveRing/
└── Services/
    ├── IClaudeClient.cs           - Interface for Claude API
    ├── ClaudeClient.cs            - Anthropic API HTTP client
    └── WorkflowSuggestion.cs      - Data model for AI suggestions
```

**API Configuration**:
- **Endpoint**: `https://api.anthropic.com/v1/messages`
- **Model**: `claude-sonnet-4-20250514` (latest Sonnet)
- **API Key**: Required (set as environment variable or config)
- **Max Tokens**: 500 (suggestions are short)
- **System Prompt**: "You are a workflow assistant. Given an MCP server description, suggest 6-8 common user actions."

**Success Criteria**:
- [ ] Can authenticate with Anthropic API
- [ ] Sends server metadata in structured prompt
- [ ] Receives JSON array of workflow suggestions
- [ ] Handles API errors (rate limits, network failures)
- [ ] Timeout: 10 seconds

---

### Phase 2: Prompt Engineering (1 hour)

**Prompt Template**:
```
You are analyzing an MCP server for workflow suggestions.

Application: {processName}
MCP Server: {serverName} v{version}
Description: {serverDescription}

Task: Suggest 6-8 common workflows a user might want to perform with this server.

Requirements:
- Each suggestion should be a short action phrase (2-4 words)
- Focus on high-frequency, practical tasks
- Order by likelihood of use (most common first)

Return ONLY a JSON array of strings:
["Action 1", "Action 2", "Action 3", ...]

Example for "github - Repository management":
["Create Pull Request", "Commit Changes", "Push to Remote", "Search Code", "Create Issue", "View Branches"]
```

**Edge Cases**:
- **No servers found**: Return generic suggestions (Screenshot, Search, Copy Text, Open Settings)
- **Multiple servers**: Combine top 3-4 actions from each
- **Unknown/niche servers**: Ask Claude to infer from description

**Success Criteria**:
- [ ] Prompt generates relevant suggestions for known servers
- [ ] Suggestions are actionable and user-friendly
- [ ] Handles edge cases gracefully
- [ ] JSON parsing succeeds >95% of the time

---

### Phase 3: Suggestion Caching (1 hour)

**Database Changes**:
```sql
-- Add new table to cache AI-generated suggestions
CREATE TABLE workflow_suggestions (
    app_name TEXT,
    server_name TEXT,
    suggestion_text TEXT,
    suggestion_order INTEGER,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (app_name, server_name, suggestion_order)
);

CREATE INDEX idx_suggestions_app ON workflow_suggestions(app_name);
```

**Caching Strategy**:
- Cache suggestions per app + server combination
- TTL: 7 days (suggestions don't change often)
- First query: Call Claude API, cache results
- Subsequent queries: Return cached suggestions instantly
- Invalidate cache when server version changes

**AppDatabase Methods to Add**:
```csharp
void CacheWorkflowSuggestions(string appName, string serverName, List<string> suggestions)
List<string> GetCachedSuggestions(string appName, string serverName)
bool AreSuggestionsCached(string appName, string serverName)
```

**Success Criteria**:
- [ ] Suggestions stored in database
- [ ] Cache lookups are <5ms
- [ ] Cache invalidation works correctly
- [ ] Database upgrade is automatic (ALTER TABLE)

---

### Phase 4: Plugin Integration (1-2 hours)

**Files to Modify**:
```
src/LogitumAdaptiveRing/
└── AdaptiveRingPlugin.cs  - Add AI suggestion logic
```

**Integration Points**:

1. **Constructor**: Initialize ClaudeClient
   ```csharp
   private ClaudeClient _claudeClient;

   public AdaptiveRingPlugin()
   {
       // Existing code...
       this._claudeClient = new ClaudeClient(apiKey: GetClaudeApiKey());
       this.Log.Info($"{LogTag} Claude API client initialized");
   }
   ```

2. **OnApplicationChanged**: Request suggestions after MCP query
   ```csharp
   private async void OnApplicationChanged(object sender, ProcessInfo info)
   {
       var servers = await this.QueryMCPServersForAppAsync(info.ProcessName);

       if (servers.Count > 0)
       {
           // NEW: Get AI suggestions
           var suggestions = await this.GetWorkflowSuggestionsAsync(info.ProcessName, servers);

           this.Log.Info($"{LogTag} AI suggested {suggestions.Count} workflows:");
           foreach (var suggestion in suggestions)
           {
               this.Log.Info($"{LogTag}   - {suggestion}");
           }
       }
   }
   ```

3. **New Method**: GetWorkflowSuggestionsAsync
   ```csharp
   private async Task<List<string>> GetWorkflowSuggestionsAsync(string appName, List<MCPServer> servers)
   {
       // Check cache first
       if (_database.AreSuggestionsCached(appName, servers[0].Name))
       {
           return _database.GetCachedSuggestions(appName, servers[0].Name);
       }

       // Query Claude API
       var suggestions = await _claudeClient.GetWorkflowSuggestionsAsync(appName, servers[0]);

       // Cache results
       _database.CacheWorkflowSuggestions(appName, servers[0].Name, suggestions);

       return suggestions;
   }
   ```

**Configuration**:
- API key from environment variable: `ANTHROPIC_API_KEY`
- Fallback: Hardcoded key in PluginManifest.json (for testing)
- Error handling: Log failure, return empty list

**Success Criteria**:
- [ ] Plugin queries Claude after finding MCP servers
- [ ] Suggestions appear in logs
- [ ] Cache works (second query is instant)
- [ ] Handles API failures gracefully
- [ ] No crashes if API key is missing

---

### Phase 5: Test Console Enhancement (30 min)

**Files to Modify**:
```
src/LogitumAdaptiveRing.TestConsole/
└── Program.cs  - Add --test-ai mode
```

**New Test Mode**:
```bash
dotnet run --test-ai
```

**What It Tests**:
1. Queries MCP Registry for "github"
2. Sends to Claude API for suggestions
3. Displays results:
   ```
   🔍 Testing AI Suggestions for 'github'...
   📦 Found server: github v1.0.0
   🤖 Claude API suggests:
      1. Create Pull Request
      2. Commit Changes
      3. Push to Remote
      4. Search Code
      5. Create Issue
      6. View Branches

   ✅ AI test complete!
   ```

**Success Criteria**:
- [ ] Test mode queries API successfully
- [ ] Displays formatted suggestions
- [ ] Shows API response time
- [ ] Handles no-server edge case

---

## API Cost Estimation

**Per Query**:
- Input tokens: ~150 (prompt + metadata)
- Output tokens: ~100 (6-8 suggestions)
- Cost: ~$0.001 per query (Sonnet 4)

**With Caching** (7-day TTL):
- First week: ~$0.10 (100 unique app queries)
- Ongoing: ~$0.01/week (cache hits)

**Total Milestone 4 Budget**: <$1 for testing

---

## Testing Instructions

### Option A: Test AI Client Standalone

```bash
cd src/LogitumAdaptiveRing.TestConsole
dotnet run --test-ai
```

**What to test**:
- [ ] API authentication works
- [ ] Suggestions are relevant
- [ ] JSON parsing succeeds
- [ ] Network errors handled
- [ ] Response time <10s

---

### Option B: Test with Logitech Options+

**Setup**:
```bash
# Set API key
setx ANTHROPIC_API_KEY "sk-ant-your-key-here"

# Rebuild plugin
dotnet build
```

**Test Flow**:
1. Open Options+
2. Switch to GitHub Desktop
3. Check logs: Should see MCP query + AI suggestions
4. Switch to VS Code
5. Check logs: Should see cached suggestions (instant)

**Expected Log Output**:
```
[MCP-AdaptiveRing] Active app changed: GitHub
[MCP-AdaptiveRing] Found 1 MCP server(s) for GitHub:
[MCP-AdaptiveRing]   - github v1.0.0: GitHub repository management
[MCP-AdaptiveRing] Querying Claude API for workflow suggestions...
[MCP-AdaptiveRing] AI suggested 6 workflows:
[MCP-AdaptiveRing]   - Create Pull Request
[MCP-AdaptiveRing]   - Commit Changes
[MCP-AdaptiveRing]   - Push to Remote
[MCP-AdaptiveRing]   - Search Code
[MCP-AdaptiveRing]   - Create Issue
[MCP-AdaptiveRing]   - View Branches
```

---

### Option C: Database Inspection

```bash
sqlite3 %LOCALAPPDATA%/Logitum/adaptivering.db

# Check cached suggestions
SELECT * FROM workflow_suggestions;

# Check app-to-suggestion mapping
SELECT app_name, server_name, suggestion_text, suggestion_order
FROM workflow_suggestions
ORDER BY app_name, suggestion_order;
```

---

## Performance Targets

**API Response Time**:
- First query (no cache): <10s (includes API latency)
- Cached query: <5ms (database lookup)
- Timeout threshold: 10s

**Database Performance**:
- Cache lookup: <5ms
- Cache insert: <10ms
- Database size after 100 apps: ~1.5MB (including suggestions)

**Memory Usage**:
- Claude API client: ~5MB
- Additional overhead: ~2MB

---

## Error Handling Strategy

**API Errors**:
```csharp
public async Task<List<string>> GetWorkflowSuggestionsAsync(string appName, MCPServer server)
{
    try
    {
        var response = await _httpClient.PostAsync(endpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Warning($"Claude API returned {response.StatusCode}");
            return GetFallbackSuggestions();
        }

        return ParseSuggestions(response);
    }
    catch (HttpRequestException ex)
    {
        _logger.Warning($"Network error: {ex.Message}");
        return GetFallbackSuggestions();
    }
    catch (JsonException ex)
    {
        _logger.Error($"JSON parse error: {ex.Message}");
        return GetFallbackSuggestions();
    }
}

private List<string> GetFallbackSuggestions()
{
    // Generic suggestions when AI fails
    return new List<string>
    {
        "Screenshot Window",
        "Copy Text",
        "Search Google",
        "Open Settings"
    };
}
```

**Graceful Degradation**:
- API down → Use fallback suggestions
- No API key → Use fallback suggestions
- Timeout → Use cached suggestions (even if stale)
- Never crash the plugin

---

## Code Quality Checklist

Before committing Milestone 4:
- [ ] All methods have XML documentation
- [ ] Async/await used correctly (no blocking)
- [ ] HTTP client disposed properly
- [ ] Database transactions for suggestion inserts
- [ ] JSON serialization handles missing fields
- [ ] API key never logged or exposed
- [ ] All errors handled gracefully
- [ ] Unit tests for prompt generation (optional)
- [ ] Integration test with real API

---

## File Checklist

**To Create**:
- [ ] `src/LogitumAdaptiveRing/Services/IClaudeClient.cs`
- [ ] `src/LogitumAdaptiveRing/Services/ClaudeClient.cs`
- [ ] `src/LogitumAdaptiveRing/Services/WorkflowSuggestion.cs`

**To Modify**:
- [ ] `src/LogitumAdaptiveRing/AdaptiveRingPlugin.cs` - Add Claude integration
- [ ] `src/LogitumAdaptiveRing/Data/AppDatabase.cs` - Add suggestion caching methods
- [ ] `src/LogitumAdaptiveRing/Data/DatabaseSchema.sql` - Add workflow_suggestions table
- [ ] `src/LogitumAdaptiveRing.TestConsole/Program.cs` - Add --test-ai mode

**NuGet Packages to Add**:
None (System.Net.Http and System.Text.Json already available)

---

## Success Metrics

**Milestone 4 is complete when**:
- [ ] ClaudeClient can authenticate and query API
- [ ] Suggestions are parsed from JSON response
- [ ] Database caches suggestions (7-day TTL)
- [ ] Plugin queries Claude after finding MCP servers
- [ ] Logs show AI-suggested workflows
- [ ] Handles errors gracefully (no crashes)
- [ ] Test console can test AI suggestions standalone
- [ ] Build succeeds with 0 errors

---

## Common MCP Servers to Test With

**Definitely Work** (verified in Milestone 3):
1. **github** → Expect: Create PR, Commit, Push, Search, Issues
2. **slack** → Expect: Send Message, Search, Mention, Create Channel
3. **postgres** → Expect: Query Database, View Tables, Backup, Execute SQL
4. **docker** → Expect: Start Container, Stop Container, View Logs, Build Image

**Edge Cases to Test**:
- **notepad** (no server) → Fallback suggestions
- **chrome** (multiple servers) → Combined suggestions
- **unknown-app** → Graceful handling

---

## Next Milestone Preview

**Milestone 5: Actions Ring Population**
- Map AI suggestions to Actions Ring slots
- Update ring when app context changes
- Handle user clicks on ring actions
- Execute MCP tools via API/CLI
- Display action feedback to user

---

## Architecture Diagram

```
┌──────────────────────────────────────────┐
│    Process Monitor (M2) + MCP (M3)       │
│  Detects: "User switched to GitHub"     │
│  Query: Found MCP server "github"        │
└──────────────┬───────────────────────────┘
               │
               ↓
┌──────────────────────────────────────────┐
│       Claude API Client (M4 - NEW)       │
│  1. Check cache: Do we have suggestions? │
│  2. If not: Send to Claude API           │
│     Prompt: "github - Repository mgmt"   │
│  3. Parse response → List<string>        │
│  4. Cache in SQLite for 7 days           │
└──────────────┬───────────────────────────┘
               │
               ↓
┌──────────────────────────────────────────┐
│        SQLite Database (M3 + M4)         │
│  New Table: workflow_suggestions         │
│  TTL: 7 days                             │
└──────────────┬───────────────────────────┘
               │
               ↓
┌──────────────────────────────────────────┐
│         Plugin Logs Output               │
│  [MCP-AdaptiveRing] AI suggested 6 workflows │
│    - Create Pull Request                 │
│    - Commit Changes                      │
│    - Push to Remote                      │
│    (... ready for Actions Ring M5)       │
└──────────────────────────────────────────┘
```

---

**Ready to start?** Get your Anthropic API key, set the environment variable, and begin implementing the Claude client! 🚀

**Estimated Time**: 4-6 hours for complete Milestone 4 implementation
**Dependencies**: Milestone 3 must be working (MCP Registry queries)
**Blocker Risk**: Low (Claude API is stable, we have fallback suggestions)
**API Cost**: <$1 for testing phase
