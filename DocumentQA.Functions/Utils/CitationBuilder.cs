using DocumentQA.Functions.Models;

namespace DocumentQA.Functions.Utils;

public static class CitationBuilder
{
    public static List<Citation> BuildCitations(List<QueryResult> queryResults, int maxExcerptLength = 200)
    {
        var citations = new List<Citation>();

        foreach (var result in queryResults)
        {
            var excerpt = result.Content.Length > maxExcerptLength
                ? result.Content.Substring(0, maxExcerptLength) + "..."
                : result.Content;

            citations.Add(new Citation
            {
                DocumentTitle = result.DocumentTitle,
                PageNumber = result.PageNumber,
                Excerpt = excerpt,
                SectionTitle = result.SectionTitle
            });
        }

        return citations;
    }

    public static string FormatCitationsForDisplay(List<Citation> citations)
    {
        if (citations.Count == 0)
            return "No citations available.";

        var formattedCitations = new List<string>();

        for (var i = 0; i < citations.Count; i++)
        {
            var citation = citations[i];
            var citationText = $"[{i + 1}] {citation.DocumentTitle}, Page {citation.PageNumber}";

            if (!string.IsNullOrWhiteSpace(citation.SectionTitle))
            {
                citationText += $", Section: {citation.SectionTitle}";
            }

            citationText += $"\n    \"{citation.Excerpt}\"";
            formattedCitations.Add(citationText);
        }

        return string.Join("\n\n", formattedCitations);
    }

    public static string BuildAnswerWithInlineCitations(string answerText, List<Citation> citations)
    {
        // Simple inline citation format: add [1], [2], etc. at the end
        if (citations.Count == 0)
            return answerText;

        var citationNumbers = string.Join(", ", Enumerable.Range(1, citations.Count).Select(i => $"[{i}]"));
        return $"{answerText} {citationNumbers}";
    }
}
