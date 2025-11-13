using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using DocumentQA.Functions.Services;
using DocumentQA.Functions.Models;
using System.Net;

namespace DocumentQA.Functions.Functions;

public class StatusFunction
{
    private readonly DocumentStatusService _statusService;
    private readonly ILogger<StatusFunction> _logger;

    public StatusFunction(
        DocumentStatusService statusService,
        ILogger<StatusFunction> logger)
    {
        _statusService = statusService;
        _logger = logger;
    }

    [Function("GetDocumentStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{documentId}")] HttpRequestData req,
        string documentId)
    {
        _logger.LogInformation("Getting status for document: {DocumentId}", documentId);

        try
        {
            // Validate documentId
            if (string.IsNullOrWhiteSpace(documentId))
            {
                _logger.LogWarning("Document ID is missing");
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteAsJsonAsync(new { error = "Document ID is required" });
                return badResponse;
            }

            // Get document status
            var status = await _statusService.GetStatusAsync(documentId);

            if (status == null)
            {
                _logger.LogWarning("Document not found: {DocumentId}", documentId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new { error = $"Document with ID '{documentId}' not found" });
                return notFoundResponse;
            }

            _logger.LogInformation(
                "Retrieved status for document {DocumentId}: {Status}",
                documentId, status.Status);

            // Map to response DTO
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new StatusResponse
            {
                DocumentId = status.RowKey,
                FileName = status.FileName,
                Status = status.Status,
                UploadedAt = status.UploadedAt,
                ProcessedAt = status.ProcessedAt,
                ErrorMessage = status.ErrorMessage,
                ErrorStep = status.ErrorStep,
                PageCount = status.PageCount,
                ChunkCount = status.ChunkCount
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status for document {DocumentId}: {ErrorMessage}",
                documentId, ex.Message);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = $"Internal server error: {ex.Message}"
            });
            return errorResponse;
        }
    }
}
