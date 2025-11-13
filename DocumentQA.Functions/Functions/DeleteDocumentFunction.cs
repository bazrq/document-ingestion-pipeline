using System.Net;
using DocumentQA.Functions.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocumentQA.Functions.Functions;

/// <summary>
/// Azure Function that handles document deletion requests.
/// Provides a RESTful DELETE endpoint to remove documents and all associated data.
/// </summary>
public class DeleteDocumentFunction
{
    private readonly DocumentDeletionService _deletionService;
    private readonly ILogger<DeleteDocumentFunction> _logger;

    public DeleteDocumentFunction(
        DocumentDeletionService deletionService,
        ILogger<DeleteDocumentFunction> logger)
    {
        _deletionService = deletionService;
        _logger = logger;
    }

    /// <summary>
    /// Deletes a document by ID from all storage locations (Blob Storage, Table Storage, AI Search).
    /// </summary>
    /// <param name="req">The HTTP request containing the document ID in the route</param>
    /// <param name="documentId">The unique identifier of the document to delete (from route parameter)</param>
    /// <returns>HTTP response with deletion result</returns>
    [Function("DeleteDocument")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "documents/{documentId}")] HttpRequestData req,
        string documentId)
    {
        _logger.LogInformation("DELETE /api/documents/{DocumentId} - Request received", documentId);

        // Validate documentId parameter
        if (string.IsNullOrWhiteSpace(documentId))
        {
            _logger.LogWarning("DELETE /api/documents - Missing or empty documentId");
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteAsJsonAsync(new
            {
                error = "Bad Request",
                message = "Document ID is required and cannot be empty."
            });
            return badRequestResponse;
        }

        // Validate GUID format
        if (!Guid.TryParse(documentId, out _))
        {
            _logger.LogWarning("DELETE /api/documents/{DocumentId} - Invalid GUID format", documentId);
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteAsJsonAsync(new
            {
                error = "Bad Request",
                message = "Document ID must be a valid GUID."
            });
            return badRequestResponse;
        }

        try
        {
            // Perform the deletion
            var result = await _deletionService.DeleteDocumentAsync(documentId);

            // Handle result based on outcome
            if (result.OverallSuccess)
            {
                // Complete success - return 204 No Content
                _logger.LogInformation("DELETE /api/documents/{DocumentId} - Successfully deleted", documentId);
                var successResponse = req.CreateResponse(HttpStatusCode.NoContent);
                return successResponse;
            }
            else if (result.Errors.Any(e => e.Contains("Document not found")))
            {
                // Document doesn't exist - return 404 Not Found
                _logger.LogWarning("DELETE /api/documents/{DocumentId} - Document not found", documentId);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteAsJsonAsync(new
                {
                    error = "Not Found",
                    message = $"Document with ID '{documentId}' not found.",
                    documentId = documentId
                });
                return notFoundResponse;
            }
            else
            {
                // Partial success or failure - return 200 OK with details
                _logger.LogWarning("DELETE /api/documents/{DocumentId} - Partial deletion: {Message}", documentId, result.Message);
                var partialResponse = req.CreateResponse(HttpStatusCode.OK);
                await partialResponse.WriteAsJsonAsync(new
                {
                    documentId = result.DocumentId,
                    message = result.Message,
                    success = result.OverallSuccess,
                    details = new
                    {
                        deletedFromAISearch = result.DeletedChunks,
                        deletedFromBlobStorage = result.DeletedBlob,
                        deletedFromTableStorage = result.DeletedStatus
                    },
                    errors = result.Errors
                });
                return partialResponse;
            }
        }
        catch (Exception ex)
        {
            // Unexpected error - return 500 Internal Server Error
            _logger.LogError(ex, "DELETE /api/documents/{DocumentId} - Unexpected error during deletion", documentId);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                error = "Internal Server Error",
                message = "An unexpected error occurred while deleting the document.",
                documentId = documentId,
                details = ex.Message
            });
            return errorResponse;
        }
    }
}
