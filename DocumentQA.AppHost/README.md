# DocumentQA.AppHost - Aspire Orchestration

This is the .NET Aspire orchestration project for the Document QA system.

## What it Does

The AppHost orchestrates:
1. **DocumentQA.Functions** - Azure Functions application with automatic configuration injection
2. **React Frontend** - Vite dev server with automatic API endpoint configuration

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
    },
    "Storage": {
      "ConnectionString": "YOUR-STORAGE-CONNECTION-STRING"
    }
  }
}
```

2. **Required Azure Resources:**
   Deploy infrastructure using `infra/deploy-local-dev.sh` to create:
   - **Azure OpenAI**: Must have deployments for text-embedding-3-large and gpt-5-mini
   - **Azure Document Intelligence**: For PDF text extraction
   - **Azure AI Search**: Standard tier (S1) for vector search support
   - **Azure Storage Account**: Standard_LRS for blob and table storage

## Running the Application

### Prerequisites

1. **.NET 10 SDK** installed
2. **Node.js** installed (for React frontend)
3. **Docker Desktop** running (for Aspire Dashboard)
4. **Azure CLI** installed (for deploying local infrastructure)
5. **Azure infrastructure deployed** via `infra/deploy-local-dev.sh`

### Start the Stack

```bash
cd DocumentQA.AppHost
/usr/local/share/dotnet/dotnet run
```

This will:
- Launch Azure Functions app with injected configuration
- Launch React frontend (Vite dev server on port 5173)
- Connect to Azure Storage and other Azure services
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
