namespace DocumentQA.Functions.Models;

public class QueryRequest
{
    public string Question { get; set; } = string.Empty;
    public List<string> DocumentIds { get; set; } = new();
    public int MaxChunks { get; set; } = 7;
}
