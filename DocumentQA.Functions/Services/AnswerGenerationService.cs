using Azure;
using Azure.AI.OpenAI;
using DocumentQA.Functions.Configuration;
using DocumentQA.Functions.Models;
using DocumentQA.Functions.Utils;
using OpenAI.Chat;

namespace DocumentQA.Functions.Services;

public class AnswerGenerationService
{
    private readonly AzureOpenAIClient _client;
    private readonly ChatClient _chatClient;
    private readonly AnswerGenerationConfig _config;

    public AnswerGenerationService(OpenAIConfig openAIConfig, AnswerGenerationConfig config)
    {
        // Create client options with the configured API version
        // Falls back to the latest stable version if the configured version is not supported
        var clientOptions = AzureOpenAIClientOptionsFactory.CreateWithFallback(openAIConfig.ApiVersion);

        _client = new AzureOpenAIClient(
            new Uri(openAIConfig.Endpoint),
            new AzureKeyCredential(openAIConfig.ApiKey),
            clientOptions);

        _chatClient = _client.GetChatClient(openAIConfig.ChatDeploymentName);
        _config = config;
    }

    public async Task<Answer> GenerateAnswerAsync(string question, List<QueryResult> retrievedChunks)
    {
        try
        {
            // Check if we have any relevant chunks
            if (retrievedChunks.Count == 0)
            {
                return new Answer
                {
                    Text = "I couldn't find any relevant information in the documents to answer your question.",
                    ConfidenceScore = 0.0,
                    FoundInDocuments = false
                };
            }

            // Build context from retrieved chunks
            var context = BuildContext(retrievedChunks);

            // Create the system prompt
            var systemPrompt = @"You are a helpful AI assistant that answers questions based strictly on the provided document context.

IMPORTANT RULES:
1. ONLY use information from the provided context to answer questions
2. If the context doesn't contain enough information to answer, say so clearly
3. Include specific references to document titles and page numbers when citing information
4. Be concise but thorough
5. If you're uncertain, indicate your level of confidence
6. Never make up or infer information beyond what's in the context";

            // Create the user prompt
            var userPrompt = $@"Context from documents:
{context}

Question: {question}

Please provide a comprehensive answer based ONLY on the context above. If the answer is not in the context, clearly state that you don't have enough information.";

            // Generate response
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var chatOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = _config.MaxTokens,
                Temperature = _config.Temperature
            };

            var response = await _chatClient.CompleteChatAsync(messages, chatOptions);
            var answerText = response.Value.Content[0].Text;

            // Build citations
            var citations = CitationBuilder.BuildCitations(retrievedChunks);

            // Calculate confidence score
            var confidenceScore = CalculateConfidenceScore(answerText, retrievedChunks);

            // Check if answer indicates information not found
            var foundInDocuments = !ContainsNotFoundIndicators(answerText);

            return new Answer
            {
                Text = answerText,
                ConfidenceScore = confidenceScore,
                Citations = citations,
                FoundInDocuments = foundInDocuments,
                Alternatives = new List<AlternativeAnswer>()
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error generating answer: {ex.Message}", ex);
        }
    }

    private static string BuildContext(List<QueryResult> chunks)
    {
        var contextParts = new List<string>();

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var contextPart = $"[Document: {chunk.DocumentTitle}, Page {chunk.PageNumber}]\n{chunk.Content}";
            contextParts.Add(contextPart);
        }

        return string.Join("\n\n---\n\n", contextParts);
    }

    private double CalculateConfidenceScore(string answerText, List<QueryResult> chunks)
    {
        // Simple confidence calculation based on:
        // 1. Average retrieval score of chunks
        // 2. Answer length (very short answers might indicate uncertainty)
        // 3. Presence of hedging language

        var avgRetrievalScore = chunks.Average(c => c.RelevanceScore);

        // Normalize to 0-1 range (assuming search scores are 0-1)
        var retrievalConfidence = Math.Min(avgRetrievalScore, 1.0);

        // Penalize very short answers (less than 20 words might indicate uncertainty)
        var wordCount = answerText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var lengthConfidence = wordCount < 20 ? 0.7 : 1.0;

        // Check for hedging language
        var hedgingPhrases = new[]
        {
            "i'm not sure",
            "i don't have enough information",
            "the context doesn't provide",
            "it's unclear",
            "might", "possibly", "perhaps"
        };

        var lowerAnswer = answerText.ToLowerInvariant();
        var hedgingPenalty = hedgingPhrases.Any(phrase => lowerAnswer.Contains(phrase)) ? 0.7 : 1.0;

        // Combined confidence score
        var confidence = retrievalConfidence * lengthConfidence * hedgingPenalty;

        return Math.Round(confidence, 2);
    }

    private static bool ContainsNotFoundIndicators(string answerText)
    {
        var notFoundPhrases = new[]
        {
            "don't have enough information",
            "not in the context",
            "doesn't contain",
            "cannot find",
            "no information",
            "not provided",
            "not mentioned"
        };

        var lowerAnswer = answerText.ToLowerInvariant();
        return notFoundPhrases.Any(phrase => lowerAnswer.Contains(phrase));
    }
}
