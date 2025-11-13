namespace DocumentQA.Functions.Models;

/// <summary>
/// Represents the result of a document deletion operation across all storage locations.
/// </summary>
public class DeletionResult
{
    /// <summary>
    /// The ID of the document that was deleted.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether all deletion operations succeeded without errors.
    /// </summary>
    public bool OverallSuccess { get; set; }

    /// <summary>
    /// Indicates whether chunks were successfully deleted from Azure AI Search.
    /// </summary>
    public bool DeletedChunks { get; set; }

    /// <summary>
    /// Indicates whether the blob (PDF file) was successfully deleted from Blob Storage.
    /// </summary>
    public bool DeletedBlob { get; set; }

    /// <summary>
    /// Indicates whether the status record was successfully deleted from Table Storage.
    /// </summary>
    public bool DeletedStatus { get; set; }

    /// <summary>
    /// List of error messages encountered during deletion operations.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// User-friendly summary message describing the deletion result.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful deletion result.
    /// </summary>
    public static DeletionResult Success(string documentId)
    {
        return new DeletionResult
        {
            DocumentId = documentId,
            OverallSuccess = true,
            DeletedChunks = true,
            DeletedBlob = true,
            DeletedStatus = true,
            Message = "Document deleted successfully from all storage locations."
        };
    }

    /// <summary>
    /// Creates a partial success deletion result with specific errors.
    /// </summary>
    public static DeletionResult PartialSuccess(string documentId, bool deletedChunks, bool deletedBlob, bool deletedStatus, List<string> errors)
    {
        var successCount = (deletedChunks ? 1 : 0) + (deletedBlob ? 1 : 0) + (deletedStatus ? 1 : 0);

        return new DeletionResult
        {
            DocumentId = documentId,
            OverallSuccess = false,
            DeletedChunks = deletedChunks,
            DeletedBlob = deletedBlob,
            DeletedStatus = deletedStatus,
            Errors = errors,
            Message = $"Document partially deleted ({successCount}/3 operations succeeded). Some data may remain in the system."
        };
    }

    /// <summary>
    /// Creates a failure deletion result.
    /// </summary>
    public static DeletionResult Failure(string documentId, List<string> errors)
    {
        return new DeletionResult
        {
            DocumentId = documentId,
            OverallSuccess = false,
            DeletedChunks = false,
            DeletedBlob = false,
            DeletedStatus = false,
            Errors = errors,
            Message = "Document deletion failed. No data was removed from the system."
        };
    }
}
