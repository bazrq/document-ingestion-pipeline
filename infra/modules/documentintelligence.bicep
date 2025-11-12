param name string
param location string = resourceGroup().location
param tags object = {}

@description('SKU for Document Intelligence')
@allowed([
  'F0'
  'S0'
])
param sku string = 'S0'

resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'FormRecognizer'
  sku: {
    name: sku
  }
  properties: {
    customSubDomainName: name
    networkAcls: {
      defaultAction: 'Allow'
    }
    publicNetworkAccess: 'Enabled'
  }
}

output name string = documentIntelligence.name
output id string = documentIntelligence.id
output endpoint string = documentIntelligence.properties.endpoint
output apiKey string = documentIntelligence.listKeys().key1
