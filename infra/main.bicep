targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment used for resource naming')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Unique suffix for globally unique resource names')
param resourceGroupName string = ''

// Azure OpenAI - Assuming existing resource
@description('Azure OpenAI endpoint URL (e.g., https://myopenai.openai.azure.com/)')
param openAiEndpoint string

@secure()
@description('Azure OpenAI API Key')
param openAiApiKey string

@description('Azure OpenAI embedding deployment name')
param openAiEmbeddingDeploymentName string = 'text-embedding-3-large'

@description('Azure OpenAI chat deployment name')
param openAiChatDeploymentName string = 'gpt-4'

// Processing Configuration
@description('Chunk size in characters for document processing')
param chunkSize int = 800

@description('Chunk overlap in characters')
param chunkOverlap int = 50

@description('Maximum chunks to retrieve during search')
param maxChunksToRetrieve int = 20

@description('Top chunks to use for answer generation')
param topChunksForAnswer int = 7

// Answer Generation Configuration
@description('Temperature for GPT-4 answer generation (0.0-1.0)')
param answerTemperature string = '0.3'

@description('Maximum tokens for answer generation')
param answerMaxTokens int = 1500

@description('Minimum confidence threshold for answers (0.0-1.0)')
param minimumConfidenceThreshold string = '0.5'

// Storage Configuration
@description('Name of the blob container for documents')
param documentsContainerName string = 'documents'

@description('Name of the table for document status tracking')
param documentStatusTableName string = 'documentstatus'

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
@description('Document Intelligence SKU')
@allowed([
  'F0'
  'S0'
])
param documentIntelligenceSku string = 'S0'

// Function App Configuration
@description('Function App SKU (Consumption Plan)')
param functionAppSku string = 'Y1'

@description('ID of the principal to assign admin roles')
param principalId string = ''

// Variables
var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = {
  'azd-env-name': environmentName
}

// Resource Group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// Application Insights
module appInsights './modules/appinsights.bicep' = {
  name: 'appinsights'
  scope: rg
  params: {
    name: '${abbrs.insightsComponents}${resourceToken}'
    location: location
    tags: tags
  }
}

// Storage Account
module storage './modules/storage.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    name: '${abbrs.storageStorageAccounts}${resourceToken}'
    location: location
    tags: tags
    documentsContainerName: documentsContainerName
    documentStatusTableName: documentStatusTableName
  }
}

// Document Intelligence
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

// AI Search
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

// Azure Functions App
module functionApp './modules/functionapp.bicep' = {
  name: 'functionapp'
  scope: rg
  params: {
    name: '${abbrs.webSitesFunctions}${resourceToken}'
    location: location
    tags: union(tags, { 'azd-service-name': 'functions' })
    applicationInsightsConnectionString: appInsights.outputs.connectionString
    storageAccountName: storage.outputs.name

    // Azure OpenAI Configuration
    openAiEndpoint: openAiEndpoint
    openAiApiKey: openAiApiKey
    openAiEmbeddingDeploymentName: openAiEmbeddingDeploymentName
    openAiChatDeploymentName: openAiChatDeploymentName

    // Document Intelligence Configuration
    documentIntelligenceEndpoint: documentIntelligence.outputs.endpoint
    documentIntelligenceApiKey: documentIntelligence.outputs.apiKey

    // AI Search Configuration
    searchEndpoint: search.outputs.endpoint
    searchAdminKey: search.outputs.adminKey
    searchIndexName: searchIndexName

    // Storage Configuration
    storageConnectionString: storage.outputs.connectionString
    documentsContainerName: documentsContainerName
    documentStatusTableName: documentStatusTableName

    // Processing Configuration
    chunkSize: chunkSize
    chunkOverlap: chunkOverlap
    maxChunksToRetrieve: maxChunksToRetrieve
    topChunksForAnswer: topChunksForAnswer

    // Answer Generation Configuration
    answerTemperature: answerTemperature
    answerMaxTokens: answerMaxTokens
    minimumConfidenceThreshold: minimumConfidenceThreshold
  }
}

// Outputs
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = rg.name

output APPLICATIONINSIGHTS_CONNECTION_STRING string = appInsights.outputs.connectionString
output AZURE_FUNCTION_APP_NAME string = functionApp.outputs.name
output AZURE_FUNCTION_URI string = functionApp.outputs.uri

output AZURE_STORAGE_ACCOUNT_NAME string = storage.outputs.name
output AZURE_STORAGE_CONNECTION_STRING string = storage.outputs.connectionString

output AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT string = documentIntelligence.outputs.endpoint
output AZURE_AI_SEARCH_ENDPOINT string = search.outputs.endpoint
output AZURE_AI_SEARCH_NAME string = search.outputs.name
