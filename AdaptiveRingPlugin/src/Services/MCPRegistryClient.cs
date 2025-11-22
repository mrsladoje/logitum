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
            return await _database.SearchToolSDKIndexAsync(appName);
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

            foreach (var term in searchTerms)
            {
                var url = $"{OFFICIAL_REGISTRY_URL}?search={term}&version=latest";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<OfficialRegistryResponse>(response);

                if (result?.Servers != null && result.Servers.Count > 0)
                {
                    var server = result.Servers[0].Server;
                    if (server != null)
                    {
                        return new MCPServerData
                        {
                            ServerName = server.Title ?? server.Name,
                            PackageName = server.Name,
                            Description = server.Description,
                            RegistrySource = "Official",
                            Category = "official",
                            Validated = true
                        };
                    }
                }
            }
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

            foreach (var term in searchTerms)
            {
                var url = $"{GLAMA_URL}?search={term}";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<GlamaResponse>(response);

                if (result?.Servers != null && result.Servers.Count > 0)
                {
                    var server = result.Servers[0];
                    return new MCPServerData
                    {
                        ServerName = server.Name,
                        PackageName = $"{server.Namespace}/{server.Slug}",
                        Description = server.Description,
                        RegistrySource = "Glama",
                        Category = "glama",
                        Validated = false
                    };
                }
            }
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
}
