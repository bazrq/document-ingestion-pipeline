namespace DocumentQA.Functions.Models;

public class Citation
{
    public string DocumentTitle { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
}
