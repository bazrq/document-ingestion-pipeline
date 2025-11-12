# Infrastructure as Code (IaC) for Document QA System

This directory contains Bicep templates for deploying the Document QA System to Azure using the Azure Developer CLI (azd).

## Architecture Overview

The infrastructure provisions the following Azure resources:

1. **Azure Functions** (Consumption Plan) - Hosts the document processing and query endpoints
2. **Azure Storage Account** - Stores PDF documents (Blob) and processing status (Table)
3. **Azure Document Intelligence** - Extracts text from PDF documents
4. **Azure AI Search** (Standard S1) - Vector search for document chunks
5. **Application Insights** - Monitoring, logging, and telemetry
6. **Log Analytics Workspace** - Backend for Application Insights

**Note:** Azure OpenAI is NOT provisioned by these templates. You must have an existing Azure OpenAI resource with deployments for:
- `text-embedding-3-large` (or your specified embedding model)
- `gpt-5-mini` (or your specified chat model)

## Prerequisites

1. **Azure Subscription** with sufficient permissions to create resources
2. **Azure Developer CLI** (`azd`) installed - [Installation Guide](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
3. **.NET 10 SDK** installed
4. **Existing Azure OpenAI Resource** with deployed models
5. **Docker Desktop** (optional, for local Aspire development)

## Environment Setup

### First-Time Setup

1. **Initialize azd environment:**
   ```bash
   azd init
   ```
   When prompted, accept the detected configuration from `azure.yaml`.

2. **Create an environment:**
   ```bash
   # For development
   azd env new dev

   # For production (optional)
   azd env new prod
   ```

3. **Set required environment variables:**
   ```bash
   # Azure OpenAI Configuration (REQUIRED)
   azd env set AZURE_OPENAI_ENDPOINT "https://YOUR-OPENAI-RESOURCE.openai.azure.com/"
   azd env set AZURE_OPENAI_API_KEY "your-api-key-here"

   # Optional: Override default deployment names if different
   azd env set AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME "text-embedding-3-large"
   azd env set AZURE_OPENAI_CHAT_DEPLOYMENT_NAME "gpt-5-mini"

   # Optional: Set Azure region (defaults to eastus)
   azd env set AZURE_LOCATION "eastus"
   ```

4. **Deploy to Azure:**
   ```bash
   azd up
   ```

   This single command will:
   - Provision all Azure resources via Bicep templates
   - Build the DocumentQA.Functions project
   - Deploy the Functions app
   - Configure all application settings automatically

## Environment Variables Reference

### Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint URL | `https://myopenai.openai.azure.com/` |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI API key | `abc123...` |

### Optional Variables (with defaults)

| Variable | Default | Description |
|----------|---------|-------------|
| `AZURE_LOCATION` | `eastus` | Azure region for resources |
| `AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME` | `text-embedding-3-large` | Embedding model deployment name |
| `AZURE_OPENAI_CHAT_DEPLOYMENT_NAME` | `gpt-5-mini` | Chat model deployment name |
| `PROCESSING_CHUNK_SIZE` | `800` | Chunk size in characters |
| `PROCESSING_CHUNK_OVERLAP` | `50` | Chunk overlap in characters |
| `PROCESSING_MAX_CHUNKS_TO_RETRIEVE` | `20` | Max chunks from search |
| `PROCESSING_TOP_CHUNKS_FOR_ANSWER` | `7` | Top chunks for GPT-5-mini |
| `ANSWER_GENERATION_TEMPERATURE` | `0.3` | GPT-5-mini temperature (0.0-1.0) |
| `ANSWER_GENERATION_MAX_TOKENS` | `1500` | Max tokens in answer |
| `ANSWER_GENERATION_MIN_CONFIDENCE` | `0.5` | Min confidence threshold |
| `STORAGE_CONTAINER_NAME` | `documents` | Blob container name |
| `STORAGE_TABLE_NAME` | `documentstatus` | Table storage name |
| `AI_SEARCH_INDEX_NAME` | `document-chunks` | AI Search index name |
| `AI_SEARCH_SKU` | `standard` | AI Search SKU (standard/standard2/standard3) |
| `DOCUMENT_INTELLIGENCE_SKU` | `F0` (dev), `S0` (prod) | Document Intelligence SKU |

## Multi-Environment Support

This infrastructure supports separate dev and prod environments with different parameter files:

### Development Environment
- Uses `infra/main.parameters.json`
- Document Intelligence: F0 (free tier)
- AI Search: Standard (S1)
- Cost-optimized settings

```bash
azd env new dev
azd env set AZURE_OPENAI_ENDPOINT "..."
azd env set AZURE_OPENAI_API_KEY "..."
azd up
```

### Production Environment
- Uses `infra/main.parameters.prod.json`
- Document Intelligence: S0 (paid tier)
- AI Search: Standard (S1)
- Production-ready settings

```bash
azd env new prod
azd env set AZURE_OPENAI_ENDPOINT "..."
azd env set AZURE_OPENAI_API_KEY "..."
azd up
```

### Switching Between Environments
```bash
# List environments
azd env list

# Select environment
azd env select dev
# or
azd env select prod
```

## Deployment Commands

### Full Deployment (Infrastructure + Code)
```bash
azd up
```

### Infrastructure Only
```bash
azd provision
```

### Code Deployment Only
```bash
azd deploy
```

### View Deployed Resources
```bash
azd show
```

### Clean Up Resources
```bash
azd down
```

## Resource Naming Convention

Resources are named using this pattern: `{abbreviation}{uniqueSuffix}`

| Resource Type | Abbreviation | Example |
|---------------|--------------|---------|
| Resource Group | `rg-` | `rg-dev` |
| Storage Account | `st` | `stabc123def` |
| Function App | `func-` | `func-abc123def` |
| AI Search | `srch-` | `srch-abc123def` |
| Document Intelligence | `di-` | `di-abc123def` |
| Application Insights | `appi-` | `appi-abc123def` |

The `uniqueSuffix` is generated from subscription ID + environment + location to ensure global uniqueness.

## Bicep Module Structure

```
infra/
├── main.bicep                           # Main orchestration template
├── main.parameters.json                 # Development parameters
├── main.parameters.prod.json            # Production parameters
├── abbreviations.json                   # Resource naming abbreviations
└── modules/
    ├── appinsights.bicep               # Application Insights + Log Analytics
    ├── documentintelligence.bicep      # Document Intelligence service
    ├── functionapp.bicep               # Function App + Hosting Plan
    ├── search.bicep                    # Azure AI Search
    └── storage.bicep                   # Storage Account + Blob + Table
```

## Configuration Flow

```
azd environment variables (.env)
            ↓
main.parameters.json (parameter substitution)
            ↓
main.bicep (orchestration)
            ↓
module outputs (endpoints, keys)
            ↓
functionapp.bicep (app settings)
            ↓
Azure Function App environment variables
            ↓
Program.cs reads configuration
```

## Outputs After Deployment

After successful deployment, `azd up` outputs:

- `AZURE_FUNCTION_APP_NAME` - Function App name
- `AZURE_FUNCTION_URI` - Function App HTTPS endpoint
- `AZURE_STORAGE_ACCOUNT_NAME` - Storage account name
- `AZURE_AI_SEARCH_ENDPOINT` - AI Search endpoint
- `AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT` - Document Intelligence endpoint
- `APPLICATIONINSIGHTS_CONNECTION_STRING` - App Insights connection string

View outputs anytime:
```bash
azd env get-values
```

## Testing the Deployment

After deployment completes:

1. **Get the Function App URL:**
   ```bash
   FUNC_URL=$(azd env get-value AZURE_FUNCTION_URI)
   echo $FUNC_URL
   ```

2. **Upload a test PDF:**
   ```bash
   curl -X POST "$FUNC_URL/api/upload" \
     -F "file=@test.pdf"
   ```

   Response will include a `documentId`.

3. **Wait for processing to complete** (check Azure Portal or Application Insights logs)

4. **Query the document:**
   ```bash
   curl -X POST "$FUNC_URL/api/query" \
     -H "Content-Type: application/json" \
     -d '{
       "question": "What is the main topic of the document?",
       "documentIds": ["YOUR-DOCUMENT-ID"]
     }'
   ```

## Monitoring and Troubleshooting

### View Logs
```bash
# Stream Function App logs
azd monitor --logs
```

### Application Insights
- Navigate to Azure Portal → Application Insights resource
- Use "Live Metrics" for real-time monitoring
- Use "Logs" (Kusto) for detailed query analysis

### Common Issues

1. **Document Intelligence quota exceeded:**
   - Free tier (F0) has limited quota
   - Upgrade to S0 in production or wait for quota reset

2. **Azure OpenAI rate limits:**
   - Check your OpenAI deployment quotas
   - Consider increasing TPM (tokens per minute) limits

3. **AI Search vector search not working:**
   - Ensure SKU is Standard S1 or higher (Basic tier doesn't support vector search)
   - Check `AI_SEARCH_SKU` parameter

4. **Function App cold starts:**
   - Consumption Plan has cold start delays
   - Consider Premium Plan for production (requires updating `functionapp.bicep`)

## Cost Estimates (Monthly)

### Development Environment (F0 tiers)
- Storage Account: ~$5
- Function App (Consumption): ~$5-20 (usage-based)
- Document Intelligence (F0): Free (limited quota)
- AI Search (Standard S1): ~$250
- Application Insights: ~$5-10
- **Total: ~$265-290/month**

### Production Environment (S tiers)
- Storage Account: ~$10-20
- Function App (Consumption): ~$20-100 (usage-based)
- Document Intelligence (S0): ~$10 + usage
- AI Search (Standard S1): ~$250
- Application Insights: ~$10-50
- **Total: ~$300-430/month**

**Note:** AI Search is the most expensive component. Consider usage patterns when selecting SKU.

## Security Considerations

### Current Implementation
- API keys stored in Function App configuration (encrypted at rest)
- HTTPS enforced on all endpoints
- Storage account with no public blob access
- Minimum TLS 1.2 required

### Production Enhancements (Future)
- Migrate to Managed Identity for Azure service authentication
- Integrate Azure Key Vault for secret management
- Enable VNet integration for private endpoints
- Configure Azure Front Door or API Management
- Enable Azure AD authentication for function endpoints

## Updating Infrastructure

To update infrastructure after code changes:

1. **Modify Bicep templates** in `infra/` directory
2. **Update parameters** if needed in `main.parameters*.json`
3. **Re-provision:**
   ```bash
   azd provision
   ```

To update only the function code:
```bash
azd deploy
```

## CI/CD Integration

For automated deployments, configure GitHub Actions or Azure DevOps:

```bash
# Configure CI/CD with GitHub
azd pipeline config
```

This creates `.github/workflows/azure-dev.yml` for automated `azd up` on push.

## Additional Resources

- [Azure Developer CLI Documentation](https://learn.microsoft.com/azure/developer/azure-developer-cli/)
- [Bicep Documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [Azure Functions .NET Isolated Worker](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
- [Azure AI Search Vector Search](https://learn.microsoft.com/azure/search/vector-search-overview)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
