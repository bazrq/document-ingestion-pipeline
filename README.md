# Document QA System

A production-ready Retrieval-Augmented Generation (RAG) system for PDF documents built with Azure Functions and .NET 10. Upload PDFs, ask questions, and get AI-powered answers with citations.

## Features

- **PDF Document Upload**: Upload PDF documents via HTTP API
- **Intelligent Text Extraction**: Automated text extraction using Azure Document Intelligence
- **Semantic Chunking**: Smart text chunking with configurable overlap for context preservation
- **Vector Search**: Fast semantic search using Azure AI Search with vector embeddings
- **AI-Powered Answers**: GPT-5-mini powered answers with source citations
- **Distributed Processing**: Scalable Azure Functions architecture with blob-triggered processing
- **Local Development**: Full local development stack with .NET Aspire orchestration

## Architecture

The system consists of three main components:

1. **Upload Pipeline**: Validates and stores PDF documents in Azure Blob Storage
2. **Processing Pipeline**: Extracts text, generates embeddings, and indexes in Azure AI Search
3. **Query Pipeline**: Retrieves relevant chunks and generates answers using GPT-5-mini

```
┌─────────────┐
│   Upload    │ → Blob Storage → ┌──────────────┐
│  Function   │                  │  Processing  │ → Azure AI Search
└─────────────┘                  │   Function   │
                                 └──────────────┘
┌─────────────┐                          ↓
│    Query    │ ← Azure AI Search ← Embeddings
│  Function   │      + GPT-5-mini
└─────────────┘
```

## Technology Stack

- **.NET 10** - Azure Functions (isolated worker model)
- **Azure OpenAI** - Embeddings (text-embedding-3-large) and GPT-5-mini
- **Azure Document Intelligence** - PDF text extraction
- **Azure AI Search** - Vector search and indexing
- **Azure Storage** - Blob storage for documents, Table storage for status tracking
- **.NET Aspire** - Local orchestration and development

## Prerequisites

### For Local Development
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for Aspire)
- [Azure Subscription](https://azure.microsoft.com/free/) with Azure OpenAI Service
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (for deploying local-dev infrastructure)

### For Azure Deployment
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd) (recommended)
- [Azure Subscription](https://azure.microsoft.com/free/)
- Existing Azure OpenAI resource with deployed models:
  - `text-embedding-3-large` (or compatible embedding model)
  - `gpt-5-mini` (or compatible chat model)

### Optional
- [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local) (for standalone function development)

## Quick Start

### 1. Clone the Repository

```bash
git clone <repository-url>
cd document-ingestion-pipeline
```

### 2. Configure Azure Services

Create `DocumentQA.AppHost/appsettings.Development.json`:

```json
{
  "Azure": {
    "OpenAI": {
      "Endpoint": "https://your-openai-resource.openai.azure.com/",
      "ApiKey": "your-openai-api-key",
      "EmbeddingDeploymentName": "text-embedding-3-large",
      "ChatDeploymentName": "gpt-5-mini"
    },
    "DocumentIntelligence": {
      "Endpoint": "https://your-doc-intelligence.cognitiveservices.azure.com/",
      "ApiKey": "your-doc-intelligence-key"
    },
    "AISearch": {
      "Endpoint": "https://your-search-service.search.windows.net",
      "AdminKey": "your-search-admin-key",
      "IndexName": "document-chunks"
    }
  }
}
```

See `DocumentQA.AppHost/README.md` for detailed configuration instructions.

### 3. Run with Aspire

```bash
cd DocumentQA.AppHost
dotnet run
```

This will:
- Launch the Azure Functions app with configuration from appsettings.Development.json
- Connect to Azure Storage (deployed via infra/main.local-dev.bicep)
- Open the Aspire Dashboard at `https://localhost:17XXX`

### 4. Test the API

Upload a document:
```bash
curl -X POST http://localhost:7071/api/upload \
  -F "file=@sample.pdf"
```

Query the document (replace `DOCUMENT_ID` with the ID from upload response):
```bash
curl -X POST http://localhost:7071/api/query \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What is the main topic of this document?",
    "documentIds": ["DOCUMENT_ID"]
  }'
```

## Project Structure

```
DocumentQA.sln
├── DocumentQA.AppHost/           # Aspire orchestration (dev only)
│   ├── AppHost.cs               # Configuration and service setup
│   └── appsettings.Development.json
├── DocumentQA.ServiceDefaults/   # Shared telemetry configuration
└── DocumentQA.Functions/         # Azure Functions application
    ├── Functions/               # HTTP and blob trigger endpoints
    │   ├── UploadFunction.cs
    │   ├── ProcessingFunction.cs
    │   └── QueryFunction.cs
    ├── Services/                # Core business logic
    │   ├── DocumentIngestionService.cs
    │   ├── SearchService.cs
    │   ├── EmbeddingService.cs
    │   ├── AnswerGenerationService.cs
    │   ├── QueryService.cs
    │   └── DocumentStatusService.cs
    ├── Configuration/           # Configuration POCOs
    ├── Models/                  # Data transfer objects
    └── Utils/                   # Helper utilities
```

## Configuration

The system is configured via environment variables. Key settings:

### Required Azure Services

| Variable | Description | Default |
|----------|-------------|---------|
| `Azure__OpenAI__Endpoint` | Azure OpenAI endpoint URL | - |
| `Azure__OpenAI__ApiKey` | Azure OpenAI API key | - |
| `Azure__DocumentIntelligence__Endpoint` | Document Intelligence endpoint | - |
| `Azure__DocumentIntelligence__ApiKey` | Document Intelligence API key | - |
| `Azure__AISearch__Endpoint` | AI Search service endpoint | - |
| `Azure__AISearch__AdminKey` | AI Search admin key | - |

### Optional Processing Parameters

| Variable | Description | Default |
|----------|-------------|---------|
| `Processing__ChunkSize` | Characters per chunk | 800 |
| `Processing__ChunkOverlap` | Overlap between chunks | 50 |
| `Processing__MaxChunksToRetrieve` | Max chunks from search | 20 |
| `Processing__TopChunksForAnswer` | Chunks sent to GPT-5-mini | 7 |

See `DocumentQA.Functions/Configuration/AzureConfig.cs` for all configuration options.

## API Reference

### Upload Document

**Endpoint**: `POST /api/upload`

**Request**:
```bash
curl -X POST http://localhost:7071/api/upload \
  -F "file=@document.pdf"
```

**Response** (202 Accepted):
```json
{
  "documentId": "abc123",
  "message": "Document upload initiated",
  "status": "processing"
}
```

### Query Documents

**Endpoint**: `POST /api/query`

**Request**:
```json
{
  "question": "What are the key findings?",
  "documentIds": ["doc-id-1", "doc-id-2"]
}
```

**Response** (200 OK):
```json
{
  "answer": "The key findings include...",
  "sources": [
    {
      "documentId": "doc-id-1",
      "chunkId": "chunk-1",
      "pageNumber": 5,
      "relevanceScore": 0.92
    }
  ]
}
```

## Development

### Building

Build the entire solution:
```bash
dotnet build DocumentQA.sln
```

Build specific project:
```bash
dotnet build DocumentQA.Functions/DocumentQA.Functions.csproj
```

### Running Functions Standalone

```bash
cd DocumentQA.Functions
func start
```

Note: Requires manual configuration of 30+ environment variables in `local.settings.json`. Using Aspire is recommended.

### Adding a New Service

1. Create service class in `DocumentQA.Functions/Services/`
2. Inject dependencies via constructor
3. Register in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<MyService>();
   ```

### Adding a New Function

1. Create function class in `DocumentQA.Functions/Functions/`
2. Use appropriate trigger attribute:
   ```csharp
   [Function("MyFunction")]
   public async Task<HttpResponseData> Run(
       [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
   ```
3. Inject required services via constructor

## Debugging

### View Processing Status

Check the Aspire Dashboard for:
- Real-time traces and logs
- Function execution metrics
- Resource health

### Common Issues

1. **Processing Failures**: Check Azure AI Search index exists and deployment names match configuration
2. **Rate Limits**: Azure OpenAI has rate limits; adjust batch sizes if needed
3. **Memory Issues**: Large PDFs may require increased function memory allocation

### Azure Storage Access

View your Azure Storage contents:
```bash
# Get connection string from your deployment
STORAGE_CONNECTION_STRING="<your-connection-string-from-bicep-output>"

# List uploaded documents
az storage blob list \
  --container-name documents \
  --connection-string "$STORAGE_CONNECTION_STRING"

# Check document processing status
az storage entity query \
  --table-name documentstatus \
  --connection-string "$STORAGE_CONNECTION_STRING"
```

Alternatively, use [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/) for a GUI experience.

## Deployment

### Azure Developer CLI (Recommended)

The fastest way to deploy to Azure is using the Azure Developer CLI (`azd`):

```bash
# First-time setup
azd init
azd env new dev

# Set required Azure OpenAI configuration
azd env set AZURE_OPENAI_ENDPOINT "https://your-openai.openai.azure.com/"
azd env set AZURE_OPENAI_API_KEY "your-api-key"

# Deploy everything (infrastructure + code)
azd up
```

This will:
- Provision all Azure resources (Storage, Document Intelligence, AI Search, Functions, App Insights)
- Build and deploy the Functions app
- Configure all application settings automatically

For detailed deployment instructions, see **[infra/README.md](./infra/README.md)**.

**Requirements**:
- Existing Azure OpenAI resource with `text-embedding-3-large` and `gpt-5-mini` deployments
- Azure subscription with permissions to create resources

### Manual Deployment (Alternative)

You can also deploy using Azure Functions Core Tools:

```bash
cd DocumentQA.Functions
func azure functionapp publish <function-app-name>
```

**Important**: Do not deploy the Aspire AppHost project. It is for local development only.

For manual production deployment:
1. Create Azure resources (Storage Account, Document Intelligence, AI Search, etc.)
2. Set environment variables in Azure Function App Configuration
3. Deploy using Azure Functions Core Tools, Azure CLI, or CI/CD pipeline

### Multi-Environment Deployments

Deploy to different environments (dev, staging, prod):

```bash
# Development
azd env new dev
azd env set AZURE_OPENAI_ENDPOINT "..."
azd up

# Production
azd env new prod
azd env set AZURE_OPENAI_ENDPOINT "..."
azd up
```

See [infra/README.md](./infra/README.md) for environment-specific configuration.

## Performance Considerations

- **Chunking Strategy**: Chunks are ~800 characters with 50-character overlap by default
- **Vector Search**: Requires Azure AI Search Standard (S1) tier or higher
- **Embeddings**: Uses `text-embedding-3-large` (3072 dimensions)
- **Answer Generation**: GPT-5-mini with temperature 0.3 for consistent responses
- **Concurrent Processing**: Azure Functions scale automatically based on blob queue

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see below for details:

```
MIT License

Copyright (c) 2025

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Acknowledgments

- Built with [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)
- Powered by [Azure OpenAI Service](https://azure.microsoft.com/en-us/products/ai-services/openai-service)
- Uses [Azure Document Intelligence](https://azure.microsoft.com/en-us/products/ai-services/ai-document-intelligence)
- Vector search by [Azure AI Search](https://azure.microsoft.com/en-us/products/ai-services/ai-search)

## Support

For detailed development guidance, see:
- `CLAUDE.md` - Comprehensive development guide
- `DocumentQA.AppHost/README.md` - Aspire setup instructions
- `docs/ASPIRE_SETUP.md` - Integration guide

For issues and questions, please open an issue in the repository.
