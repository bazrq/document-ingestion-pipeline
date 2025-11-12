using Azure;
using Azure.Data.Tables;
using DocumentQA.Functions.Models;

namespace DocumentQA.Functions.Services;

public class DocumentStatusService
{
    private readonly TableClient _tableClient;

    public DocumentStatusService(string connectionString, string tableName = "documentstatus")
    {
        _tableClient = new TableClient(connectionString, tableName);
    }

    /// <summary>
    /// Initializes the table storage (creates table if it doesn't exist)
    /// </summary>
    public async Task InitializeAsync()
    {
        await _tableClient.CreateIfNotExistsAsync();
    }

    /// <summary>
    /// Creates a new document status record with initial "uploaded" status
    /// </summary>
    public async Task<DocumentStatus> CreateAsync(string documentId, string fileName, string blobPath)
    {
        var status = new DocumentStatus
        {
            PartitionKey = "documents",
            RowKey = documentId,
            FileName = fileName,
            BlobPath = blobPath,
            Status = "uploaded",
            UploadedAt = DateTime.UtcNow
        };

        await _tableClient.AddEntityAsync(status);
        return status;
    }

    /// <summary>
    /// Updates the status of a document (uploaded -> processing -> completed/failed)
    /// </summary>
    public async Task UpdateStatusAsync(string documentId, string status)
    {
        try
        {
            var entity = await _tableClient.GetEntityAsync<DocumentStatus>("documents", documentId);
            var documentStatus = entity.Value;

            documentStatus.Status = status;

            if (status == "completed")
            {
                documentStatus.ProcessedAt = DateTime.UtcNow;
            }

            await _tableClient.UpdateEntityAsync(documentStatus, documentStatus.ETag, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException($"Document with ID {documentId} not found.", ex);
        }
    }

    /// <summary>
    /// Updates processing details after successful completion
    /// </summary>
    public async Task UpdateProcessingDetailsAsync(string documentId, int pageCount, int chunkCount)
    {
        try
        {
            var entity = await _tableClient.GetEntityAsync<DocumentStatus>("documents", documentId);
            var documentStatus = entity.Value;

            documentStatus.PageCount = pageCount;
            documentStatus.ChunkCount = chunkCount;

            await _tableClient.UpdateEntityAsync(documentStatus, documentStatus.ETag, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException($"Document with ID {documentId} not found.", ex);
        }
    }

    /// <summary>
    /// Marks a document as failed and records error details
    /// </summary>
    public async Task MarkAsFailedAsync(string documentId, string errorMessage, string errorStep, int attemptCount)
    {
        try
        {
            var entity = await _tableClient.GetEntityAsync<DocumentStatus>("documents", documentId);
            var documentStatus = entity.Value;

            documentStatus.Status = "failed";
            documentStatus.ErrorMessage = errorMessage;
            documentStatus.ErrorStep = errorStep;
            documentStatus.ErrorAttemptCount = attemptCount;
            documentStatus.LastAttemptAt = DateTime.UtcNow;

            await _tableClient.UpdateEntityAsync(documentStatus, documentStatus.ETag, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException($"Document with ID {documentId} not found.", ex);
        }
    }

    /// <summary>
    /// Retrieves the current status of a document
    /// </summary>
    public async Task<DocumentStatus?> GetStatusAsync(string documentId)
    {
        try
        {
            var entity = await _tableClient.GetEntityAsync<DocumentStatus>("documents", documentId);
            return entity.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
