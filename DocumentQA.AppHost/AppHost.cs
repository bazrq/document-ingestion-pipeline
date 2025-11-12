var builder = DistributedApplication.CreateBuilder(args);

// Add Azure Functions project with all required configuration
var functions = builder.AddProject<Projects.DocumentQA_Functions>("documentqa-functions")
    .WithHttpEndpoint(port: 7071, name: "http")
    // Azure OpenAI Configuration
    .WithEnvironment("Azure__OpenAI__Endpoint", builder.Configuration["Azure:OpenAI:Endpoint"] ?? "")
    .WithEnvironment("Azure__OpenAI__ApiKey", builder.Configuration["Azure:OpenAI:ApiKey"] ?? "")
    .WithEnvironment("Azure__OpenAI__EmbeddingDeploymentName",
        builder.Configuration["Azure:OpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-large")
    .WithEnvironment("Azure__OpenAI__ChatDeploymentName",
        builder.Configuration["Azure:OpenAI:ChatDeploymentName"] ?? "gpt-5-mini")
    .WithEnvironment("Azure__OpenAI__ApiVersion",
        builder.Configuration["Azure:OpenAI:ApiVersion"] ?? "2024-12-01-preview")
    // Azure Document Intelligence Configuration
    .WithEnvironment("Azure__DocumentIntelligence__Endpoint",
        builder.Configuration["Azure:DocumentIntelligence:Endpoint"] ?? "")
    .WithEnvironment("Azure__DocumentIntelligence__ApiKey",
        builder.Configuration["Azure:DocumentIntelligence:ApiKey"] ?? "")
    // Azure AI Search Configuration
    .WithEnvironment("Azure__AISearch__Endpoint",
        builder.Configuration["Azure:AISearch:Endpoint"] ?? "")
    .WithEnvironment("Azure__AISearch__AdminKey",
        builder.Configuration["Azure:AISearch:AdminKey"] ?? "")
    .WithEnvironment("Azure__AISearch__IndexName",
        builder.Configuration["Azure:AISearch:IndexName"] ?? "document-chunks")
    // Azure Storage Configuration
    .WithEnvironment("Azure__Storage__ConnectionString",
        builder.Configuration["Azure:Storage:ConnectionString"] ?? "")
    .WithEnvironment("Azure__Storage__ContainerName",
        builder.Configuration["Azure:Storage:ContainerName"] ?? "documents")
    .WithEnvironment("Azure__Storage__TableName",
        builder.Configuration["Azure:Storage:TableName"] ?? "documentstatus")
    // Processing Configuration
    .WithEnvironment("Processing__ChunkSize",
        builder.Configuration["Processing:ChunkSize"] ?? "800")
    .WithEnvironment("Processing__ChunkOverlap",
        builder.Configuration["Processing:ChunkOverlap"] ?? "50")
    .WithEnvironment("Processing__MaxChunksToRetrieve",
        builder.Configuration["Processing:MaxChunksToRetrieve"] ?? "20")
    .WithEnvironment("Processing__TopChunksForAnswer",
        builder.Configuration["Processing:TopChunksForAnswer"] ?? "7")
    // Answer Generation Configuration
    .WithEnvironment("AnswerGeneration__MinimumConfidenceThreshold",
        builder.Configuration["AnswerGeneration:MinimumConfidenceThreshold"] ?? "0.5")
    .WithEnvironment("AnswerGeneration__MaxTokens",
        builder.Configuration["AnswerGeneration:MaxTokens"] ?? "1500")
    .WithEnvironment("AnswerGeneration__Temperature",
        builder.Configuration["AnswerGeneration:Temperature"] ?? "0.3")
    // Functions Runtime Configuration
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated");

// Add React frontend with Vite dev server
var frontend = builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithEnvironment("VITE_API_URL", functions.GetEndpoint("http"));

builder.Build().Run();
