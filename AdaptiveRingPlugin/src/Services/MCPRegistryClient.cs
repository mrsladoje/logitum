using System.Net.Http;
using System.Text.Json;
using Loupedeck.AdaptiveRingPlugin.Models;

namespace Loupedeck.AdaptiveRingPlugin.Services;

public class MCPRegistryClient
{
    private readonly HttpClient _httpClient;
    private readonly AppDatabase _database;

    private const string TOOLSDK_URL = "https://toolsdk-ai.github.io/toolsdk-mcp-registry/indexes/packages-list.json";
    private const string OFFICIAL_REGISTRY_URL = "https://registry.modelcontextprotocol.io/v0/servers";
    private const string GLAMA_URL = "https://glama.ai/api/mcp/v1/servers";

    public MCPRegistryClient(AppDatabase database)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _database = database;
    }

    public async Task<MCPServerData?> FindServerAsync(string appName)
    {
        PluginLog.Info($"Looking for MCP server for app: {appName}");

        // 1. Check DB cache first
        var cached = await _database.GetCachedAsync(appName);
        if (cached != null)
        {
            PluginLog.Info($"Cache HIT: Found {cached.ServerName} from {cached.RegistrySource}");
            return cached;
        }

        PluginLog.Info("Cache MISS: Querying registries...");

        // 2. Search ToolSDK index (local, fast)
        var toolsdkResult = await SearchToolSDKAsync(appName);
        if (toolsdkResult != null)
        {
            PluginLog.Info($"Found in ToolSDK: {toolsdkResult.ServerName}");
            await _database.SaveResultAsync(appName, "ToolSDK", toolsdkResult);
            return toolsdkResult;
        }

        // 3. Query Official Registry API
        var officialResult = await QueryOfficialRegistryAsync(appName);
        if (officialResult != null)
        {
            PluginLog.Info($"Found in Official Registry: {officialResult.ServerName}");
            await _database.SaveResultAsync(appName, "Official", officialResult);
            return officialResult;
        }

        // 4. Query Glama API
        var glamaResult = await QueryGlamaAsync(appName);
        if (glamaResult != null)
        {
            PluginLog.Info($"Found in Glama: {glamaResult.ServerName}");
            await _database.SaveResultAsync(appName, "Glama", glamaResult);
            return glamaResult;
        }

        // 5. Mark as not found
        PluginLog.Info($"No MCP server found for {appName} in any registry");
        await _database.MarkAsNotFoundAsync(appName);
        return null;
    }

    public async Task InitializeToolSDKIndexAsync()
    {
        // Check if index is fresh
        if (await _database.IsToolSDKIndexFreshAsync())
        {
            PluginLog.Info("ToolSDK index is fresh, skipping download");
            return;
        }

        PluginLog.Info("Downloading ToolSDK index...");

        try
        {
            var response = await _httpClient.GetStringAsync(TOOLSDK_URL);
            var packages = JsonSerializer.Deserialize<Dictionary<string, ToolSDKPackage>>(response);

            if (packages != null && packages.Count > 0)
            {
                PluginLog.Info($"Downloaded {packages.Count} packages from ToolSDK");
                await _database.SaveToolSDKIndexAsync(packages);
                PluginLog.Info("ToolSDK index saved to database");
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to download ToolSDK index: {ex.Message}");
        }
    }

    private async Task<MCPServerData?> SearchToolSDKAsync(string appName)
    {
        try
        {
            var results = await _database.SearchToolSDKIndexAsync(appName);
            if (results.Count == 0)
                return null;

            if (results.Count == 1)
                return results[0];

            PluginLog.Info($"Found {results.Count} ToolSDK matches for '{appName}', selecting most general...");
            return SelectMostGeneralServer(results, appName);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"ToolSDK search error: {ex.Message}");
            return null;
        }
    }

    private async Task<MCPServerData?> QueryOfficialRegistryAsync(string appName)
    {
        try
        {
            var searchTerms = GetSearchVariants(appName);
            var allResults = new List<MCPServerData>();

            foreach (var term in searchTerms)
            {
                var url = $"{OFFICIAL_REGISTRY_URL}?search={term}&version=latest";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<OfficialRegistryResponse>(response);

                if (result?.Servers != null && result.Servers.Count > 0)
                {
                    foreach (var serverWrapper in result.Servers)
                    {
                        var server = serverWrapper.Server;
                        if (server != null)
                        {
                            allResults.Add(new MCPServerData
                            {
                                ServerName = server.Title ?? server.Name,
                                PackageName = server.Name,
                                Description = server.Description,
                                RegistrySource = "Official",
                                Category = "official",
                                Validated = true
                            });
                        }
                    }
                }
            }

            if (allResults.Count == 0)
                return null;

            if (allResults.Count == 1)
                return allResults[0];

            PluginLog.Info($"Found {allResults.Count} Official Registry matches for '{appName}', selecting most general...");
            return SelectMostGeneralServer(allResults, appName);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Official Registry query error: {ex.Message}");
        }

        return null;
    }

    private async Task<MCPServerData?> QueryGlamaAsync(string appName)
    {
        try
        {
            var searchTerms = GetSearchVariants(appName);
            var allResults = new List<MCPServerData>();

            foreach (var term in searchTerms)
            {
                var url = $"{GLAMA_URL}?search={term}";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<GlamaResponse>(response);

                if (result?.Servers != null && result.Servers.Count > 0)
                {
                    foreach (var server in result.Servers)
                    {
                        allResults.Add(new MCPServerData
                        {
                            ServerName = server.Name,
                            PackageName = $"{server.Namespace}/{server.Slug}",
                            Description = server.Description,
                            RegistrySource = "Glama",
                            Category = "glama",
                            Validated = false
                        });
                    }
                }
            }

            if (allResults.Count == 0)
                return null;

            if (allResults.Count == 1)
                return allResults[0];

            PluginLog.Info($"Found {allResults.Count} Glama matches for '{appName}', selecting most general...");
            return SelectMostGeneralServer(allResults, appName);
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Glama query error: {ex.Message}");
        }

        return null;
    }

    private List<string> GetSearchVariants(string appName)
    {
        var variants = new List<string> { appName.ToLowerInvariant() };

        // Remove .exe suffix if present
        if (appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            variants.Add(appName.Substring(0, appName.Length - 4).ToLowerInvariant());
        }

        // Common app aliases
        var aliases = new Dictionary<string, List<string>>
        {
            { "chrome", new() { "chrome", "chromium", "puppeteer", "browser" } },
            { "code", new() { "vscode", "visual-studio-code", "code" } },
            { "cursor", new() { "vscode", "visual-studio-code", "code" } },
            { "spotify", new() { "spotify", "music" } },
            { "excel", new() { "excel", "microsoft-excel", "spreadsheet" } },
            { "discord", new() { "discord", "chat" } },
            { "slack", new() { "slack", "chat", "messaging" } }
        };

        var lowerAppName = appName.ToLowerInvariant().Replace(".exe", "");
        if (aliases.ContainsKey(lowerAppName))
        {
            variants.AddRange(aliases[lowerAppName]);
        }

        return variants.Distinct().ToList();
    }

    /// <summary>
    /// Selects the most general MCP server from a list of candidates.
    /// Prefers exact matches, shorter names, and validated packages.
    /// </summary>
    private MCPServerData? SelectMostGeneralServer(List<MCPServerData> servers, string searchTerm)
    {
        if (servers == null || servers.Count == 0)
            return null;

        if (servers.Count == 1)
            return servers[0];

        var scored = servers.Select(server => new
        {
            Server = server,
            Score = CalculateGeneralityScore(server, searchTerm)
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        PluginLog.Info($"Ranked {scored.Count} servers:");
        foreach (var item in scored.Take(5))
        {
            PluginLog.Info($"  - {item.Server.PackageName} (score: {item.Score})");
        }

        var topScore = scored.First().Score;
        if (topScore < 0 && scored.Count > 1)
        {
            PluginLog.Warning($"Top match has negative score ({topScore}). All candidates may be too specific.");
        }

        return scored.First().Server;
    }

    /// <summary>
    /// Calculates a generality score for an MCP server.
    /// Higher score = more general/preferred.
    /// </summary>
    private int CalculateGeneralityScore(MCPServerData server, string searchTerm)
    {
        var score = 0;
        var packageName = server.PackageName.ToLowerInvariant();
        var normalizedSearch = searchTerm.ToLowerInvariant();

        // Strip namespace prefixes (e.g., "@cmann50/" or "namespace/")
        var nameWithoutNamespace = packageName;
        if (packageName.StartsWith("@"))
        {
            var slashIndex = packageName.IndexOf('/');
            if (slashIndex > 0)
            {
                nameWithoutNamespace = packageName.Substring(slashIndex + 1);
                score -= 150; // Penalize third-party namespaced packages
            }
        }
        else if (packageName.Contains('/'))
        {
            var slashIndex = packageName.IndexOf('/');
            nameWithoutNamespace = packageName.Substring(slashIndex + 1);
            score -= 100; // Penalize namespaced packages (less than @namespace)
        }

        // Exact match is best (on name without namespace)
        if (nameWithoutNamespace == normalizedSearch)
        {
            score += 1000;
        }
        // Starts with search term (e.g., "chrome-xyz" or "mcp-chrome")
        else if (nameWithoutNamespace.StartsWith(normalizedSearch + "-") || nameWithoutNamespace.StartsWith(normalizedSearch + "_"))
        {
            score += 700;
        }
        // Ends with search term (e.g., "xyz-chrome")
        else if (nameWithoutNamespace.EndsWith("-" + normalizedSearch) || nameWithoutNamespace.EndsWith("_" + normalizedSearch))
        {
            score += 600;
        }
        // Contains search term (less preferred)
        else if (nameWithoutNamespace.Contains(normalizedSearch))
        {
            score += 300;
        }

        // Exact match on server name
        if (server.ServerName.ToLowerInvariant() == normalizedSearch)
            score += 900;

        // Prefer validated packages
        if (server.Validated)
            score += 200;

        // Penalize feature-specific keywords
        var featureKeywords = new[] { "google-search", "api", "extension", "plugin", "specific", "manager", "tool", "client", "wrapper", "sdk" };
        foreach (var keyword in featureKeywords)
        {
            if (nameWithoutNamespace.Contains(keyword))
                score -= 200;
        }

        // Penalize packages with additional words after the search term
        // Split by hyphens/underscores/slashes and count words
        var words = nameWithoutNamespace.Split(new[] { '-', '_', '.', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            // If search term is found, check if there are words after it
            var searchIndex = Array.IndexOf(words, normalizedSearch);
            if (searchIndex >= 0 && searchIndex < words.Length - 1)
            {
                // Penalize extra words after the search term
                var extraWords = words.Length - searchIndex - 1;
                score -= extraWords * 50;
            }
            else if (nameWithoutNamespace.Contains(normalizedSearch))
            {
                // Search term is in the name but not as a separate word, still penalize extra words
                score -= (words.Length - 1) * 30;
            }
        }

        // Stricter length penalty: -2 points per character over 8 chars (on name without namespace)
        if (nameWithoutNamespace.Length > 8)
            score -= (nameWithoutNamespace.Length - 8) * 2;

        // Fewer special characters = more general
        var specialCharCount = nameWithoutNamespace.Count(c => c == '-' || c == '_' || c == '.');
        score -= specialCharCount * 10;

        // Prefer packages without version-like suffixes
        if (System.Text.RegularExpressions.Regex.IsMatch(nameWithoutNamespace, @"-v?\d+"))
            score -= 50;

        return score;
    }
}
