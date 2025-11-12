# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## System Overview

This is a **Document QA System** built with Azure Functions (.NET 10) and orchestrated using .NET Aspire. It provides RAG (Retrieval-Augmented Generation) capabilities for PDF documents:

1. **Upload**: Users upload PDFs via HTTP endpoint
2. **Process**: Blob trigger extracts text, chunks it, generates embeddings, and indexes in Azure AI Search
3. **Query**: Users ask questions, system retrieves relevant chunks and generates answers using GPT-4

**Key Technologies**: Azure Functions (isolated worker), Azure OpenAI, Azure Document Intelligence, Azure AI Search, Azure Storage (Blob + Table), .NET Aspire

## Build and Run Commands

### Running with Aspire (Recommended)

Start the entire stack with one command:

```bash
cd DocumentQA.AppHost
/usr/local/share/dotnet/dotnet run
```

This automatically:
- Starts Azurite container (Blob + Table storage emulator)
- Launches Azure Functions app with environment variables injected
- Opens Aspire Dashboard at https://localhost:17XXX

**Prerequisites**:
- .NET 10 SDK installed
- Docker Desktop running
- `DocumentQA.AppHost/appsettings.Development.json` configured with Azure credentials (see `DocumentQA.AppHost/README.md`)

### Building

Build entire solution:
```bash
/usr/local/share/dotnet/dotnet build DocumentQA.sln
```

Build specific project:
```bash
/usr/local/share/dotnet/dotnet build DocumentQA.Functions/DocumentQA.Functions.csproj
```

### Running Functions Standalone (Without Aspire)

If you need to run Functions without Aspire:

```bash
cd DocumentQA.Functions
func start  # Requires Azure Functions Core Tools
```

Note: You'll need to manually configure environment variables (30+ values) in `local.settings.json`. Aspire is strongly recommended for development.

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
├── DocumentQA.AppHost/           # Aspire orchestration (dev only)
│   └── AppHost.cs               # Configuration injection
├── DocumentQA.ServiceDefaults/   # Shared telemetry config
└── DocumentQA.Functions/         # Azure Functions app (main logic)
    ├── Functions/               # HTTP and blob trigger endpoints
    ├── Services/                # Core business logic
    ├── Configuration/           # Config POCOs
    ├── Models/                  # Data models
    └── Utils/                   # Helper classes
```

### Core Components

**Functions (Entry Points)**:
- `UploadFunction.cs`: HTTP POST `/api/upload` - Handles PDF uploads, validates, stores in Blob, creates status record
- `ProcessingFunction.cs`: Blob trigger - Extracts text → chunks → embeddings → indexes in AI Search
- `QueryFunction.cs`: HTTP POST `/api/query` - Handles questions, retrieves chunks, generates answers

**Services (Business Logic)**:
- `DocumentIngestionService.cs`: Orchestrates extraction, chunking, embedding generation
- `SearchService.cs`: Manages Azure AI Search index creation and vector search
- `EmbeddingService.cs`: Generates embeddings via Azure OpenAI
- `AnswerGenerationService.cs`: Generates GPT-4 answers with citations
- `QueryService.cs`: Orchestrates retrieval and answer generation pipeline
- `DocumentStatusService.cs`: Tracks document processing state in Table Storage

**Configuration Flow**:
1. Aspire reads `DocumentQA.AppHost/appsettings.Development.json`
2. Aspire injects values as environment variables (e.g., `Azure__OpenAI__Endpoint`)
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
                    → AnswerGenerationService.GenerateAnswerAsync (GPT-4)
                    → Return answer with citations
```

### Configuration Details

All config is environment-variable based. Key settings:

**Azure Services** (required):
- `Azure__OpenAI__Endpoint`, `Azure__OpenAI__ApiKey`
- `Azure__OpenAI__EmbeddingDeploymentName` (default: "text-embedding-3-large")
- `Azure__OpenAI__ChatDeploymentName` (default: "gpt-4")
- `Azure__DocumentIntelligence__Endpoint`, `Azure__DocumentIntelligence__ApiKey`
- `Azure__AISearch__Endpoint`, `Azure__AISearch__AdminKey`
- `Azure__AISearch__IndexName` (default: "document-chunks")
- `Azure__Storage__ConnectionString` (auto-injected by Aspire for Azurite)

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
3. Add default value to `DocumentQA.AppHost/AppHost.cs` in `.WithEnvironment()` call
4. Document in `DocumentQA.AppHost/README.md`

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
- All functions log extensively for Aspire Dashboard tracing

## Important Constraints

1. **PDF Only**: System only accepts PDF files (validated in `UploadFunction.cs:79`)
2. **100MB Limit**: Max file size enforced (`UploadFunction.cs:70`)
3. **Vector Search**: Requires Azure AI Search Standard tier (S1) or higher
4. **Deployment Mismatch**: Azure OpenAI deployment names must match config (common error)
5. **Aspire is Dev-Only**: Do not use Aspire AppHost for production deployment

## Common Tasks

### Changing Chunk Size

Edit `DocumentQA.AppHost/appsettings.Development.json`:
```json
{
  "Processing": {
    "ChunkSize": 1000,
    "ChunkOverlap": 100
  }
}
```

### Debugging Processing Failures

1. Check Aspire Dashboard → Traces for full pipeline visibility
2. Check Table Storage for document status record (includes error details)
3. Review `ProcessingFunction.cs:currentStep` to identify failure point
4. Common issues: API key errors, rate limits, deployment name mismatches

### Testing Locally

Use Aspire Dashboard's "Endpoints" section to find function URLs, then:

```bash
# Upload document
curl -X POST http://localhost:7071/api/upload -F "file=@test.pdf"

# Query (replace DOCUMENT_ID)
curl -X POST http://localhost:7071/api/query \
  -H "Content-Type: application/json" \
  -d '{"question": "What is the main topic?", "documentIds": ["DOCUMENT_ID"]}'
```

### Viewing Storage Contents

Azurite storage persists in Docker volume. Use Azure Storage Explorer or:

```bash
# List blobs (requires azure-cli)
az storage blob list --container-name documents --connection-string "UseDevelopmentStorage=true"

# Query table
az storage entity query --table-name documentstatus --connection-string "UseDevelopmentStorage=true"
```

## File References

- **Main orchestration**: `DocumentQA.AppHost/AppHost.cs`
- **DI configuration**: `DocumentQA.Functions/Program.cs:10-106`
- **Processing pipeline**: `DocumentQA.Functions/Functions/ProcessingFunction.cs:28-111`
- **Query pipeline**: `DocumentQA.Functions/Services/QueryService.cs`
- **Chunking logic**: `DocumentQA.Functions/Utils/ChunkingStrategy.cs`
- **Config schema**: `DocumentQA.Functions/Configuration/AzureConfig.cs`

## Additional Documentation

- `DocumentQA.AppHost/README.md`: Detailed Aspire setup instructions
- `docs/ASPIRE_SETUP.md`: Comprehensive Aspire integration guide
- Function-level comments explain business logic throughout codebase
