using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Storage.Blobs;
using Azure.Data.Tables;
using DocumentQA.Functions.Configuration;
using DocumentQA.Functions.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Load configuration from environment variables
var openAIConfig = new OpenAIConfig
{
    Endpoint = Environment.GetEnvironmentVariable("Azure__OpenAI__Endpoint") ?? throw new InvalidOperationException("Azure__OpenAI__Endpoint not configured"),
    ApiKey = Environment.GetEnvironmentVariable("Azure__OpenAI__ApiKey") ?? throw new InvalidOperationException("Azure__OpenAI__ApiKey not configured"),
    EmbeddingDeploymentName = Environment.GetEnvironmentVariable("Azure__OpenAI__EmbeddingDeploymentName") ?? throw new InvalidOperationException("Azure__OpenAI__EmbeddingDeploymentName not configured"),
    ChatDeploymentName = Environment.GetEnvironmentVariable("Azure__OpenAI__ChatDeploymentName") ?? throw new InvalidOperationException("Azure__OpenAI__ChatDeploymentName not configured"),
    ApiVersion = Environment.GetEnvironmentVariable("Azure__OpenAI__ApiVersion") ?? "2024-12-01-preview"
};

var docIntelConfig = new DocumentIntelligenceConfig
{
    Endpoint = Environment.GetEnvironmentVariable("Azure__DocumentIntelligence__Endpoint") ?? throw new InvalidOperationException("Azure__DocumentIntelligence__Endpoint not configured"),
    ApiKey = Environment.GetEnvironmentVariable("Azure__DocumentIntelligence__ApiKey") ?? throw new InvalidOperationException("Azure__DocumentIntelligence__ApiKey not configured")
};

var searchConfig = new AISearchConfig
{
    Endpoint = Environment.GetEnvironmentVariable("Azure__AISearch__Endpoint") ?? throw new InvalidOperationException("Azure__AISearch__Endpoint not configured"),
    AdminKey = Environment.GetEnvironmentVariable("Azure__AISearch__AdminKey") ?? throw new InvalidOperationException("Azure__AISearch__AdminKey not configured"),
    IndexName = Environment.GetEnvironmentVariable("Azure__AISearch__IndexName") ?? "document-chunks"
};

var storageConfig = new StorageConfig
{
    ConnectionString = Environment.GetEnvironmentVariable("Azure__Storage__ConnectionString") ?? throw new InvalidOperationException("Azure__Storage__ConnectionString not configured"),
    ContainerName = Environment.GetEnvironmentVariable("Azure__Storage__ContainerName") ?? "documents",
    TableName = Environment.GetEnvironmentVariable("Azure__Storage__TableName") ?? "documentstatus"
};

var processingConfig = new ProcessingConfig
{
    ChunkSize = int.Parse(Environment.GetEnvironmentVariable("Processing__ChunkSize") ?? "800"),
    ChunkOverlap = int.Parse(Environment.GetEnvironmentVariable("Processing__ChunkOverlap") ?? "50"),
    MaxChunksToRetrieve = int.Parse(Environment.GetEnvironmentVariable("Processing__MaxChunksToRetrieve") ?? "20"),
    TopChunksForAnswer = int.Parse(Environment.GetEnvironmentVariable("Processing__TopChunksForAnswer") ?? "7")
};

var answerConfig = new AnswerGenerationConfig
{
    Temperature = (float)double.Parse(Environment.GetEnvironmentVariable("AnswerGeneration__Temperature") ?? "0.3"),
    MaxTokens = int.Parse(Environment.GetEnvironmentVariable("AnswerGeneration__MaxTokens") ?? "1500"),
    MinimumConfidenceThreshold = double.Parse(Environment.GetEnvironmentVariable("AnswerGeneration__MinimumConfidenceThreshold") ?? "0.5")
};

// Register Azure clients
builder.Services.AddSingleton(sp =>
{
    var blobServiceClient = new BlobServiceClient(storageConfig.ConnectionString);
    return blobServiceClient.GetBlobContainerClient(storageConfig.ContainerName);
});

builder.Services.AddSingleton(sp =>
{
    return new TableClient(storageConfig.ConnectionString, storageConfig.TableName);
});

// Register configuration objects
builder.Services.AddSingleton(openAIConfig);
builder.Services.AddSingleton(docIntelConfig);
builder.Services.AddSingleton(searchConfig);
builder.Services.AddSingleton(storageConfig);
builder.Services.AddSingleton(processingConfig);
builder.Services.AddSingleton(answerConfig);

// Register services
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<SearchService>();
builder.Services.AddSingleton<AnswerGenerationService>();
builder.Services.AddSingleton<QueryService>();
builder.Services.AddSingleton<DocumentStatusService>();
builder.Services.AddSingleton<DocumentIngestionService>();
builder.Services.AddSingleton<DocumentDeletionService>();

// Configure CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "http://localhost:5174", // Backup port if 5173 in use
            "http://127.0.0.1:5173"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// Initialize infrastructure on startup
var host = builder.Build();

// Initialize blob container and table
using (var scope = host.Services.CreateScope())
{
    var containerClient = scope.ServiceProvider.GetRequiredService<BlobContainerClient>();
    await containerClient.CreateIfNotExistsAsync();

    var tableClient = scope.ServiceProvider.GetRequiredService<TableClient>();
    await tableClient.CreateIfNotExistsAsync();

    var searchService = scope.ServiceProvider.GetRequiredService<SearchService>();

    // RECREATE INDEX: Delete and recreate to fix vector field configuration
    Console.WriteLine("=== RECREATING INDEX TO FIX VECTOR FIELD ===");
    await searchService.RecreateIndexAsync();
    Console.WriteLine("=== INDEX RECREATION COMPLETE ===");

    // DIAGNOSTIC: Print index schema to verify vector field is properly configured
    Console.WriteLine("\n=== DIAGNOSTIC: Azure AI Search Index Schema ===");
    var indexSchema = await searchService.GetIndexSchemaAsync();
    Console.WriteLine(indexSchema);
    Console.WriteLine("=== END DIAGNOSTIC ===");
}

await host.RunAsync();
