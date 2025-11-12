param name string
param location string = resourceGroup().location
param tags object = {}

@description('Application Insights connection string')
param applicationInsightsConnectionString string

@description('Storage account name for function app')
param storageAccountName string

// Azure OpenAI Configuration
param openAiEndpoint string
@secure()
param openAiApiKey string
param openAiEmbeddingDeploymentName string
param openAiChatDeploymentName string

// Document Intelligence Configuration
param documentIntelligenceEndpoint string
@secure()
param documentIntelligenceApiKey string

// AI Search Configuration
param searchEndpoint string
@secure()
param searchAdminKey string
param searchIndexName string

// Storage Configuration
@secure()
param storageConnectionString string
param documentsContainerName string
param documentStatusTableName string

// Processing Configuration
param chunkSize int
param chunkOverlap int
param maxChunksToRetrieve int
param topChunksForAnswer int

// Answer Generation Configuration
param answerTemperature string
param answerMaxTokens int
param minimumConfidenceThreshold string

// Get reference to existing storage account
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' existing = {
  name: storageAccountName
}

// App Service Plan (Consumption)
resource hostingPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${name}-plan'
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

// Function App
resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: name
  location: location
  tags: tags
  kind: 'functionapp'
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(name)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        // Azure OpenAI Configuration
        {
          name: 'Azure__OpenAI__Endpoint'
          value: openAiEndpoint
        }
        {
          name: 'Azure__OpenAI__ApiKey'
          value: openAiApiKey
        }
        {
          name: 'Azure__OpenAI__EmbeddingDeploymentName'
          value: openAiEmbeddingDeploymentName
        }
        {
          name: 'Azure__OpenAI__ChatDeploymentName'
          value: openAiChatDeploymentName
        }
        // Document Intelligence Configuration
        {
          name: 'Azure__DocumentIntelligence__Endpoint'
          value: documentIntelligenceEndpoint
        }
        {
          name: 'Azure__DocumentIntelligence__ApiKey'
          value: documentIntelligenceApiKey
        }
        // AI Search Configuration
        {
          name: 'Azure__AISearch__Endpoint'
          value: searchEndpoint
        }
        {
          name: 'Azure__AISearch__AdminKey'
          value: searchAdminKey
        }
        {
          name: 'Azure__AISearch__IndexName'
          value: searchIndexName
        }
        // Storage Configuration
        {
          name: 'Azure__Storage__ConnectionString'
          value: storageConnectionString
        }
        {
          name: 'Azure__Storage__ContainerName'
          value: documentsContainerName
        }
        {
          name: 'Azure__Storage__TableName'
          value: documentStatusTableName
        }
        // Processing Configuration
        {
          name: 'Processing__ChunkSize'
          value: string(chunkSize)
        }
        {
          name: 'Processing__ChunkOverlap'
          value: string(chunkOverlap)
        }
        {
          name: 'Processing__MaxChunksToRetrieve'
          value: string(maxChunksToRetrieve)
        }
        {
          name: 'Processing__TopChunksForAnswer'
          value: string(topChunksForAnswer)
        }
        // Answer Generation Configuration
        {
          name: 'AnswerGeneration__Temperature'
          value: answerTemperature
        }
        {
          name: 'AnswerGeneration__MaxTokens'
          value: string(answerMaxTokens)
        }
        {
          name: 'AnswerGeneration__MinimumConfidenceThreshold'
          value: minimumConfidenceThreshold
        }
      ]
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      cors: {
        allowedOrigins: [
          'https://portal.azure.com'
        ]
      }
    }
    httpsOnly: true
  }
}

output name string = functionApp.name
output id string = functionApp.id
output uri string = 'https://${functionApp.properties.defaultHostName}'
output principalId string = functionApp.identity.principalId
