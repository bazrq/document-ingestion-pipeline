namespace DocumentQA.Functions.Models;

public class QueryResponse
{
    public string Answer { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public List<Citation> Citations { get; set; } = new();
    public int ProcessingTimeMs { get; set; }
}
