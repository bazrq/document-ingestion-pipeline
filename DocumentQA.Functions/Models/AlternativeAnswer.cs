namespace DocumentQA.Functions.Models;

public class AlternativeAnswer
{
    public string Text { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public List<Citation> Citations { get; set; } = [];
}
