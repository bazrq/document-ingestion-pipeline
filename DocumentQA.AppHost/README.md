# DocumentQA.AppHost - Aspire Orchestration

This is the .NET Aspire orchestration project for the Document QA system.

## What it Does

The AppHost orchestrates:
1. **Azurite** - Local Azure Storage emulator (Blob + Table storage)
2. **DocumentQA.Functions** - Azure Functions application with automatic configuration injection

## Configuration

### Local Development Setup

1. **Copy and configure appsettings.Development.json:**

Create `appsettings.Development.json` (this file is gitignored) with your Azure credentials:

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
      "Endpoint": "https://YOUR-OPENAI-RESOURCE.openai.azure.com/",
      "ApiKey": "YOUR-OPENAI-API-KEY",
      "EmbeddingDeploymentName": "text-embedding-3-large",
      "ChatDeploymentName": "gpt-5-mini"
    },
    "DocumentIntelligence": {
      "Endpoint": "https://YOUR-DOC-INTEL-RESOURCE.cognitiveservices.azure.com/",
      "ApiKey": "YOUR-DOC-INTEL-API-KEY"
    },
    "AISearch": {
      "Endpoint": "https://YOUR-SEARCH-SERVICE.search.windows.net/",
      "AdminKey": "YOUR-SEARCH-ADMIN-KEY",
      "IndexName": "document-chunks"
    }
  }
}
```

2. **Required Azure Resources:**
   - **Azure OpenAI**: Must have deployments for text-embedding-3-large and gpt-5-mini
   - **Azure Document Intelligence**: For PDF text extraction
   - **Azure AI Search**: Standard tier (S1) for vector search support
   - **Azurite**: Automatically started by Aspire (no setup needed)

## Running the Application

### Prerequisites

1. **.NET 10 SDK** installed
2. **Azure Functions Core Tools** installed
3. **Docker Desktop** running (for Azurite container)

### Start the Stack

```bash
cd DocumentQA.AppHost
/usr/local/share/dotnet/dotnet run
```

This will:
- Start Azurite container (Blob + Table storage on local ports)
- Launch Azure Functions app with injected configuration
- Open Aspire Dashboard

### Access Points

- **Aspire Dashboard**: https://localhost:17XXX (port shown in console)
- **Functions App**: http://localhost:7071
- **Upload API**: POST http://localhost:7071/api/upload
- **Query API**: POST http://localhost:7071/api/query

## Troubleshooting

### "Docker not running" error
- Start Docker Desktop before running dotnet run

### "Function host failed to start" error
- Ensure Azure Functions Core Tools is installed
- Check that port 7071 is not already in use

### "Configuration value not found" errors
- Verify appsettings.Development.json exists with all required Azure credentials
- Check that endpoint URLs include https:// and trailing slashes
