using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace DocumentQA.Functions.Models;

public class DocumentChunk
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [SearchableField(IsFilterable = true)]
    public string DocumentId { get; set; } = string.Empty;

    [SearchableField]
    public string DocumentTitle { get; set; } = string.Empty;

    [SearchableField(AnalyzerName = LexicalAnalyzerName.Values.EnMicrosoft)]
    public string Content { get; set; } = string.Empty;

    [VectorSearchField(VectorSearchDimensions = 3072, VectorSearchProfileName = "vector-profile")]
    public ReadOnlyMemory<float> Embedding { get; set; }

    [SimpleField(IsFilterable = true)]
    public int PageNumber { get; set; }

    [SearchableField]
    public string SectionTitle { get; set; } = string.Empty;

    [SimpleField]
    public int ChunkIndex { get; set; }

    [SimpleField(IsFilterable = true)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
