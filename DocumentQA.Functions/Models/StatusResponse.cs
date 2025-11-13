namespace DocumentQA.Functions.Models;

public class StatusResponse
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "uploaded", "processing", "completed", "failed"
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStep { get; set; }
    public int? PageCount { get; set; }
    public int? ChunkCount { get; set; }
}
