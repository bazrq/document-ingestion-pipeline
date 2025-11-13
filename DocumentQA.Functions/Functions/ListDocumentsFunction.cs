using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using DocumentQA.Functions.Services;
using DocumentQA.Functions.Models;
using System.Net;

namespace DocumentQA.Functions.Functions;

public class ListDocumentsFunction
{
    private readonly DocumentStatusService _statusService;
    private readonly ILogger<ListDocumentsFunction> _logger;

    public ListDocumentsFunction(
        DocumentStatusService statusService,
        ILogger<ListDocumentsFunction> logger)
    {
        _statusService = statusService;
        _logger = logger;
    }

    [Function("ListDocuments")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "documents")] HttpRequestData req)
    {
        _logger.LogInformation("Listing documents");

        try
        {
            // Parse query parameters manually
            string? statusFilter = null;
            int? limit = null;

            if (!string.IsNullOrEmpty(req.Url.Query))
            {
                var queryString = req.Url.Query.TrimStart('?');
                var queryPairs = queryString.Split('&');

                foreach (var pair in queryPairs)
                {
                    var parts = pair.Split('=');
                    if (parts.Length == 2)
                    {
                        var key = Uri.UnescapeDataString(parts[0]);
                        var value = Uri.UnescapeDataString(parts[1]);

                        if (key.Equals("status", StringComparison.OrdinalIgnoreCase))
                        {
                            statusFilter = value;
                        }
                        else if (key.Equals("limit", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var parsedLimit))
                        {
                            limit = parsedLimit;
                        }
                    }
                }
            }

            _logger.LogInformation(
                "Retrieving documents with filter: status={StatusFilter}, limit={Limit}",
                statusFilter ?? "all", limit?.ToString() ?? "unlimited");

            // Get documents from storage
            var documents = await _statusService.GetAllDocumentsAsync(statusFilter, limit);

            _logger.LogInformation("Retrieved {Count} documents", documents.Count);

            // Map to response DTOs
            var documentSummaries = documents.Select(d => new DocumentSummary
            {
                DocumentId = d.RowKey,
                FileName = d.FileName,
                Status = d.Status,
                UploadedAt = d.UploadedAt,
                ProcessedAt = d.ProcessedAt,
                PageCount = d.PageCount,
                ChunkCount = d.ChunkCount
            }).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new DocumentListResponse
            {
                Documents = documentSummaries,
                TotalCount = documentSummaries.Count
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents: {ErrorMessage}", ex.Message);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = $"Internal server error: {ex.Message}"
            });
            return errorResponse;
        }
    }
}
