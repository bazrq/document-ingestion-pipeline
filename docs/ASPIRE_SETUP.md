# .NET Aspire Integration Guide

This document explains how to run the Document QA system using .NET Aspire orchestration.

## What is Aspire?

.NET Aspire is a cloud-ready stack for building observable, production-ready distributed applications. It provides:
- Orchestration of multiple services (Azure Functions, Azurite, etc.)
- Automatic service discovery and configuration injection
- Built-in observability (logs, traces, metrics) via a dashboard
- Local development containers (Azurite for Azure Storage)

## Project Structure

```
DocumentQA.sln                      # Solution file
├── DocumentQA.AppHost/             # Aspire orchestration project
│   ├── AppHost.cs                  # Orchestration configuration
│   ├── appsettings.json            # Default config template (committed)
│   ├── appsettings.Development.json # Your local secrets (gitignored)
│   └── README.md                   # Detailed AppHost documentation
├── DocumentQA.ServiceDefaults/     # Shared telemetry/resilience config
├── DocumentQA.Functions/           # Azure Functions application
└── ASPIRE_SETUP.md                 # This file
```

## Prerequisites

Before running with Aspire, ensure you have:

1. **.NET 10 SDK** installed
   ```bash
   /usr/local/share/dotnet/dotnet --version
   # Should show 10.0.x
   ```

2. **Docker Desktop** running (for Azurite container)
   ```bash
   docker ps
   # Should show running containers
   ```

3. **Azure Resources** configured:
   - Azure OpenAI (with `text-embedding-3-large` and `gpt-5-mini` deployments)
   - Azure Document Intelligence
   - Azure AI Search (Standard tier S1 for vector search)

4. **(Optional) Azure Functions Core Tools** for full functionality
   ```bash
   npm install -g azure-functions-core-tools@4
   ```
   Note: Functions will run via `dotnet run`, but blob triggers may require Core Tools.

## Configuration

### Step 1: Create appsettings.Development.json

Create a new file `DocumentQA.AppHost/appsettings.Development.json` with your Azure credentials:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
      "ApiKey": "YOUR-API-KEY",
      "EmbeddingDeploymentName": "text-embedding-3-large",
      "ChatDeploymentName": "gpt-5-mini"
    },
    "DocumentIntelligence": {
      "Endpoint": "https://YOUR-RESOURCE.cognitiveservices.azure.com/",
      "ApiKey": "YOUR-API-KEY"
    },
    "AISearch": {
      "Endpoint": "https://YOUR-SEARCH-SERVICE.search.windows.net/",
      "AdminKey": "YOUR-ADMIN-KEY",
      "IndexName": "document-chunks"
    }
  }
}
```

**Important**: This file is automatically gitignored and will never be committed.

### Step 2: Verify Configuration

The `appsettings.json` file contains default values for processing parameters. You can override any of these in your `appsettings.Development.json`:

- **Processing.ChunkSize**: 800 tokens (adjust for different chunk sizes)
- **Processing.ChunkOverlap**: 50 tokens (context continuity)
- **Processing.MaxChunksToRetrieve**: 20 (initial retrieval count)
- **Processing.TopChunksForAnswer**: 7 (chunks sent to GPT-5-mini)
- **AnswerGeneration.Temperature**: 0.3 (GPT-5-mini creativity level)
- **AnswerGeneration.MaxTokens**: 1500 (max answer length)
- **AnswerGeneration.MinimumConfidenceThreshold**: 0.5 (low confidence warning)

## Running with Aspire

### Quick Start

```bash
# From the repository root or AppHost directory
cd DocumentQA.AppHost
/usr/local/share/dotnet/dotnet run
```

This single command will:
1. Start Azurite container (Azure Storage emulator)
2. Launch the Azure Functions app
3. Inject all environment variables automatically
4. Open the Aspire Dashboard in your browser

### What You'll See

The Aspire Dashboard will show:
- **Resources**: Status of Azurite and Functions app
- **Console**: Live logs from both services
- **Traces**: Distributed tracing across Azure service calls
- **Metrics**: Performance metrics (request rates, latencies)

### Access Points

Once running, you can access:

- **Aspire Dashboard**: https://localhost:17XXX (port shown in console)
- **Functions App**: http://localhost:7071
- **Upload API**: `POST http://localhost:7071/api/upload`
- **Query API**: `POST http://localhost:7071/api/query`

### Example API Usage

**Upload a PDF:**
```bash
curl -X POST http://localhost:7071/api/upload \
  -F "file=@document.pdf"
```

**Query documents:**
```bash
curl -X POST http://localhost:7071/api/query \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What is the main topic?",
    "documentIds": ["YOUR-DOCUMENT-ID-FROM-UPLOAD"]
  }'
```

## What Aspire Does for You

### 1. Automatic Storage Configuration

Aspire starts Azurite and automatically:
- Creates blob storage on port 10000
- Creates table storage on port 10001
- Injects connection strings into the Functions app
- Persists data in Docker volume (survives restarts)

### 2. Environment Variable Injection

Aspire injects 30+ environment variables:
- Azure OpenAI endpoint and API key
- Azure Document Intelligence endpoint and API key
- Azure AI Search endpoint and admin key
- Storage container and table names
- Processing parameters (chunk size, overlap, etc.)
- Answer generation settings (temperature, max tokens, etc.)
- Functions runtime configuration

### 3. Observability

The Aspire Dashboard provides:
- **Structured Logging**: Filter by severity, service, and message
- **Distributed Tracing**: See the full flow from upload → extraction → chunking → embedding → indexing
- **Metrics**: Track request counts, latencies, and error rates
- **Health Checks**: Monitor service health in real-time

### 4. Development Productivity

- **Single Command**: One `dotnet run` starts everything
- **Configuration Management**: Centralized in appsettings.json
- **Service Discovery**: No manual connection string management
- **Hot Reload**: Changes to code automatically restart services

## Comparison: With vs Without Aspire

### Without Aspire (Manual Approach)

```bash
# Terminal 1: Start Azurite manually
azurite-blob --location ./azurite-data

# Terminal 2: Start Azurite tables
azurite-table --location ./azurite-data

# Terminal 3: Set environment variables and run Functions
cd DocumentQA.Functions
export Azure__OpenAI__Endpoint="..."
export Azure__OpenAI__ApiKey="..."
export Azure__DocumentIntelligence__Endpoint="..."
# ... 27 more environment variables ...
func start

# No unified logs, no tracing, manual service management
```

### With Aspire

```bash
cd DocumentQA.AppHost
/usr/local/share/dotnet/dotnet run

# Done! Everything starts automatically with unified dashboard
```

## Troubleshooting

### "Docker not running" error

**Solution**: Start Docker Desktop before running Aspire.

```bash
docker ps  # Should show running containers
```

### "Port 7071 already in use"

**Solution**: Another Functions host is running. Kill it first.

```bash
lsof -ti:7071 | xargs kill -9
```

### "Configuration value not found" errors

**Solution**: Verify `appsettings.Development.json` exists with all required Azure credentials.

### Azurite connection errors

**Solution**: Azurite uses default ports. Check Docker logs in Aspire Dashboard.

```bash
docker logs $(docker ps -q --filter ancestor=mcr.microsoft.com/azure-storage/azurite)
```

### Functions app won't start

**Solution**: 
1. Ensure .NET 10 SDK is installed: `/usr/local/share/dotnet/dotnet --version`
2. Check AppHost logs in Aspire Dashboard for errors
3. Verify `DocumentQA.Functions/local.settings.json` doesn't conflict with Aspire config

### Azure OpenAI rate limit errors

**Solution**: 
- Use paid Azure OpenAI tier (free tier has strict limits)
- Reduce `Processing.MaxChunksToRetrieve` in config
- Add delays between requests (modify `EmbeddingService.cs`)

## Advanced Configuration

### Using User Secrets

Instead of `appsettings.Development.json`, you can use .NET User Secrets:

```bash
cd DocumentQA.AppHost
/usr/local/share/dotnet/dotnet user-secrets set "Azure:OpenAI:ApiKey" "YOUR-KEY"
```

User Secrets are stored in `~/.microsoft/usersecrets/` and never committed to git.

### Environment-Specific Settings

Create additional appsettings files:
- `appsettings.Staging.json` - Staging environment
- `appsettings.Production.json` - Production environment (for testing deployment configs)

Run with specific environment:
```bash
ASPNETCORE_ENVIRONMENT=Staging /usr/local/share/dotnet/dotnet run
```

### Custom Processing Parameters

Override processing defaults in your `appsettings.Development.json`:

```json
{
  "Processing": {
    "ChunkSize": 1000,
    "ChunkOverlap": 100,
    "MaxChunksToRetrieve": 30,
    "TopChunksForAnswer": 10
  },
  "AnswerGeneration": {
    "Temperature": 0.1,
    "MaxTokens": 2000
  }
}
```

## Production Deployment

**Important**: Aspire is for **local development orchestration only**.

For production:
1. Deploy Functions to Azure Functions service
2. Use Azure Storage (not Azurite)
3. Configure via Azure Function App Settings
4. Enable Application Insights for monitoring
5. Use Azure Key Vault for secrets

Aspire is **not** a production hosting platform - it's a development tool.

## Benefits of Using Aspire

1. **Faster Onboarding**: New developers run one command to start the entire stack
2. **Consistent Environments**: Everyone uses the same Azurite version and configuration
3. **Better Debugging**: Unified dashboard shows logs and traces across all services
4. **Simplified Configuration**: 30+ environment variables managed in one file
5. **Productivity**: No manual service orchestration, no terminal juggling

## Next Steps

1. Review `DocumentQA.AppHost/README.md` for AppHost-specific details
2. Check `CLAUDE.md` for overall project architecture
3. Explore the Aspire Dashboard to understand your application's behavior
4. Experiment with different processing parameters in `appsettings.Development.json`

## Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Azure Functions Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/)
- [Azurite Documentation](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)
