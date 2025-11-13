namespace DocumentQA.Functions.Models;

public class DocumentListResponse
{
    public List<DocumentSummary> Documents { get; set; } = new();
    public int TotalCount { get; set; }
}
