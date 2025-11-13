# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## System Overview

This is a **Document QA System** built with Azure Functions (.NET 10) and React frontend. It provides RAG (Retrieval-Augmented Generation) capabilities for PDF documents:

1. **Upload**: Users upload PDFs via React frontend (HTTP endpoint)
2. **Process**: Blob trigger extracts text, chunks it, generates embeddings, and indexes in Azure AI Search
3. **Query**: Users ask questions via React frontend, system retrieves relevant chunks and generates answers using GPT-5-mini

**Key Technologies**: Azure Functions (isolated worker), React + Vite + TypeScript + Tailwind CSS, Azure OpenAI, Azure Document Intelligence, Azure AI Search, Azure Storage (Blob + Table)

## Build and Run Commands

### Running Locally

Run the Functions app using Azure Functions Core Tools:

```bash
cd DocumentQA.Functions
func start
```

**Prerequisites**:
- .NET 10 SDK installed
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local) installed
- `DocumentQA.Functions/local.settings.json` configured with Azure credentials (see template at `local.settings.json.template`)

The Functions app will start on `http://localhost:7071` with the following endpoints:
- `POST http://localhost:7071/api/upload` - Upload PDF documents
- `POST http://localhost:7071/api/query` - Query documents
- `GET http://localhost:7071/api/documents` - List documents
- `GET http://localhost:7071/api/status/{id}` - Check document status

**Note**: Blob triggers require Azure Storage Emulator (Azurite) or connection to real Azure Storage.

### Building

Build entire solution:
```bash
/usr/local/share/dotnet/dotnet build DocumentQA.sln
```

Build specific project:
```bash
/usr/local/share/dotnet/dotnet build DocumentQA.Functions/DocumentQA.Functions.csproj
```

### Testing

Currently no test projects exist. To add tests:

```bash
# Example: Create unit test project
/usr/local/share/dotnet/dotnet new xunit -n DocumentQA.Functions.Tests
/usr/local/share/dotnet/dotnet sln add DocumentQA.Functions.Tests/DocumentQA.Functions.Tests.csproj
```

Run tests:
```bash
/usr/local/share/dotnet/dotnet test
```

## Architecture

### Project Structure

```
DocumentQA.sln
└── DocumentQA.Functions/         # Azure Functions app (backend API)
    ├── Functions/               # HTTP and blob trigger endpoints
    ├── Services/                # Core business logic
    ├── Configuration/           # Config POCOs
    ├── Models/                  # Data models
    ├── Utils/                   # Helper classes
    └── local.settings.json      # Local configuration (gitignored)
```

**Note**: A React frontend is planned but not yet implemented.

### Core Components

**Functions (Entry Points)**:
- `UploadFunction.cs`: HTTP POST `/api/upload` - Handles PDF uploads, validates, stores in Blob, creates status record
- `ProcessingFunction.cs`: Blob trigger - Extracts text → chunks → embeddings → indexes in AI Search
- `QueryFunction.cs`: HTTP POST `/api/query` - Handles questions, retrieves chunks, generates answers

**Services (Business Logic)**:
- `DocumentIngestionService.cs`: Orchestrates extraction, chunking, embedding generation
- `SearchService.cs`: Manages Azure AI Search index creation and vector search
- `EmbeddingService.cs`: Generates embeddings via Azure OpenAI
- `AnswerGenerationService.cs`: Generates GPT-5-mini answers with citations
- `QueryService.cs`: Orchestrates retrieval and answer generation pipeline
- `DocumentStatusService.cs`: Tracks document processing state in Table Storage

**Configuration Flow**:
1. Azure Functions Core Tools reads `DocumentQA.Functions/local.settings.json`
2. Values are made available as environment variables (e.g., `Azure__OpenAI__Endpoint`)
3. `Program.cs` reads environment variables and creates config POCOs
4. Config objects injected into services via DI

### Processing Pipeline

**Upload Flow**:
```
User → UploadFunction → Validate PDF → Store in Blob → Create status record → Return 202 Accepted
```

**Processing Flow** (triggered by blob upload):
```
Blob Trigger → DocumentIngestionService.ExtractTextFromStreamAsync (Azure Doc Intelligence)
            → DocumentIngestionService.ChunkDocument (chunking with overlap)
            → DocumentIngestionService.GenerateEmbeddingsForChunksAsync (Azure OpenAI)
            → SearchService.IndexChunksAsync (Azure AI Search vector indexing)
            → Update status to "completed"
```

**Query Flow**:
```
User → QueryFunction → QueryService.AskQuestionAsync
                    → EmbeddingService.GenerateEmbeddingAsync (embed question)
                    → SearchService.SearchAsync (vector search)
                    → AnswerGenerationService.GenerateAnswerAsync (GPT-5-mini)
                    → Return answer with citations
```

### Configuration Details

All config is environment-variable based. Key settings:

**Azure Services** (required):
- `Azure__OpenAI__Endpoint`, `Azure__OpenAI__ApiKey`
- `Azure__OpenAI__EmbeddingDeploymentName` (default: "text-embedding-3-large")
- `Azure__OpenAI__ChatDeploymentName` (default: "gpt-5-mini")
- `Azure__DocumentIntelligence__Endpoint`, `Azure__DocumentIntelligence__ApiKey`
- `Azure__AISearch__Endpoint`, `Azure__AISearch__AdminKey`
- `Azure__AISearch__IndexName` (default: "document-chunks")
- `Azure__Storage__ConnectionString` (from Azure Storage account deployed via infra/main.local-dev.bicep)

**Processing Params** (optional):
- `Processing__ChunkSize` (default: 800 tokens)
- `Processing__ChunkOverlap` (default: 50 tokens)
- `Processing__MaxChunksToRetrieve` (default: 20)
- `Processing__TopChunksForAnswer` (default: 7)

**Answer Generation** (optional):
- `AnswerGeneration__Temperature` (default: 0.3)
- `AnswerGeneration__MaxTokens` (default: 1500)
- `AnswerGeneration__MinimumConfidenceThreshold` (default: 0.5)

See `DocumentQA.Functions/Configuration/AzureConfig.cs` for all config classes.

## Development Patterns

### Adding New Configuration

1. Add property to appropriate config class in `Configuration/AzureConfig.cs`
2. Update `Program.cs` to read from environment variable
3. Add entry to `local.settings.json.template` with description
4. Update documentation in this file (CLAUDE.md)

### Adding New Azure Function

1. Create new class in `DocumentQA.Functions/Functions/`
2. Inject required services via constructor
3. Use `[Function("FunctionName")]` attribute
4. For HTTP: `[HttpTrigger(AuthorizationLevel.Function, "post", Route = "route")]`
5. For Blob: `[BlobTrigger("container/{path}", Connection = "Azure__Storage__ConnectionString")]`
6. Register any new services in `Program.cs`

### Adding New Service

1. Create class in `DocumentQA.Functions/Services/`
2. Inject dependencies via constructor (config, Azure clients, other services)
3. Register as singleton in `Program.cs`: `builder.Services.AddSingleton<MyService>()`
4. Services are registered AFTER config objects but BEFORE infrastructure initialization

### Chunking Strategy

Documents are chunked by character count (not tokens). See `DocumentQA.Functions/Utils/ChunkingStrategy.cs`:
- Respects sentence boundaries when possible
- Includes configurable overlap for context preservation
- Stores page numbers for citation tracking

### Error Handling

- Processing failures trigger Azure Functions retry (max 5 attempts per `host.json`)
- Document status tracks failure details (error message, failed step, attempt count)
- All functions log extensively; view logs via `func start` output or Azure Portal

## Important Constraints

1. **PDF Only**: System only accepts PDF files (validated in `UploadFunction.cs:79`)
2. **100MB Limit**: Max file size enforced (`UploadFunction.cs:70`)
3. **Vector Search**: Requires Azure AI Search Standard tier (S1) or higher
4. **Deployment Mismatch**: Azure OpenAI deployment names must match config (common error)

## Common Tasks

### Changing Chunk Size

Edit `DocumentQA.Functions/local.settings.json`:
```json
{
  "Values": {
    "Processing__ChunkSize": "1000",
    "Processing__ChunkOverlap": "100"
  }
}
```

Restart the Functions app to apply changes.

### Debugging Processing Failures

1. Check Functions console output (from `func start`) for real-time logs
2. Check Table Storage for document status record (includes error details)
3. Review `ProcessingFunction.cs:currentStep` to identify failure point
4. Common issues: API key errors, rate limits, deployment name mismatches
5. For production: Use Azure Portal → Function App → Log stream or Application Insights

### Testing Locally

With the Functions app running (`func start`):

```bash
# Upload document
curl -X POST http://localhost:7071/api/upload -F "file=@test.pdf"

# Query (replace DOCUMENT_ID)
curl -X POST http://localhost:7071/api/query \
  -H "Content-Type: application/json" \
  -d '{"question": "What is the main topic?", "documentIds": ["DOCUMENT_ID"]}'
```

### Viewing Storage Contents

Azure Storage is persistent and accessible via Azure Storage Explorer or Azure CLI:

```bash
# Get connection string from local.settings.json or bicep output
STORAGE_CONNECTION_STRING="<your-connection-string>"

# List blobs (requires azure-cli)
az storage blob list --container-name documents --connection-string "$STORAGE_CONNECTION_STRING"

# Query table
az storage entity query --table-name documentstatus --connection-string "$STORAGE_CONNECTION_STRING"
```

Alternatively, use [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/) to browse blobs and tables with a GUI.

## File References

- **DI configuration**: `DocumentQA.Functions/Program.cs:10-122`
- **Processing pipeline**: `DocumentQA.Functions/Functions/ProcessingFunction.cs:28-111`
- **Query pipeline**: `DocumentQA.Functions/Services/QueryService.cs`
- **Chunking logic**: `DocumentQA.Functions/Utils/ChunkingStrategy.cs`
- **Config schema**: `DocumentQA.Functions/Configuration/AzureConfig.cs`
- **Local settings template**: `DocumentQA.Functions/local.settings.json.template`
- **CORS configuration**: `DocumentQA.Functions/host.json:17-27`

## Additional Documentation

- `infra/README.md`: Infrastructure deployment guide
- `docs/DEPLOYMENT_QUICKSTART.md`: Quick deployment guide
- Function-level comments explain business logic throughout codebase

## Frontend Development

**Note**: A React frontend is planned but not yet implemented.

When the frontend is created, it should:
- Connect to the Functions API at `http://localhost:7071` for local development
- Use environment variables for API configuration (not hardcoded)
- Be aware of CORS configuration in `DocumentQA.Functions/Program.cs:91-105` and `host.json:17-27`
- Allowed origins for local dev: `http://localhost:5173`, `http://localhost:5174`, `http://127.0.0.1:5173`

## Context Priming
Read README.md, docs/* to understand the codebase.