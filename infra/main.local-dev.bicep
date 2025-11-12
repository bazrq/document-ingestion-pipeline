targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment used for resource naming')
param environmentName string = 'local-dev'

@minLength(1)
@description('Primary location for all resources')
param location string = 'eastus'

@description('Resource group name (leave empty for auto-generated)')
param resourceGroupName string = ''

// AI Search Configuration
@description('Name of the AI Search index')
param searchIndexName string = 'document-chunks'

@description('AI Search SKU (must be standard or higher for vector search)')
@allowed([
  'standard'
  'standard2'
  'standard3'
])
param searchSku string = 'standard'

// Document Intelligence Configuration
@description('Document Intelligence SKU (F0 = free tier with quota limits, S0 = paid tier)')
@allowed([
  'F0'
  'S0'
])
param documentIntelligenceSku string = 'F0'

// Variables
var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = {
  'environment': environmentName
  'purpose': 'local-aspire-development'
}

// Resource Group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// Document Intelligence - For PDF text extraction
module documentIntelligence './modules/documentintelligence.bicep' = {
  name: 'documentintelligence'
  scope: rg
  params: {
    name: '${abbrs.cognitiveServicesFormRecognizer}${resourceToken}'
    location: location
    tags: tags
    sku: documentIntelligenceSku
  }
}

// AI Search - For vector search indexing
module search './modules/search.bicep' = {
  name: 'search'
  scope: rg
  params: {
    name: '${abbrs.searchSearchServices}${resourceToken}'
    location: location
    tags: tags
    sku: searchSku
  }
}

// Outputs - Copy these values to DocumentQA.AppHost/appsettings.Development.json
output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name

output AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT string = documentIntelligence.outputs.endpoint
output AZURE_DOCUMENT_INTELLIGENCE_API_KEY string = documentIntelligence.outputs.apiKey

output AZURE_AI_SEARCH_ENDPOINT string = search.outputs.endpoint
output AZURE_AI_SEARCH_ADMIN_KEY string = search.outputs.adminKey
output AZURE_AI_SEARCH_NAME string = search.outputs.name
output AZURE_AI_SEARCH_INDEX_NAME string = searchIndexName

// Instructions for next steps
output INSTRUCTIONS string = '''
===========================================
LOCAL ASPIRE DEVELOPMENT SETUP - NEXT STEPS
===========================================

1. Copy the output values above to DocumentQA.AppHost/appsettings.Development.json:

   {
     "Azure": {
       "OpenAI": {
         "Endpoint": "https://YOUR-EXISTING-OPENAI.openai.azure.com/",
         "ApiKey": "YOUR-EXISTING-OPENAI-KEY",
         "EmbeddingDeploymentName": "text-embedding-3-large",
         "ChatDeploymentName": "gpt-4"
       },
       "DocumentIntelligence": {
         "Endpoint": "${documentIntelligence.outputs.endpoint}",
         "ApiKey": "${documentIntelligence.outputs.apiKey}"
       },
       "AISearch": {
         "Endpoint": "${search.outputs.endpoint}",
         "AdminKey": "${search.outputs.adminKey}",
         "IndexName": "${searchIndexName}"
       }
     }
   }

2. Storage is handled locally by Aspire (Azurite) - no Azure Storage needed!

3. Start the Aspire stack:
   cd DocumentQA.AppHost
   dotnet run

4. Aspire will automatically:
   - Start Azurite container for local Blob + Table storage
   - Inject all configuration into Azure Functions
   - Open the Aspire Dashboard

===========================================
COST ESTIMATE (Monthly)
===========================================
- Document Intelligence (F0): FREE (limited quota: 500 pages/month)
- AI Search (Standard S1): ~$250/month
- Total: ~$250/month

To reduce costs further, delete resources when not in use:
  az group delete --name ${rg.name} --yes --no-wait

To upgrade Document Intelligence to paid tier for higher quota:
  Change documentIntelligenceSku parameter to 'S0'
===========================================
'''
