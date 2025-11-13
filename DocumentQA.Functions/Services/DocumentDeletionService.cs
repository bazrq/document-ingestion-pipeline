using Azure;
using Azure.Storage.Blobs;
using DocumentQA.Functions.Models;
using Microsoft.Extensions.Logging;

namespace DocumentQA.Functions.Services;

/// <summary>
/// Orchestrates the deletion of documents across all storage locations (Blob Storage, Table Storage, AI Search).
/// Uses a best-effort approach: attempts all deletions and logs failures without rolling back.
/// </summary>
public class DocumentDeletionService
{
    private readonly BlobContainerClient _containerClient;
    private readonly DocumentStatusService _statusService;
    private readonly SearchService _searchService;
    private readonly ILogger<DocumentDeletionService> _logger;

    public DocumentDeletionService(
        BlobContainerClient containerClient,
        DocumentStatusService statusService,
        SearchService searchService,
        ILogger<DocumentDeletionService> logger)
    {
        _containerClient = containerClient;
        _statusService = statusService;
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Deletes a document and all associated data from all storage locations.
    /// </summary>
    /// <param name="documentId">The unique identifier of the document to delete</param>
    /// <returns>A DeletionResult containing the status of each deletion operation</returns>
    public async Task<DeletionResult> DeleteDocumentAsync(string documentId)
    {
        _logger.LogInformation("Starting deletion process for document {DocumentId}", documentId);

        // Validate documentId format (should be a GUID)
        if (!Guid.TryParse(documentId, out _))
        {
            _logger.LogWarning("Invalid document ID format: {DocumentId}", documentId);
            return DeletionResult.Failure(documentId, new List<string> { "Invalid document ID format. Expected a GUID." });
        }

        // Check if document exists
        var documentStatus = await _statusService.GetStatusAsync(documentId);
        if (documentStatus == null)
        {
            _logger.LogWarning("Document {DocumentId} not found in Table Storage", documentId);
            return DeletionResult.Failure(documentId, new List<string> { "Document not found." });
        }

        var errors = new List<string>();
        var deletedChunks = false;
        var deletedBlob = false;
        var deletedStatus = false;

        // Step 1: Delete chunks from Azure AI Search
        try
        {
            _logger.LogInformation("Deleting chunks from AI Search for document {DocumentId}", documentId);
            await _searchService.DeleteDocumentChunksAsync(documentId);
            deletedChunks = true;
            _logger.LogInformation("Successfully deleted chunks from AI Search for document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to delete chunks from AI Search: {ex.Message}";
            _logger.LogWarning(ex, "Failed to delete chunks from AI Search for document {DocumentId}", documentId);
            errors.Add(errorMsg);
        }

        // Step 2: Delete blob from Blob Storage
        try
        {
            _logger.LogInformation("Deleting blob from Blob Storage for document {DocumentId}", documentId);
            var blobPath = documentStatus.BlobPath;

            if (string.IsNullOrEmpty(blobPath))
            {
                _logger.LogWarning("Blob path is empty for document {DocumentId}", documentId);
                errors.Add("Blob path not found in document status record.");
            }
            else
            {
                var blobClient = _containerClient.GetBlobClient(blobPath);
                var deleted = await blobClient.DeleteIfExistsAsync();

                if (deleted.Value)
                {
                    deletedBlob = true;
                    _logger.LogInformation("Successfully deleted blob from Blob Storage for document {DocumentId}", documentId);
                }
                else
                {
                    // Blob didn't exist, but that's okay - treat as success
                    deletedBlob = true;
                    _logger.LogInformation("Blob did not exist in Blob Storage for document {DocumentId} (already deleted or never uploaded)", documentId);
                }
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob doesn't exist, which is fine for deletion
            deletedBlob = true;
            _logger.LogInformation("Blob not found in Blob Storage for document {DocumentId} (already deleted)", documentId);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to delete blob from Blob Storage: {ex.Message}";
            _logger.LogWarning(ex, "Failed to delete blob from Blob Storage for document {DocumentId}", documentId);
            errors.Add(errorMsg);
        }

        // Step 3: Delete status record from Table Storage
        try
        {
            _logger.LogInformation("Deleting status record from Table Storage for document {DocumentId}", documentId);
            var deleted = await _statusService.DeleteStatusAsync(documentId);

            if (deleted)
            {
                deletedStatus = true;
                _logger.LogInformation("Successfully deleted status record from Table Storage for document {DocumentId}", documentId);
            }
            else
            {
                // Entity didn't exist, but that's okay - treat as success
                deletedStatus = true;
                _logger.LogInformation("Status record did not exist in Table Storage for document {DocumentId} (already deleted)", documentId);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to delete status record from Table Storage: {ex.Message}";
            _logger.LogWarning(ex, "Failed to delete status record from Table Storage for document {DocumentId}", documentId);
            errors.Add(errorMsg);
        }

        // Determine overall result
        if (deletedChunks && deletedBlob && deletedStatus && errors.Count == 0)
        {
            _logger.LogInformation("Document {DocumentId} successfully deleted from all storage locations", documentId);
            return DeletionResult.Success(documentId);
        }
        else if (errors.Count > 0)
        {
            _logger.LogWarning("Document {DocumentId} partially deleted: {SuccessCount}/3 operations succeeded, {ErrorCount} errors occurred",
                documentId, (deletedChunks ? 1 : 0) + (deletedBlob ? 1 : 0) + (deletedStatus ? 1 : 0), errors.Count);
            return DeletionResult.PartialSuccess(documentId, deletedChunks, deletedBlob, deletedStatus, errors);
        }
        else
        {
            _logger.LogError("Document {DocumentId} deletion failed completely", documentId);
            return DeletionResult.Failure(documentId, errors);
        }
    }
}
