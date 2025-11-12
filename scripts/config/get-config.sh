#!/bin/bash

# Retrieve configuration from deployed local-dev infrastructure
# Usage: ./get-config.sh [resource-group-name]

set -e

RESOURCE_GROUP="${1:-rg-local-dev}"

echo "=================================================="
echo "RETRIEVING CONFIGURATION FROM AZURE"
echo "=================================================="
echo ""
echo "Resource Group: $RESOURCE_GROUP"
echo ""

# Check if resource group exists
if ! az group show --name "$RESOURCE_GROUP" &>/dev/null; then
  echo "Error: Resource group '$RESOURCE_GROUP' not found!"
  echo "Available resource groups:"
  az group list --query "[].name" -o tsv
  exit 1
fi

echo "Retrieving Document Intelligence configuration..."
DOC_INTEL_NAME=$(az cognitiveservices account list -g "$RESOURCE_GROUP" --query "[?kind=='FormRecognizer'].name | [0]" -o tsv)
if [ -z "$DOC_INTEL_NAME" ]; then
  echo "Warning: Document Intelligence resource not found in $RESOURCE_GROUP"
  DOC_INTEL_ENDPOINT=""
  DOC_INTEL_KEY=""
else
  DOC_INTEL_ENDPOINT=$(az cognitiveservices account show --name "$DOC_INTEL_NAME" -g "$RESOURCE_GROUP" --query "properties.endpoint" -o tsv)
  DOC_INTEL_KEY=$(az cognitiveservices account keys list --name "$DOC_INTEL_NAME" -g "$RESOURCE_GROUP" --query "key1" -o tsv)
fi

echo "Retrieving AI Search configuration..."
SEARCH_NAME=$(az search service list -g "$RESOURCE_GROUP" --query "[0].name" -o tsv)
if [ -z "$SEARCH_NAME" ]; then
  echo "Warning: AI Search resource not found in $RESOURCE_GROUP"
  SEARCH_ENDPOINT=""
  SEARCH_KEY=""
else
  SEARCH_ENDPOINT=$(az search service show --name "$SEARCH_NAME" -g "$RESOURCE_GROUP" --query "{endpoint:properties.endpoint}" -o tsv)
  # Construct full endpoint URL
  SEARCH_ENDPOINT="https://${SEARCH_NAME}.search.windows.net/"
  SEARCH_KEY=$(az search admin-key show --service-name "$SEARCH_NAME" -g "$RESOURCE_GROUP" --query "primaryKey" -o tsv)
fi

SEARCH_INDEX="document-chunks"

echo ""
echo "=================================================="
echo "CONFIGURATION VALUES"
echo "=================================================="
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
echo "=================================================="
echo "APPSETTINGS.DEVELOPMENT.JSON FORMAT"
echo "=================================================="
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
      "Endpoint": "https://YOUR-OPENAI-RESOURCE.openai.azure.com/",
      "ApiKey": "YOUR-OPENAI-KEY",
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
    }
  }
}
EOF
echo ""
echo "=================================================="
echo "EXPORT AS ENVIRONMENT VARIABLES"
echo "=================================================="
echo ""
cat <<EOF
export AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT="$DOC_INTEL_ENDPOINT"
export AZURE_DOCUMENT_INTELLIGENCE_API_KEY="$DOC_INTEL_KEY"
export AZURE_AI_SEARCH_ENDPOINT="$SEARCH_ENDPOINT"
export AZURE_AI_SEARCH_ADMIN_KEY="$SEARCH_KEY"
export AZURE_AI_SEARCH_INDEX_NAME="$SEARCH_INDEX"
EOF
echo ""
