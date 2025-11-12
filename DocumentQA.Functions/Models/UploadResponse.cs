namespace DocumentQA.Functions.Models;

public class UploadResponse
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string StatusEndpoint { get; set; } = string.Empty;
}
