#!/bin/bash

# Deploy minimal infrastructure for local Azure Functions Core Tools development
# This deploys only Document Intelligence + AI Search
# Azure Functions Core Tools handles storage locally with Azurite

set -e

echo "=================================================="
echo "LOCAL DEVELOPMENT INFRASTRUCTURE DEPLOYMENT"
echo "=================================================="
echo ""
echo "This will deploy:"
echo "  - Azure Document Intelligence (F0 - FREE tier)"
echo "  - Azure AI Search (Standard S1 - ~\$250/month)"
echo "  - Azure Storage Account (Standard_LRS - ~\$1-5/month)"
echo ""
echo "Total estimated cost: ~\$251-255/month"
echo ""
echo "Prerequisites:"
echo "  - Azure CLI installed and logged in (az login)"
echo "  - Existing Azure OpenAI resource with deployments"
echo ""
read -p "Press Enter to continue or Ctrl+C to cancel..."

# Set deployment variables
DEPLOYMENT_NAME="local-dev-$(date +%Y%m%d-%H%M%S)"
LOCATION="${AZURE_LOCATION:-eastus}"
ENV_NAME="${AZURE_ENV_NAME:-local-dev}"

echo ""
echo "Deployment settings:"
echo "  Environment: $ENV_NAME"
echo "  Location: $LOCATION"
echo "  Deployment name: $DEPLOYMENT_NAME"
echo ""

# Get current subscription
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
echo "Using subscription: $SUBSCRIPTION_ID"
echo ""

# Deploy infrastructure
echo "Deploying infrastructure..."
az deployment sub create \
  --name "$DEPLOYMENT_NAME" \
  --location "$LOCATION" \
  --template-file ./main.local-dev.bicep \
  --parameters ./main.local-dev.parameters.json \
  --parameters environmentName="$ENV_NAME" location="$LOCATION"

echo ""
echo "=================================================="
echo "DEPLOYMENT COMPLETE!"
echo "=================================================="
echo ""

# Get outputs
echo "Retrieving deployment outputs..."
RESOURCE_GROUP=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_RESOURCE_GROUP.value -o tsv)
DOC_INTEL_ENDPOINT=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT.value -o tsv)
DOC_INTEL_KEY=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_DOCUMENT_INTELLIGENCE_API_KEY.value -o tsv)
SEARCH_ENDPOINT=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_AI_SEARCH_ENDPOINT.value -o tsv)
SEARCH_KEY=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_AI_SEARCH_ADMIN_KEY.value -o tsv)
SEARCH_INDEX=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_AI_SEARCH_INDEX_NAME.value -o tsv)
STORAGE_ACCOUNT=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_STORAGE_ACCOUNT_NAME.value -o tsv)
STORAGE_CONNECTION=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_STORAGE_CONNECTION_STRING.value -o tsv)
STORAGE_CONTAINER=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_STORAGE_CONTAINER_NAME.value -o tsv)
STORAGE_TABLE=$(az deployment sub show --name "$DEPLOYMENT_NAME" --query properties.outputs.AZURE_STORAGE_TABLE_NAME.value -o tsv)

echo ""
echo "=================================================="
echo "CONFIGURATION VALUES"
echo "=================================================="
echo ""
echo "Resource Group: $RESOURCE_GROUP"
echo ""
echo "Document Intelligence:"
echo "  Endpoint: $DOC_INTEL_ENDPOINT"
echo "  API Key: ${DOC_INTEL_KEY:0:10}..."
echo ""
echo "AI Search:"
echo "  Endpoint: $SEARCH_ENDPOINT"
echo "  Admin Key: ${SEARCH_KEY:0:10}..."
echo "  Index Name: $SEARCH_INDEX"
echo ""
echo "Storage Account:"
echo "  Account Name: $STORAGE_ACCOUNT"
echo "  Connection String: ${STORAGE_CONNECTION:0:50}..."
echo "  Container Name: $STORAGE_CONTAINER"
echo "  Table Name: $STORAGE_TABLE"
echo ""
echo "=================================================="
echo "NEXT STEPS"
echo "=================================================="
echo ""
echo "1. Update DocumentQA.AppHost/appsettings.Development.json with these values:"
echo ""
cat <<EOF
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
      "ApiKey": "YOUR-EXISTING-OPENAI-KEY",
      "EmbeddingDeploymentName": "text-embedding-3-large",
      "ChatDeploymentName": "gpt-5-mini"
    },
    "DocumentIntelligence": {
      "Endpoint": "$DOC_INTEL_ENDPOINT",
      "ApiKey": "$DOC_INTEL_KEY"
    },
    "AISearch": {
      "Endpoint": "$SEARCH_ENDPOINT",
      "AdminKey": "$SEARCH_KEY",
      "IndexName": "$SEARCH_INDEX"
    },
    "Storage": {
      "ConnectionString": "$STORAGE_CONNECTION"
    }
  }
}
EOF
echo ""
echo "2. Start the Azure Functions Core Tools stack:"
echo "   cd DocumentQA.AppHost"
echo "   dotnet run"
echo ""
echo "3. Azure Functions Core Tools will automatically:"
echo "   - Inject all configuration into Azure Functions"
echo "   - Initialize the blob container and table storage"
echo "   - Open the Azure Functions Core Tools Dashboard"
echo ""
echo "=================================================="
echo "COST MANAGEMENT"
echo "=================================================="
echo ""
echo "To delete these resources when not in use:"
echo "  az group delete --name $RESOURCE_GROUP --yes --no-wait"
echo ""
echo "To upgrade Document Intelligence from F0 (free) to S0 (paid):"
echo "  Edit main.local-dev.parameters.json and change documentIntelligenceSku to 'S0'"
echo "  Then re-run this script"
echo ""
echo "=================================================="
