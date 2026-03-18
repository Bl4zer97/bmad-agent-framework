// ============================================================================
// Modulo Azure Storage Account
// Usato per: artefatti BMAD, Azure Functions state, Durable Functions
// ============================================================================

param storageAccountName string
param location string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false  // Sicurezza: nessun accesso pubblico ai blob
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

// Container per gli artefatti prodotti dagli agenti
resource artifactsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccount.name}/default/bmad-artifacts'
  properties: {
    publicAccess: 'None'
  }
}

// Container per gli export dei progetti
resource exportsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccount.name}/default/bmad-exports'
  properties: {
    publicAccess: 'None'
  }
}

output connectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
output storageAccountName string = storageAccount.name
