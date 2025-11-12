namespace DocumentQA.Functions.Models;

public class Answer
{
    public string Text { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public List<Citation> Citations { get; set; } = [];
    public List<AlternativeAnswer> Alternatives { get; set; } = [];
    public bool FoundInDocuments { get; set; } = true;
}
