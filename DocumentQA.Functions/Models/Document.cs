namespace DocumentQA.Functions.Models;

public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int PageCount { get; set; }
    public string BlobUri { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
