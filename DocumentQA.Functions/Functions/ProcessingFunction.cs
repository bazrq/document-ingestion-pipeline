using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using DocumentQA.Functions.Services;
using DocumentQA.Functions.Models;

namespace DocumentQA.Functions.Functions;

public class ProcessingFunction
{
    private readonly DocumentIngestionService _ingestionService;
    private readonly SearchService _searchService;
    private readonly DocumentStatusService _statusService;
    private readonly ILogger<ProcessingFunction> _logger;
    private int _attemptCount = 0;

    public ProcessingFunction(
        DocumentIngestionService ingestionService,
        SearchService searchService,
        DocumentStatusService statusService,
        ILogger<ProcessingFunction> logger)
    {
        _ingestionService = ingestionService;
        _searchService = searchService;
        _statusService = statusService;
        _logger = logger;
    }

    [Function("ProcessDocument")]
    public async Task Run(
        [BlobTrigger("documents/{documentId}/{fileName}", Connection = "Azure__Storage__ConnectionString")]
        Stream blobStream,
        string documentId,
        string fileName)
    {
        _attemptCount++;
        string currentStep = "initialization";

        _logger.LogInformation("Processing document {FileName} with ID {DocumentId} (attempt {AttemptCount})",
            fileName, documentId, _attemptCount);

        try
        {
            // Update status to processing
            currentStep = "status_update";
            await _statusService.UpdateStatusAsync(documentId, "processing");
            _logger.LogInformation("Status updated to 'processing' for document {DocumentId}", documentId);

            // Step 1: Extract text from stream
            currentStep = "text_extraction";
            _logger.LogInformation("Extracting text from document {DocumentId}", documentId);
            var extractedPages = await _ingestionService.ExtractTextFromStreamAsync(blobStream);
            _logger.LogInformation("Extracted {PageCount} pages from document {DocumentId}",
                extractedPages.Count, documentId);

            // Step 2: Chunk document
            currentStep = "chunking";
            _logger.LogInformation("Chunking document {DocumentId}", documentId);
            var textChunks = _ingestionService.ChunkDocument(extractedPages);
            _logger.LogInformation("Created {ChunkCount} chunks from document {DocumentId}",
                textChunks.Count, documentId);

            // Step 3: Generate embeddings
            currentStep = "embedding_generation";
            _logger.LogInformation("Generating embeddings for {ChunkCount} chunks in document {DocumentId}",
                textChunks.Count, documentId);
            var documentChunks = await _ingestionService.GenerateEmbeddingsForChunksAsync(
                documentId,
                Path.GetFileNameWithoutExtension(fileName),
                textChunks);
            _logger.LogInformation("Generated embeddings for document {DocumentId}", documentId);

            // Step 4: Index in Azure AI Search
            currentStep = "indexing";
            _logger.LogInformation("Indexing {ChunkCount} chunks for document {DocumentId}",
                documentChunks.Count, documentId);
            await _searchService.IndexChunksAsync(documentChunks);
            _logger.LogInformation("Indexed chunks for document {DocumentId}", documentId);

            // Update status to completed with metadata
            currentStep = "completion";
            await _statusService.UpdateProcessingDetailsAsync(documentId, extractedPages.Count, textChunks.Count);
            await _statusService.UpdateStatusAsync(documentId, "completed");

            _logger.LogInformation(
                "Successfully processed document {DocumentId}: {PageCount} pages, {ChunkCount} chunks",
                documentId, extractedPages.Count, textChunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing document {DocumentId} at step '{Step}' (attempt {AttemptCount}): {ErrorMessage}",
                documentId, currentStep, _attemptCount, ex.Message);

            // Mark as failed with detailed error info
            try
            {
                await _statusService.MarkAsFailedAsync(
                    documentId,
                    ex.Message,
                    currentStep,
                    _attemptCount);
            }
            catch (Exception statusEx)
            {
                _logger.LogError(statusEx, "Failed to update status to 'failed' for document {DocumentId}", documentId);
            }

            // Re-throw to trigger retry mechanism (up to maxDequeueCount in host.json)
            throw;
        }
    }
}
