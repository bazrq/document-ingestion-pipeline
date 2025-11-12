using Azure;
using Azure.AI.OpenAI;
using DocumentQA.Functions.Configuration;
using OpenAI.Embeddings;

namespace DocumentQA.Functions.Services;

public class EmbeddingService
{
    private readonly AzureOpenAIClient _client;
    private readonly string _deploymentName;
    private readonly EmbeddingClient _embeddingClient;

    public EmbeddingService(OpenAIConfig config)
    {
        _client = new AzureOpenAIClient(
            new Uri(config.Endpoint),
            new AzureKeyCredential(config.ApiKey));

        _deploymentName = config.EmbeddingDeploymentName;
        _embeddingClient = _client.GetEmbeddingClient(_deploymentName);
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var response = await _embeddingClient.GenerateEmbeddingAsync(text);
            return response.Value.ToFloats();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error generating embedding: {ex.Message}", ex);
        }
    }

    public async Task<List<ReadOnlyMemory<float>>> GenerateBatchEmbeddingsAsync(List<string> texts)
    {
        try
        {
            var embeddings = new List<ReadOnlyMemory<float>>();

            // Process in batches of 10 to avoid rate limits
            const int batchSize = 10;
            for (var i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize).ToList();

                var tasks = batch.Select(text => GenerateEmbeddingAsync(text));
                var batchResults = await Task.WhenAll(tasks);

                embeddings.AddRange(batchResults);

                // Small delay to avoid rate limiting
                if (i + batchSize < texts.Count)
                {
                    await Task.Delay(100);
                }
            }

            return embeddings;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error generating batch embeddings: {ex.Message}", ex);
        }
    }
}
