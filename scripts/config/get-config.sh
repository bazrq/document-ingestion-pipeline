#!/bin/bash

# Retrieve configuration from deployed local-dev infrastructure
# Usage: ./get-config.sh [resource-group-name] [--update-functions] [--update-aspire]

set -e

# Parse arguments
RESOURCE_GROUP="rg-local-dev"
UPDATE_FUNCTIONS=false
UPDATE_ASPIRE=false

for arg in "$@"; do
  case $arg in
    --update-functions)
      UPDATE_FUNCTIONS=true
      ;;
    --update-aspire)
      UPDATE_ASPIRE=true
      ;;
    --help|-h)
      echo "Usage: $0 [resource-group-name] [--update-functions] [--update-aspire]"
      echo ""
      echo "Options:"
      echo "  resource-group-name    Azure resource group name (default: rg-local-dev)"
      echo "  --update-functions     Update DocumentQA.Functions/local.settings.json"
      echo "  --update-aspire        Update DocumentQA.AppHost/appsettings.Development.json"
      echo "  --help, -h             Show this help message"
      echo ""
      echo "Examples:"
      echo "  $0                                    # Show config from rg-local-dev"
      echo "  $0 rg-my-resources                    # Show config from rg-my-resources"
      echo "  $0 --update-functions                 # Show and update local.settings.json"
      echo "  $0 rg-local-dev --update-functions    # Show and update from specific RG"
      exit 0
      ;;
    *)
      # Treat as resource group name if it doesn't start with --
      if [[ ! $arg =~ ^-- ]]; then
        RESOURCE_GROUP="$arg"
      fi
      ;;
  esac
done

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

echo "Retrieving Storage Account configuration..."
STORAGE_NAME=$(az storage account list -g "$RESOURCE_GROUP" --query "[0].name" -o tsv)
if [ -z "$STORAGE_NAME" ]; then
  echo "Warning: Storage Account not found in $RESOURCE_GROUP"
  STORAGE_CONNECTION=""
  STORAGE_CONTAINER="documents"
  STORAGE_TABLE="documentstatus"
else
  STORAGE_CONNECTION=$(az storage account show-connection-string --name "$STORAGE_NAME" -g "$RESOURCE_GROUP" --query "connectionString" -o tsv)
  STORAGE_CONTAINER="documents"
  STORAGE_TABLE="documentstatus"
fi

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
echo "Storage Account:"
echo "  Account Name: $STORAGE_NAME"
echo "  Connection String: ${STORAGE_CONNECTION:0:50}..."
echo "  Container Name: $STORAGE_CONTAINER"
echo "  Table Name: $STORAGE_TABLE"
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
    },
    "Storage": {
      "ConnectionString": "$STORAGE_CONNECTION"
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
export AZURE_STORAGE_ACCOUNT_NAME="$STORAGE_NAME"
export AZURE_STORAGE_CONNECTION_STRING="$STORAGE_CONNECTION"
export AZURE_STORAGE_CONTAINER_NAME="$STORAGE_CONTAINER"
export AZURE_STORAGE_TABLE_NAME="$STORAGE_TABLE"
EOF
echo ""

# Update local.settings.json if requested
if [ "$UPDATE_FUNCTIONS" = true ]; then
  echo "=================================================="
  echo "UPDATING LOCAL.SETTINGS.JSON"
  echo "=================================================="
  echo ""

  # Get script directory and project root
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
  FUNCTIONS_DIR="$PROJECT_ROOT/DocumentQA.Functions"
  LOCAL_SETTINGS="$FUNCTIONS_DIR/local.settings.json"

  # Check if jq is available
  if ! command -v jq &> /dev/null; then
    echo "Warning: jq not found. Installing is recommended for JSON manipulation."
    echo "On macOS: brew install jq"
    echo ""
    echo "Creating local.settings.json without jq (basic merge)..."
  fi

  # Read existing OpenAI configuration if file exists
  EXISTING_OPENAI_ENDPOINT=""
  EXISTING_OPENAI_KEY=""
  EXISTING_OPENAI_EMBEDDING=""
  EXISTING_OPENAI_CHAT=""

  if [ -f "$LOCAL_SETTINGS" ] && command -v jq &> /dev/null; then
    EXISTING_OPENAI_ENDPOINT=$(jq -r '.Values["Azure__OpenAI__Endpoint"] // ""' "$LOCAL_SETTINGS")
    EXISTING_OPENAI_KEY=$(jq -r '.Values["Azure__OpenAI__ApiKey"] // ""' "$LOCAL_SETTINGS")
    EXISTING_OPENAI_EMBEDDING=$(jq -r '.Values["Azure__OpenAI__EmbeddingDeploymentName"] // ""' "$LOCAL_SETTINGS")
    EXISTING_OPENAI_CHAT=$(jq -r '.Values["Azure__OpenAI__ChatDeploymentName"] // ""' "$LOCAL_SETTINGS")
  fi

  # Use existing OpenAI values or defaults
  OPENAI_ENDPOINT="${EXISTING_OPENAI_ENDPOINT:-https://YOUR-OPENAI-RESOURCE.openai.azure.com/}"
  OPENAI_KEY="${EXISTING_OPENAI_KEY:-YOUR-OPENAI-KEY}"
  OPENAI_EMBEDDING="${EXISTING_OPENAI_EMBEDDING:-text-embedding-3-large}"
  OPENAI_CHAT="${EXISTING_OPENAI_CHAT:-gpt-5-mini}"

  # Create or update local.settings.json
  if command -v jq &> /dev/null; then
    # Use jq for robust JSON manipulation
    cat > "$LOCAL_SETTINGS" <<EOF
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Azure__OpenAI__Endpoint": "$OPENAI_ENDPOINT",
    "Azure__OpenAI__ApiKey": "$OPENAI_KEY",
    "Azure__OpenAI__EmbeddingDeploymentName": "$OPENAI_EMBEDDING",
    "Azure__OpenAI__ChatDeploymentName": "$OPENAI_CHAT",
    "Azure__DocumentIntelligence__Endpoint": "$DOC_INTEL_ENDPOINT",
    "Azure__DocumentIntelligence__ApiKey": "$DOC_INTEL_KEY",
    "Azure__AISearch__Endpoint": "$SEARCH_ENDPOINT",
    "Azure__AISearch__AdminKey": "$SEARCH_KEY",
    "Azure__AISearch__IndexName": "$SEARCH_INDEX",
    "Azure__Storage__ConnectionString": "$STORAGE_CONNECTION",
    "Azure__Storage__ContainerName": "$STORAGE_CONTAINER",
    "Azure__Storage__TableName": "$STORAGE_TABLE",
    "Processing__ChunkSize": "800",
    "Processing__ChunkOverlap": "50",
    "Processing__MaxChunksToRetrieve": "20",
    "Processing__TopChunksForAnswer": "7",
    "AnswerGeneration__MinimumConfidenceThreshold": "0.5",
    "AnswerGeneration__MaxTokens": "1500",
    "AnswerGeneration__Temperature": "0.3"
  }
}
EOF
  else
    # Fallback without jq
    cat > "$LOCAL_SETTINGS" <<EOF
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Azure__OpenAI__Endpoint": "$OPENAI_ENDPOINT",
    "Azure__OpenAI__ApiKey": "$OPENAI_KEY",
    "Azure__OpenAI__EmbeddingDeploymentName": "$OPENAI_EMBEDDING",
    "Azure__OpenAI__ChatDeploymentName": "$OPENAI_CHAT",
    "Azure__DocumentIntelligence__Endpoint": "$DOC_INTEL_ENDPOINT",
    "Azure__DocumentIntelligence__ApiKey": "$DOC_INTEL_KEY",
    "Azure__AISearch__Endpoint": "$SEARCH_ENDPOINT",
    "Azure__AISearch__AdminKey": "$SEARCH_KEY",
    "Azure__AISearch__IndexName": "$SEARCH_INDEX",
    "Azure__Storage__ConnectionString": "$STORAGE_CONNECTION",
    "Azure__Storage__ContainerName": "$STORAGE_CONTAINER",
    "Azure__Storage__TableName": "$STORAGE_TABLE",
    "Processing__ChunkSize": "800",
    "Processing__ChunkOverlap": "50",
    "Processing__MaxChunksToRetrieve": "20",
    "Processing__TopChunksForAnswer": "7",
    "AnswerGeneration__MinimumConfidenceThreshold": "0.5",
    "AnswerGeneration__MaxTokens": "1500",
    "AnswerGeneration__Temperature": "0.3"
  }
}
EOF
  fi

  echo "✅ Updated: $LOCAL_SETTINGS"
  echo ""
  if [ "$OPENAI_ENDPOINT" = "https://YOUR-OPENAI-RESOURCE.openai.azure.com/" ]; then
    echo "⚠️  NOTE: Azure OpenAI configuration still needs to be set manually."
    echo "   Update the following values in $LOCAL_SETTINGS:"
    echo "   - Azure__OpenAI__Endpoint"
    echo "   - Azure__OpenAI__ApiKey"
    echo "   - Azure__OpenAI__EmbeddingDeploymentName (if different)"
    echo "   - Azure__OpenAI__ChatDeploymentName (if different)"
    echo ""
  fi
fi

# Update appsettings.Development.json if requested
if [ "$UPDATE_ASPIRE" = true ]; then
  echo "=================================================="
  echo "UPDATING APPSETTINGS.DEVELOPMENT.JSON"
  echo "=================================================="
  echo ""

  # Get script directory and project root
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
  APPHOST_DIR="$PROJECT_ROOT/DocumentQA.AppHost"
  APPSETTINGS="$APPHOST_DIR/appsettings.Development.json"

  # Check if jq is available
  if ! command -v jq &> /dev/null; then
    echo "Warning: jq not found. Installing is recommended for JSON manipulation."
    echo "On macOS: brew install jq"
    echo ""
    echo "Creating appsettings.Development.json without jq..."
  fi

  # Read existing OpenAI configuration if file exists
  EXISTING_OPENAI_ENDPOINT=""
  EXISTING_OPENAI_KEY=""
  EXISTING_OPENAI_EMBEDDING=""
  EXISTING_OPENAI_CHAT=""

  if [ -f "$APPSETTINGS" ] && command -v jq &> /dev/null; then
    EXISTING_OPENAI_ENDPOINT=$(jq -r '.Azure.OpenAI.Endpoint // ""' "$APPSETTINGS")
    EXISTING_OPENAI_KEY=$(jq -r '.Azure.OpenAI.ApiKey // ""' "$APPSETTINGS")
    EXISTING_OPENAI_EMBEDDING=$(jq -r '.Azure.OpenAI.EmbeddingDeploymentName // ""' "$APPSETTINGS")
    EXISTING_OPENAI_CHAT=$(jq -r '.Azure.OpenAI.ChatDeploymentName // ""' "$APPSETTINGS")
  fi

  # Use existing OpenAI values or defaults
  OPENAI_ENDPOINT="${EXISTING_OPENAI_ENDPOINT:-https://YOUR-OPENAI-RESOURCE.openai.azure.com/}"
  OPENAI_KEY="${EXISTING_OPENAI_KEY:-YOUR-OPENAI-KEY}"
  OPENAI_EMBEDDING="${EXISTING_OPENAI_EMBEDDING:-text-embedding-3-large}"
  OPENAI_CHAT="${EXISTING_OPENAI_CHAT:-gpt-5-mini}"

  # Create or update appsettings.Development.json
  cat > "$APPSETTINGS" <<EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Azure": {
    "OpenAI": {
      "Endpoint": "$OPENAI_ENDPOINT",
      "ApiKey": "$OPENAI_KEY",
      "EmbeddingDeploymentName": "$OPENAI_EMBEDDING",
      "ChatDeploymentName": "$OPENAI_CHAT"
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

  echo "✅ Updated: $APPSETTINGS"
  echo ""
  if [ "$OPENAI_ENDPOINT" = "https://YOUR-OPENAI-RESOURCE.openai.azure.com/" ]; then
    echo "⚠️  NOTE: Azure OpenAI configuration still needs to be set manually."
    echo "   Update the following values in $APPSETTINGS:"
    echo "   - Azure.OpenAI.Endpoint"
    echo "   - Azure.OpenAI.ApiKey"
    echo "   - Azure.OpenAI.EmbeddingDeploymentName (if different)"
    echo "   - Azure.OpenAI.ChatDeploymentName (if different)"
    echo ""
  fi
fi
