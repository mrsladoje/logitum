using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Loupedeck.AdaptiveRingPlugin.Services;

/// <summary>
/// Client for VoyageAI embedding API
/// </summary>
public class VoyageAIClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string API_ENDPOINT = "https://api.voyageai.com/v1/embeddings";
    private const string MODEL = "voyage-3";

    public VoyageAIClient(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Gets an embedding vector for the given text
    /// </summary>
    public async Task<float[]?> GetEmbeddingAsync(string text)
    {
        try
        {
            var request = new
            {
                input = new[] { text },
                model = MODEL,
                input_type = "document"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(API_ENDPOINT, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<VoyageEmbeddingResponse>(responseBody);

            if (result?.Data != null && result.Data.Count > 0)
            {
                return result.Data[0].Embedding;
            }

            return null;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to get embedding from VoyageAI: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets embeddings for multiple texts in a batch
    /// </summary>
    public async Task<List<float[]>?> GetEmbeddingsBatchAsync(List<string> texts)
    {
        try
        {
            var request = new
            {
                input = texts,
                model = MODEL,
                input_type = "document"
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _httpClient.PostAsync(API_ENDPOINT, content);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<VoyageEmbeddingResponse>(responseBody);

            if (result?.Data != null && result.Data.Count > 0)
            {
                return result.Data.Select(d => d.Embedding).ToList();
            }

            return null;
        }
        catch (Exception ex)
        {
            PluginLog.Error($"Failed to get batch embeddings from VoyageAI: {ex.Message}");
            return null;
        }
    }
}

// Response models for VoyageAI API
internal class VoyageEmbeddingResponse
{
    public List<VoyageEmbeddingData> Data { get; set; } = new();
    public VoyageUsage? Usage { get; set; }
}

internal class VoyageEmbeddingData
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int Index { get; set; }
}

internal class VoyageUsage
{
    public int TotalTokens { get; set; }
}
