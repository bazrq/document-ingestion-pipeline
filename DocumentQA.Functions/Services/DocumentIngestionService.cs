using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Storage.Blobs;
using DocumentQA.Functions.Configuration;
using DocumentQA.Functions.Models;
using DocumentQA.Functions.Utils;

namespace DocumentQA.Functions.Services;

public class DocumentIngestionService
{
    private readonly DocumentAnalysisClient _documentAnalysisClient;
    private readonly BlobContainerClient _containerClient;
    private readonly EmbeddingService _embeddingService;
    private readonly SearchService _searchService;
    private readonly ChunkingStrategy _chunkingStrategy;

    public DocumentIngestionService(
        DocumentIntelligenceConfig docIntelConfig,
        StorageConfig storageConfig,
        ProcessingConfig processingConfig,
        EmbeddingService embeddingService,
        SearchService searchService)
    {
        _documentAnalysisClient = new DocumentAnalysisClient(
            new Uri(docIntelConfig.Endpoint),
            new AzureKeyCredential(docIntelConfig.ApiKey));

        var blobServiceClient = new BlobServiceClient(storageConfig.ConnectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(storageConfig.ContainerName);

        _embeddingService = embeddingService;
        _searchService = searchService;
        _chunkingStrategy = new ChunkingStrategy(processingConfig.ChunkSize, processingConfig.ChunkOverlap);
    }

    public async Task InitializeStorageAsync()
    {
        await _containerClient.CreateIfNotExistsAsync();
    }

    public async Task<Document> IngestDocumentAsync(string filePath)
    {
        try
        {
            // Step 1: Upload to Blob Storage
            var document = await UploadToBlobStorageAsync(filePath);

            // Step 2: Extract text using Document Intelligence
            var extractedPages = await ExtractTextFromDocumentAsync(filePath);

            // Step 3: Chunk the document
            var textChunks = ChunkDocument(extractedPages);

            // Step 4: Generate embeddings
            var documentChunks = await GenerateEmbeddingsForChunksAsync(document, textChunks);

            // Step 5: Index in Azure AI Search
            await _searchService.IndexChunksAsync(documentChunks);

            return document;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error ingesting document: {ex.Message}", ex);
        }
    }

    private async Task<Document> UploadToBlobStorageAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var blobClient = _containerClient.GetBlobClient($"{Guid.NewGuid()}_{fileName}");

        await using var fileStream = File.OpenRead(filePath);
        await blobClient.UploadAsync(fileStream, overwrite: true);

        var document = new Document
        {
            FileName = fileName,
            Title = Path.GetFileNameWithoutExtension(fileName),
            BlobUri = blobClient.Uri.ToString()
        };

        return document;
    }

    private async Task<List<PageContent>> ExtractTextFromDocumentAsync(string filePath)
    {
        await using var fileStream = File.OpenRead(filePath);

        var operation = await _documentAnalysisClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-read",
            fileStream);

        var result = operation.Value;
        var pages = new List<PageContent>();

        for (var i = 0; i < result.Pages.Count; i++)
        {
            var page = result.Pages[i];
            var pageText = string.Join(" ", page.Lines.Select(line => line.Content));

            pages.Add(new PageContent
            {
                PageNumber = i + 1,
                Text = pageText
            });
        }

        return pages;
    }

    /// <summary>
    /// Extract text from a document stream (overload for Azure Functions blob trigger)
    /// </summary>
    public async Task<List<PageContent>> ExtractTextFromStreamAsync(Stream documentStream)
    {
        var operation = await _documentAnalysisClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-read",
            documentStream);

        var result = operation.Value;
        var pages = new List<PageContent>();

        for (var i = 0; i < result.Pages.Count; i++)
        {
            var page = result.Pages[i];
            var pageText = string.Join(" ", page.Lines.Select(line => line.Content));

            pages.Add(new PageContent
            {
                PageNumber = i + 1,
                Text = pageText
            });
        }

        return pages;
    }

    /// <summary>
    /// Generate embeddings for text chunks and create DocumentChunk objects
    /// Public overload for Azure Functions
    /// </summary>
    public async Task<List<DocumentChunk>> GenerateEmbeddingsForChunksAsync(
        string documentId,
        string documentTitle,
        List<TextChunk> textChunks)
    {
        var chunkTexts = textChunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingService.GenerateBatchEmbeddingsAsync(chunkTexts);

        var documentChunks = new List<DocumentChunk>();

        for (var i = 0; i < textChunks.Count; i++)
        {
            documentChunks.Add(new DocumentChunk
            {
                DocumentId = documentId,
                DocumentTitle = documentTitle,
                Content = textChunks[i].Content,
                Embedding = embeddings[i],
                PageNumber = textChunks[i].PageNumber,
                SectionTitle = textChunks[i].SectionTitle,
                ChunkIndex = textChunks[i].ChunkIndex
            });
        }

        return documentChunks;
    }

    /// <summary>
    /// Public method to chunk document pages (for Azure Functions and console app)
    /// </summary>
    public List<TextChunk> ChunkDocument(List<PageContent> pages)
    {
        var allChunks = new List<TextChunk>();

        foreach (var page in pages)
        {
            var pageChunks = _chunkingStrategy.ChunkText(
                page.Text,
                page.PageNumber,
                sectionTitle: ""
            );

            allChunks.AddRange(pageChunks);
        }

        return allChunks;
    }

    private async Task<List<DocumentChunk>> GenerateEmbeddingsForChunksAsync(
        Document document,
        List<TextChunk> textChunks)
    {
        var chunkTexts = textChunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingService.GenerateBatchEmbeddingsAsync(chunkTexts);

        var documentChunks = new List<DocumentChunk>();

        for (var i = 0; i < textChunks.Count; i++)
        {
            documentChunks.Add(new DocumentChunk
            {
                DocumentId = document.Id,
                DocumentTitle = document.Title,
                Content = textChunks[i].Content,
                Embedding = embeddings[i],
                PageNumber = textChunks[i].PageNumber,
                SectionTitle = textChunks[i].SectionTitle,
                ChunkIndex = textChunks[i].ChunkIndex
            });
        }

        return documentChunks;
    }
}

public class PageContent
{
    public int PageNumber { get; set; }
    public string Text { get; set; } = string.Empty;
}
