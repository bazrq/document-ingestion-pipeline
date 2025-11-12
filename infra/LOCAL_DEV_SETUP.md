# Minimal Infrastructure for Local Aspire Development

This directory contains a **minimal Bicep template** designed specifically for local development with .NET Aspire. It deploys only the Azure services that cannot be emulated locally.

## What Gets Deployed

**Azure Services:**
- **Azure Document Intelligence** (F0 - Free tier) - For PDF text extraction
- **Azure AI Search** (Standard S1) - For vector search

**Local Services (via Aspire):**
- **Azurite** - Local Azure Storage emulator (Blob + Table)
- **Azure Functions** - Runs locally with injected configuration

**Not Deployed (You Must Have):**
- **Azure OpenAI** - You need an existing Azure OpenAI resource with deployed models

## Cost Breakdown

| Service | Tier | Monthly Cost |
|---------|------|--------------|
| Document Intelligence | F0 (Free) | **$0** (500 pages/month limit) |
| AI Search | Standard S1 | **~$250** |
| **TOTAL** | | **~$250/month** |

**Cost-Saving Tip:** Delete the resource group when not actively developing:
```bash
az group delete --name rg-local-dev --yes --no-wait
```

## Prerequisites

Before deploying:

1. **Azure CLI** installed and authenticated:
   ```bash
   az login
   az account set --subscription <your-subscription-id>
   ```

2. **Existing Azure OpenAI** resource with these deployments:
   - `text-embedding-3-large` (or your preferred embedding model)
   - `gpt-5-mini` (or your preferred chat model)

3. **.NET 10 SDK** installed (for running Aspire)

4. **Docker Desktop** running (for Azurite container)

## Deployment Steps

### Option 1: Automated Script (Recommended)

```bash
cd infra
./deploy-local-dev.sh
```

The script will:
1. Deploy Document Intelligence + AI Search to Azure
2. Output all configuration values
3. Provide ready-to-paste JSON for `appsettings.Development.json`

### Option 2: Manual Deployment

```bash
cd infra

# Deploy infrastructure
az deployment sub create \
  --name local-dev-deployment \
  --location eastus \
  --template-file ./main.local-dev.bicep \
  --parameters ./main.local-dev.parameters.json

# Get outputs
az deployment sub show \
  --name local-dev-deployment \
  --query properties.outputs
```

## Configuration

After deployment, update `DocumentQA.AppHost/appsettings.Development.json`:

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
      "Endpoint": "https://YOUR-EXISTING-OPENAI.openai.azure.com/",
      "ApiKey": "your-openai-api-key",
      "EmbeddingDeploymentName": "text-embedding-3-large",
      "ChatDeploymentName": "gpt-5-mini"
    },
    "DocumentIntelligence": {
      "Endpoint": "<FROM DEPLOYMENT OUTPUT>",
      "ApiKey": "<FROM DEPLOYMENT OUTPUT>"
    },
    "AISearch": {
      "Endpoint": "<FROM DEPLOYMENT OUTPUT>",
      "AdminKey": "<FROM DEPLOYMENT OUTPUT>",
      "IndexName": "document-chunks"
    }
  }
}
```

**Important:** The `appsettings.Development.json` file is gitignored to prevent accidental credential commits.

## Running the Application

Once configured:

```bash
# Ensure Docker Desktop is running
docker ps

# Start the Aspire stack
cd DocumentQA.AppHost
dotnet run
```

Aspire will:
1. Start Azurite container for local Blob + Table storage
2. Launch Azure Functions with all environment variables injected
3. Open Aspire Dashboard at `https://localhost:17XXX`

### Testing the Endpoints

```bash
# Upload a PDF
curl -X POST http://localhost:7071/api/upload -F "file=@test.pdf"

# Query (replace DOCUMENT_ID with the ID from upload response)
curl -X POST http://localhost:7071/api/query \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What is the main topic?",
    "documentIds": ["DOCUMENT_ID"]
  }'
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────┐
│           LOCAL DEVELOPMENT MACHINE             │
│                                                 │
│  ┌──────────────────────────────────────────┐  │
│  │        .NET Aspire (AppHost)             │  │
│  │                                          │  │
│  │  ┌────────────┐      ┌───────────────┐  │  │
│  │  │  Azurite   │      │ Azure Funcs   │  │  │
│  │  │  (Docker)  │◄─────┤   (Local)     │  │  │
│  │  └────────────┘      └───────┬───────┘  │  │
│  └──────────────────────────────┼──────────┘  │
│                                 │              │
└─────────────────────────────────┼──────────────┘
                                  │
                                  │ HTTPS
                                  │
                    ┌─────────────┴─────────────┐
                    │                           │
                    ▼                           ▼
         ┌──────────────────┐      ┌──────────────────┐
         │  AZURE SERVICES  │      │  AZURE SERVICES  │
         │  (Your Existing) │      │   (Deployed)     │
         │                  │      │                  │
         │  Azure OpenAI    │      │  Doc Intelligence│
         │  - Embeddings    │      │  AI Search (S1)  │
         │  - GPT-5-mini    │      │                  │
         └──────────────────┘      └──────────────────┘
```

## Customization

### Using Free Document Intelligence Tier (F0)

Default configuration uses F0 (free tier) with limitations:
- **500 pages/month** quota
- May have slower processing
- Good for initial development

To upgrade to paid tier (S0):

Edit `infra/main.local-dev.parameters.json`:
```json
{
  "documentIntelligenceSku": {
    "value": "S0"
  }
}
```

Then redeploy.

### Changing AI Search Tier

To use a higher-performance AI Search tier:

Edit `infra/main.local-dev.parameters.json`:
```json
{
  "searchSku": {
    "value": "standard2"  // or "standard3"
  }
}
```

**Note:** Basic tier does NOT support vector search. Must be Standard or higher.

## Troubleshooting

### "Docker not running" error
- Start Docker Desktop before running `dotnet run`
- Verify: `docker ps`

### "Document Intelligence quota exceeded"
- F0 tier has 500 pages/month limit
- Upgrade to S0 tier or wait for monthly quota reset
- Check usage in Azure Portal

### "AI Search index not found"
- Index is created on first document upload
- Check Aspire Dashboard logs for indexing errors

### "Azure OpenAI rate limits"
- Check your deployment quotas in Azure Portal
- Increase TPM (tokens per minute) limits
- Consider using different deployment names

### "Connection string not found"
- Ensure Aspire is running (it injects storage connection string)
- Check Aspire Dashboard → Environment Variables

## Viewing Local Storage

Aspire uses Azurite for local storage. To browse:

### Option 1: Azure Storage Explorer
1. Install [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/)
2. Connect to "Local Storage Emulator (Azurite)"
3. Browse `documents` container and `documentstatus` table

### Option 2: Azure CLI
```bash
# List blobs
az storage blob list \
  --container-name documents \
  --connection-string "UseDevelopmentStorage=true"

# Query table
az storage entity query \
  --table-name documentstatus \
  --connection-string "UseDevelopmentStorage=true"
```

## Cleanup

### Delete All Azure Resources
```bash
# Get resource group name from deployment
RESOURCE_GROUP=$(az deployment sub show \
  --name local-dev-deployment \
  --query properties.outputs.AZURE_RESOURCE_GROUP.value -o tsv)

# Delete everything
az group delete --name $RESOURCE_GROUP --yes
```

### Clear Local Storage
```bash
# Stop Aspire (Ctrl+C in terminal)

# Remove Azurite Docker container
docker ps -a | grep azurite
docker rm -f <container-id>

# Clear Azurite data volume (if needed)
docker volume ls | grep azurite
docker volume rm <volume-name>
```

## Differences from Full Deployment

| Component | Full Deployment (`main.bicep`) | Local Dev (`main.local-dev.bicep`) |
|-----------|--------------------------------|-------------------------------------|
| Storage Account | Azure Storage | Azurite (local emulator) |
| Azure Functions | Deployed to Azure | Runs locally |
| Application Insights | Azure resource | Local telemetry via Aspire |
| Document Intelligence | Azure | Azure (can't be emulated) |
| AI Search | Azure | Azure (can't be emulated) |
| Azure OpenAI | Existing (assumed) | Existing (assumed) |

## Next Steps

- Review `../DocumentQA.AppHost/README.md` for Aspire configuration details
- Review `../CLAUDE.md` for architecture and development patterns
- See `../docs/ASPIRE_SETUP.md` for comprehensive Aspire integration guide

## Support

For issues:
1. Check Aspire Dashboard logs
2. Verify all configuration values in `appsettings.Development.json`
3. Ensure Docker Desktop is running
4. Check Azure Portal for service health
