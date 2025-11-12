using DocumentQA.Functions.Configuration;
using DocumentQA.Functions.Models;

namespace DocumentQA.Functions.Services;

public class QueryService
{
    private readonly EmbeddingService _embeddingService;
    private readonly SearchService _searchService;
    private readonly AnswerGenerationService _answerGenerationService;
    private readonly ProcessingConfig _config;

    public QueryService(
        EmbeddingService embeddingService,
        SearchService searchService,
        AnswerGenerationService answerGenerationService,
        ProcessingConfig config)
    {
        _embeddingService = embeddingService;
        _searchService = searchService;
        _answerGenerationService = answerGenerationService;
        _config = config;
    }

    public async Task<Answer> AskQuestionAsync(string question, List<string>? documentIds = null)
    {
        try
        {
            // Step 1: Generate embedding for the question
            var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(question);

            // Step 2: Perform hybrid search to find relevant chunks
            List<QueryResult> searchResults;
            if (documentIds != null && documentIds.Count > 0)
            {
                searchResults = await _searchService.HybridSearchWithFilterAsync(
                    question,
                    questionEmbedding,
                    documentIds,
                    _config.MaxChunksToRetrieve);
            }
            else
            {
                searchResults = await _searchService.HybridSearchAsync(
                    question,
                    questionEmbedding,
                    _config.MaxChunksToRetrieve);
            }

            // Step 3: Re-rank and select top chunks
            var topChunks = await ReRankChunksAsync(question, searchResults);

            // Step 4: Generate answer with GPT-4
            var answer = await _answerGenerationService.GenerateAnswerAsync(question, topChunks);

            return answer;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error processing question: {ex.Message}", ex);
        }
    }

    private async Task<List<QueryResult>> ReRankChunksAsync(string question, List<QueryResult> searchResults)
    {
        // For now, simple re-ranking based on search scores
        // Can be enhanced with a dedicated re-ranking model

        // Take top N chunks based on score
        var topChunks = searchResults
            .OrderByDescending(r => r.Score)
            .Take(_config.TopChunksForAnswer)
            .ToList();

        // Optionally: Use GPT to re-rank based on relevance
        // This would involve calling GPT with the question and each chunk
        // and asking it to score relevance from 0-1

        return await Task.FromResult(topChunks);
    }

    public async Task<List<QueryResult>> SearchDocumentsAsync(string query, int maxResults = 10, List<string>? documentIds = null)
    {
        try
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            if (documentIds != null && documentIds.Count > 0)
            {
                return await _searchService.HybridSearchWithFilterAsync(query, queryEmbedding, documentIds, maxResults);
            }

            return await _searchService.HybridSearchAsync(query, queryEmbedding, maxResults);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error searching documents: {ex.Message}", ex);
        }
    }
}
