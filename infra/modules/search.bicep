param name string
param location string = resourceGroup().location
param tags object = {}

@description('SKU for AI Search - must be standard or higher for vector search')
@allowed([
  'standard'
  'standard2'
  'standard3'
])
param sku string = 'standard'

resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    networkRuleSet: {
      ipRules: []
    }
    encryptionWithCmk: {
      enforcement: 'Unspecified'
    }
    disableLocalAuth: false
    authOptions: {
      apiKeyOnly: {}
    }
  }
}

output name string = search.name
output id string = search.id
output endpoint string = 'https://${search.name}.search.windows.net/'
output adminKey string = search.listAdminKeys().primaryKey
