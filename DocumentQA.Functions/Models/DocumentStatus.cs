using Azure;
using Azure.Data.Tables;

namespace DocumentQA.Functions.Models;

public class DocumentStatus : ITableEntity
{
    public string PartitionKey { get; set; } = "documents";
    public string RowKey { get; set; } = string.Empty; // documentId (GUID)
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Custom properties
    public string FileName { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "uploaded", "processing", "completed", "failed"
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStep { get; set; }
    public int? ErrorAttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public int? PageCount { get; set; }
    public int? ChunkCount { get; set; }
}
