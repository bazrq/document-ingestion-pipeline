using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using DocumentQA.Functions.Configuration;
using DocumentQA.Functions.Models;

namespace DocumentQA.Functions.Services;

public class SearchService
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly string _indexName;

    public SearchService(AISearchConfig config)
    {
        var credential = new AzureKeyCredential(config.AdminKey);
        var endpoint = new Uri(config.Endpoint);

        _indexClient = new SearchIndexClient(endpoint, credential);
        _indexName = config.IndexName;
        _searchClient = _indexClient.GetSearchClient(_indexName);
    }

    public async Task CreateIndexIfNotExistsAsync()
    {
        try
        {
            // Check if index exists
            try
            {
                await _indexClient.GetIndexAsync(_indexName);
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Index doesn't exist, create it
            }

            // Define vector search configuration
            var vectorSearch = new VectorSearch();
            vectorSearch.Profiles.Add(new VectorSearchProfile("vector-profile", "vector-config"));
            vectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration("vector-config"));

            // Create the index using FieldBuilder
            var indexDefinition = new SearchIndex(_indexName)
            {
                Fields = new FieldBuilder().Build(typeof(DocumentChunk)),
                VectorSearch = vectorSearch
            };

            await _indexClient.CreateIndexAsync(indexDefinition);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error creating index: {ex.Message}", ex);
        }
    }

    public async Task IndexChunksAsync(List<DocumentChunk> chunks)
    {
        try
        {
            if (chunks.Count == 0)
                return;

            // Upload documents in batches
            var batch = IndexDocumentsBatch.Upload(chunks);
            await _searchClient.IndexDocumentsAsync(batch);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error indexing chunks: {ex.Message}", ex);
        }
    }

    public async Task<List<QueryResult>> HybridSearchAsync(
        string query,
        ReadOnlyMemory<float> queryEmbedding,
        int maxResults = 20)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Size = maxResults,
                Select = { "id", "documentId", "documentTitle", "content", "pageNumber", "sectionTitle" }
            };

            // Add vector search
            var vectorQuery = new VectorizedQuery(queryEmbedding)
            {
                KNearestNeighborsCount = maxResults,
                Fields = { "embedding" }
            };
            searchOptions.VectorSearch = new VectorSearchOptions();
            searchOptions.VectorSearch.Queries.Add(vectorQuery);

            // Perform hybrid search (vector + keyword)
            var searchResults = await _searchClient.SearchAsync<DocumentChunk>(query, searchOptions);

            var results = new List<QueryResult>();
            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                results.Add(new QueryResult
                {
                    ChunkId = result.Document.Id,
                    DocumentId = result.Document.DocumentId,
                    DocumentTitle = result.Document.DocumentTitle,
                    Content = result.Document.Content,
                    PageNumber = result.Document.PageNumber,
                    SectionTitle = result.Document.SectionTitle,
                    Score = result.Score ?? 0,
                    RelevanceScore = result.Score ?? 0
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error performing hybrid search: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Performs hybrid search with document ID filtering (OR logic for multiple documents)
    /// </summary>
    public async Task<List<QueryResult>> HybridSearchWithFilterAsync(
        string query,
        ReadOnlyMemory<float> queryEmbedding,
        List<string> documentIds,
        int maxResults = 20)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Size = maxResults,
                Select = { "id", "documentId", "documentTitle", "content", "pageNumber", "sectionTitle" }
            };

            // Add document ID filter if provided (OR logic)
            if (documentIds != null && documentIds.Count > 0)
            {
                var filterClauses = documentIds.Select(id => $"documentId eq '{id}'");
                searchOptions.Filter = string.Join(" or ", filterClauses);
            }

            // Add vector search
            var vectorQuery = new VectorizedQuery(queryEmbedding)
            {
                KNearestNeighborsCount = maxResults,
                Fields = { "embedding" }
            };
            searchOptions.VectorSearch = new VectorSearchOptions();
            searchOptions.VectorSearch.Queries.Add(vectorQuery);

            // Perform hybrid search (vector + keyword) with filter
            var searchResults = await _searchClient.SearchAsync<DocumentChunk>(query, searchOptions);

            var results = new List<QueryResult>();
            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                results.Add(new QueryResult
                {
                    ChunkId = result.Document.Id,
                    DocumentId = result.Document.DocumentId,
                    DocumentTitle = result.Document.DocumentTitle,
                    Content = result.Document.Content,
                    PageNumber = result.Document.PageNumber,
                    SectionTitle = result.Document.SectionTitle,
                    Score = result.Score ?? 0,
                    RelevanceScore = result.Score ?? 0
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error performing hybrid search with filter: {ex.Message}", ex);
        }
    }

    public async Task DeleteDocumentChunksAsync(string documentId)
    {
        try
        {
            var searchOptions = new SearchOptions
            {
                Filter = $"documentId eq '{documentId}'",
                Size = 1000
            };

            var searchResults = await _searchClient.SearchAsync<DocumentChunk>("*", searchOptions);
            var documentsToDelete = new List<DocumentChunk>();

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                documentsToDelete.Add(result.Document);
            }

            if (documentsToDelete.Count > 0)
            {
                var batch = IndexDocumentsBatch.Delete(documentsToDelete);
                await _searchClient.IndexDocumentsAsync(batch);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error deleting document chunks: {ex.Message}", ex);
        }
    }
}
