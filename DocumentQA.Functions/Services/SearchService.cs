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

    /// <summary>
    /// Deletes and recreates the search index. WARNING: This will delete all indexed documents!
    /// </summary>
    public async Task RecreateIndexAsync()
    {
        try
        {
            // Delete existing index if it exists
            try
            {
                await _indexClient.DeleteIndexAsync(_indexName);
                Console.WriteLine($"Deleted existing index: {_indexName}");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"Index {_indexName} does not exist, will create new one");
            }

            // Wait a moment for deletion to complete
            await Task.Delay(2000);

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
            Console.WriteLine($"Created new index: {_indexName} with vector search configuration");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error recreating index: {ex.Message}", ex);
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
                Select = { "Id", "DocumentId", "DocumentTitle", "Content", "PageNumber", "SectionTitle" }
            };

            // Add vector search
            var vectorQuery = new VectorizedQuery(queryEmbedding)
            {
                KNearestNeighborsCount = maxResults,
                Fields = { "Embedding" }
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
                Select = { "Id", "DocumentId", "DocumentTitle", "Content", "PageNumber", "SectionTitle" }
            };

            // Add document ID filter if provided (OR logic)
            if (documentIds != null && documentIds.Count > 0)
            {
                var filterClauses = documentIds.Select(id => $"DocumentId eq '{id}'");
                searchOptions.Filter = string.Join(" or ", filterClauses);
            }

            // Add vector search
            var vectorQuery = new VectorizedQuery(queryEmbedding)
            {
                KNearestNeighborsCount = maxResults,
                Fields = { "Embedding" }
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
                Filter = $"DocumentId eq '{documentId}'",
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

    /// <summary>
    /// Diagnostic method to retrieve and display the actual index schema
    /// </summary>
    public async Task<string> GetIndexSchemaAsync()
    {
        try
        {
            var index = await _indexClient.GetIndexAsync(_indexName);
            var schema = new System.Text.StringBuilder();

            schema.AppendLine($"Index Name: {index.Value.Name}");
            schema.AppendLine("\nFields:");

            foreach (var field in index.Value.Fields)
            {
                schema.AppendLine($"  - Name: {field.Name}, Type: {field.Type}, IsKey: {field.IsKey}, IsSearchable: {field.IsSearchable}");

                // Show vector search configuration if present
                if (field.VectorSearchDimensions.HasValue)
                {
                    schema.AppendLine($"    Vector Dimensions: {field.VectorSearchDimensions}");
                    schema.AppendLine($"    Vector Profile: {field.VectorSearchProfileName}");
                }
            }

            // Show vector search configuration
            if (index.Value.VectorSearch != null)
            {
                schema.AppendLine("\nVector Search Configuration:");
                schema.AppendLine($"  Profiles: {index.Value.VectorSearch.Profiles.Count}");
                foreach (var profile in index.Value.VectorSearch.Profiles)
                {
                    schema.AppendLine($"    - Name: {profile.Name}, Algorithm: {profile.AlgorithmConfigurationName}");
                }
                schema.AppendLine($"  Algorithms: {index.Value.VectorSearch.Algorithms.Count}");
                foreach (var algo in index.Value.VectorSearch.Algorithms)
                {
                    schema.AppendLine($"    - Name: {algo.Name}");
                }
            }
            else
            {
                schema.AppendLine("\nVector Search: NOT CONFIGURED");
            }

            return schema.ToString();
        }
        catch (Exception ex)
        {
            return $"Error retrieving index schema: {ex.Message}";
        }
    }
}
