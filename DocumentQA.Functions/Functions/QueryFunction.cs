using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using DocumentQA.Functions.Services;
using DocumentQA.Functions.Models;
using System.Diagnostics;
using System.Net;

namespace DocumentQA.Functions.Functions;

public class QueryFunction
{
    private readonly QueryService _queryService;
    private readonly ILogger<QueryFunction> _logger;

    public QueryFunction(
        QueryService queryService,
        ILogger<QueryFunction> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    [Function("QueryDocuments")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "query")] HttpRequestData req)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Processing query request");

        try
        {
            // Parse request
            var request = await req.ReadFromJsonAsync<QueryRequest>();

            if (request == null)
            {
                _logger.LogWarning("Failed to parse request body");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Invalid request body" });
                return badResponse;
            }

            if (string.IsNullOrWhiteSpace(request.Question))
            {
                _logger.LogWarning("Question is missing from request");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Question is required" });
                return badResponse;
            }

            if (request.DocumentIds == null || !request.DocumentIds.Any())
            {
                _logger.LogWarning("No document IDs provided in request");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "At least one document ID is required" });
                return badResponse;
            }

            _logger.LogInformation(
                "Processing query: '{Question}' across {DocumentCount} documents",
                request.Question, request.DocumentIds.Count);

            // Process query using QueryService
            var answer = await _queryService.AskQuestionAsync(
                request.Question,
                request.DocumentIds);

            stopwatch.Stop();

            if (answer == null || string.IsNullOrWhiteSpace(answer.Text))
            {
                _logger.LogInformation("No relevant information found for query");
                var notFoundResponse = req.CreateResponse(HttpStatusCode.OK);
                await notFoundResponse.WriteAsJsonAsync(new QueryResponse
                {
                    Answer = "No relevant information found in the specified documents.",
                    Confidence = 0.0,
                    Citations = new List<Citation>(),
                    ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
                });
                return notFoundResponse;
            }

            _logger.LogInformation(
                "Query completed successfully with confidence {Confidence:F2} in {ElapsedMs}ms",
                answer.ConfidenceScore, stopwatch.ElapsedMilliseconds);

            // Return response
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new QueryResponse
            {
                Answer = answer.Text,
                Confidence = answer.ConfidenceScore,
                Citations = answer.Citations,
                ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds
            });

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing query: {ErrorMessage}", ex.Message);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = $"Internal server error: {ex.Message}",
                processingTimeMs = (int)stopwatch.ElapsedMilliseconds
            });
            return errorResponse;
        }
    }
}
