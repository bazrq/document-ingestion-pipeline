namespace DocumentQA.Functions.Models;

public class QueryResult
{
    public string ChunkId { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string SectionTitle { get; set; } = string.Empty;
    public double Score { get; set; }
    public double RelevanceScore { get; set; }
}
