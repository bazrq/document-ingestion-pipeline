namespace DocumentQA.Functions.Configuration;

public class AzureConfig
{
    public OpenAIConfig OpenAI { get; set; } = new();
    public DocumentIntelligenceConfig DocumentIntelligence { get; set; } = new();
    public AISearchConfig AISearch { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
}

public class OpenAIConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingDeploymentName { get; set; } = string.Empty;
    public string ChatDeploymentName { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
}

public class DocumentIntelligenceConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

public class AISearchConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string AdminKey { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
}

public class StorageConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
}

public class ProcessingConfig
{
    public int ChunkSize { get; set; } = 800;
    public int ChunkOverlap { get; set; } = 50;
    public int MaxChunksToRetrieve { get; set; } = 20;
    public int TopChunksForAnswer { get; set; } = 7;
}

public class AnswerGenerationConfig
{
    public double MinimumConfidenceThreshold { get; set; } = 0.5;
    public int MaxTokens { get; set; } = 1500;
    public float Temperature { get; set; } = 0.3f;
}
