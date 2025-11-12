using System.Text;

namespace DocumentQA.Functions.Utils;

public class ChunkingStrategy
{
    private readonly int _chunkSize;
    private readonly int _overlap;

    public ChunkingStrategy(int chunkSize = 800, int overlap = 50)
    {
        _chunkSize = chunkSize;
        _overlap = overlap;
    }

    public List<TextChunk> ChunkText(string text, int pageNumber, string sectionTitle = "")
    {
        var chunks = new List<TextChunk>();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        // Split by paragraphs first to respect document structure
        var paragraphs = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new StringBuilder();
        var currentTokenCount = 0;
        var chunkIndex = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphTokenCount = EstimateTokenCount(paragraph);

            // If single paragraph is larger than chunk size, split it
            if (paragraphTokenCount > _chunkSize)
            {
                // Save current chunk if it has content
                if (currentChunk.Length > 0)
                {
                    chunks.Add(new TextChunk
                    {
                        Content = currentChunk.ToString().Trim(),
                        PageNumber = pageNumber,
                        SectionTitle = sectionTitle,
                        ChunkIndex = chunkIndex++
                    });
                    currentChunk.Clear();
                    currentTokenCount = 0;
                }

                // Split large paragraph into sentences
                var sentences = SplitIntoSentences(paragraph);
                foreach (var sentence in sentences)
                {
                    var sentenceTokenCount = EstimateTokenCount(sentence);

                    if (currentTokenCount + sentenceTokenCount > _chunkSize && currentChunk.Length > 0)
                    {
                        chunks.Add(new TextChunk
                        {
                            Content = currentChunk.ToString().Trim(),
                            PageNumber = pageNumber,
                            SectionTitle = sectionTitle,
                            ChunkIndex = chunkIndex++
                        });

                        // Add overlap from previous chunk
                        var overlapText = GetOverlapText(currentChunk.ToString(), _overlap);
                        currentChunk.Clear();
                        currentChunk.Append(overlapText);
                        currentTokenCount = EstimateTokenCount(overlapText);
                    }

                    currentChunk.Append(sentence).Append(' ');
                    currentTokenCount += sentenceTokenCount;
                }
            }
            else
            {
                // Check if adding this paragraph exceeds chunk size
                if (currentTokenCount + paragraphTokenCount > _chunkSize && currentChunk.Length > 0)
                {
                    chunks.Add(new TextChunk
                    {
                        Content = currentChunk.ToString().Trim(),
                        PageNumber = pageNumber,
                        SectionTitle = sectionTitle,
                        ChunkIndex = chunkIndex++
                    });

                    // Add overlap from previous chunk
                    var overlapText = GetOverlapText(currentChunk.ToString(), _overlap);
                    currentChunk.Clear();
                    currentChunk.Append(overlapText);
                    currentTokenCount = EstimateTokenCount(overlapText);
                }

                currentChunk.Append(paragraph).Append("\n\n");
                currentTokenCount += paragraphTokenCount;
            }
        }

        // Add remaining content as final chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(new TextChunk
            {
                Content = currentChunk.ToString().Trim(),
                PageNumber = pageNumber,
                SectionTitle = sectionTitle,
                ChunkIndex = chunkIndex
            });
        }

        return chunks;
    }

    private static int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token for English text
        return text.Length / 4;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitting (can be enhanced with NLP library)
        var sentences = new List<string>();
        var sentenceEndings = new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" };

        var lastIndex = 0;
        for (var i = 0; i < text.Length - 1; i++)
        {
            foreach (var ending in sentenceEndings)
            {
                if (i + ending.Length <= text.Length &&
                    text.Substring(i, ending.Length) == ending)
                {
                    sentences.Add(text.Substring(lastIndex, i - lastIndex + ending.Length));
                    lastIndex = i + ending.Length;
                    break;
                }
            }
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            sentences.Add(text.Substring(lastIndex));
        }

        return sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    private static string GetOverlapText(string text, int overlapTokens)
    {
        var words = text.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var overlapWordCount = Math.Min(overlapTokens, words.Length);

        return string.Join(' ', words.TakeLast(overlapWordCount));
    }
}

public class TextChunk
{
    public string Content { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string SectionTitle { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
}
