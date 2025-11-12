# Deployment Quick Start

This guide will help you deploy the Document QA System to Azure in minutes using Azure Developer CLI.

## Prerequisites

1. **Azure Developer CLI** installed - [Install azd](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
2. **Azure subscription** with contributor access
3. **Existing Azure OpenAI resource** with these deployments:
   - `text-embedding-3-large` (or your embedding model)
   - `gpt-5-mini` (or your chat model)

## Step-by-Step Deployment

### 1. Initialize azd (First Time Only)

```bash
azd init
```

When prompted, accept the detected configuration from `azure.yaml`.

### 2. Create Environment

```bash
# For development
azd env new dev

# Or for production
azd env new prod
```

### 3. Configure Azure OpenAI

Set your Azure OpenAI credentials (these are **required**):

```bash
azd env set AZURE_OPENAI_ENDPOINT "https://YOUR-OPENAI-RESOURCE.openai.azure.com/"
azd env set AZURE_OPENAI_API_KEY "your-api-key-here"
```

**Optional**: If your deployment names differ from defaults:
```bash
azd env set AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME "your-embedding-deployment"
azd env set AZURE_OPENAI_CHAT_DEPLOYMENT_NAME "your-chat-deployment"
```

**Optional**: Set Azure region (defaults to eastus):
```bash
azd env set AZURE_LOCATION "westus2"
```

### 4. Deploy to Azure

```bash
azd up
```

This single command will:
- ✅ Provision all Azure resources (Storage, Document Intelligence, AI Search, Functions, App Insights)
- ✅ Build the DocumentQA.Functions project
- ✅ Deploy the Functions app
- ✅ Configure all application settings automatically

**Deployment takes approximately 5-10 minutes.**

### 5. Get Your Function URL

After deployment completes:

```bash
azd env get-values
```

Look for `AZURE_FUNCTION_URI` in the output.

## Testing Your Deployment

### Upload a PDF

```bash
FUNC_URL=$(azd env get-value AZURE_FUNCTION_URI)

curl -X POST "$FUNC_URL/api/upload" \
  -F "file=@test.pdf"
```

Save the `documentId` from the response.

### Query the Document

Wait 30-60 seconds for processing to complete, then:

```bash
curl -X POST "$FUNC_URL/api/query" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What is the main topic?",
    "documentIds": ["YOUR-DOCUMENT-ID"]
  }'
```

## Common Commands

### View All Deployed Resources
```bash
azd show
```

### View Environment Variables
```bash
azd env get-values
```

### Re-deploy Code Only (No Infrastructure Changes)
```bash
azd deploy
```

### Re-provision Infrastructure Only
```bash
azd provision
```

### Delete All Resources
```bash
azd down
```

### Switch Between Environments
```bash
azd env list
azd env select dev
```

## Multi-Environment Setup

Deploy to multiple environments:

```bash
# Create dev environment
azd env new dev
azd env set AZURE_OPENAI_ENDPOINT "https://dev-openai.openai.azure.com/"
azd env set AZURE_OPENAI_API_KEY "dev-key"
azd up

# Create prod environment
azd env new prod
azd env set AZURE_OPENAI_ENDPOINT "https://prod-openai.openai.azure.com/"
azd env set AZURE_OPENAI_API_KEY "prod-key"
azd up
```

## Cost Estimates

### Development (F0/Free tiers where possible)
- **~$265-290/month**
- Includes: Storage, Functions (usage-based), Document Intelligence (F0), AI Search (S1), App Insights

### Production (Standard tiers)
- **~$300-430/month**
- Includes: Storage, Functions (usage-based), Document Intelligence (S0), AI Search (S1), App Insights

**Note**: AI Search Standard S1 (~$250/month) is the largest cost component. It's required for vector search support.

## Troubleshooting

### Common Issues

**1. Azure OpenAI deployment not found**
```
Error: Deployment 'gpt-5-mini' not found
```
**Solution**: Verify your deployment names match:
```bash
azd env set AZURE_OPENAI_CHAT_DEPLOYMENT_NAME "your-actual-deployment-name"
azd deploy  # Re-deploy with updated settings
```

**2. Document Intelligence quota exceeded**
```
Error: Quota exceeded for Document Intelligence
```
**Solution**: Free tier (F0) has limits. For production:
```bash
azd env set DOCUMENT_INTELLIGENCE_SKU "S0"
azd provision  # Re-provision with paid tier
```

**3. Function app not responding**
- Check Azure Portal → Function App → Logs
- View Application Insights for errors
- Run: `azd monitor --logs`

### View Logs
```bash
# Real-time log streaming
azd monitor --logs

# Or use Azure Portal
# Navigate to: Function App → Log stream
```

### Check Deployment Status
```bash
# List all resources
az resource list --resource-group $(azd env get-value AZURE_RESOURCE_GROUP) -o table
```

## Next Steps

- **Monitor**: Open Application Insights in Azure Portal for telemetry
- **Scale**: Adjust Function App plan in `infra/modules/functionapp.bicep`
- **Secure**: Add Key Vault for secrets (see `infra/README.md`)
- **CI/CD**: Configure GitHub Actions with `azd pipeline config`

## Additional Documentation

- **Detailed Infrastructure Guide**: [infra/README.md](./infra/README.md)
- **Development Guide**: [README.md](./README.md)
- **Claude Code Instructions**: [CLAUDE.md](./CLAUDE.md)
- **Aspire Setup**: [DocumentQA.AppHost/README.md](./DocumentQA.AppHost/README.md)

## Support

For issues:
1. Check [infra/README.md](./infra/README.md) troubleshooting section
2. Review Azure Portal logs (Function App → Logs)
3. Open an issue in the repository

## Clean Up

To delete all Azure resources and stop incurring costs:

```bash
azd down
```

This will delete the resource group and all contained resources. Your local code remains unchanged.
