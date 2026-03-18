// ============================================================================
// BMAD Agent Framework - Infrastructure as Code
// main.bicep - Infrastruttura Azure principale
//
// Risorse deployate:
// - Azure OpenAI Service (GPT-4o)
// - Azure App Service Plan + Web App (o Container App)
// - Azure Service Bus (comunicazione tra agenti)
// - Azure Storage Account (artefatti)
// - Azure Cosmos DB (memoria degli agenti)
// - Azure Key Vault (segreti)
// - Application Insights (monitoring)
// - Log Analytics Workspace
// ============================================================================

targetScope = 'resourceGroup'

@description('Ambiente di deployment: dev, staging, prod')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Prefisso per i nomi delle risorse')
param projectName string = 'bmad'

@description('Regione Azure per il deployment')
param location string = resourceGroup().location

@description('Tier di Azure OpenAI da usare')
param openAISku string = 'S0'

// ============================================================================
// VARIABILI E NAMING CONVENTION
// ============================================================================

var suffix = '${projectName}-${environment}'
var openAIName = 'oai-${suffix}'
var serviceBusName = 'sb-${suffix}'
var storageAccountName = toLower('st${replace(suffix, '-', '')}')
var cosmosDbName = 'cosmos-${suffix}'
var keyVaultName = 'kv-${suffix}'
var appInsightsName = 'appi-${suffix}'
var logAnalyticsName = 'log-${suffix}'
var functionAppName = 'func-${suffix}'
var appServicePlanName = 'asp-${suffix}'

// ============================================================================
// LOG ANALYTICS + APPLICATION INSIGHTS
// ============================================================================

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ============================================================================
// AZURE OPENAI SERVICE
// ============================================================================

module openAI 'modules/openai.bicep' = {
  name: 'openAI-deployment'
  params: {
    openAIName: openAIName
    location: location
    sku: openAISku
  }
}

// ============================================================================
// AZURE SERVICE BUS (comunicazione asincrona tra agenti)
// ============================================================================

module serviceBus 'modules/servicebus.bicep' = {
  name: 'serviceBus-deployment'
  params: {
    serviceBusName: serviceBusName
    location: location
  }
}

// ============================================================================
// AZURE STORAGE (artefatti prodotti dagli agenti)
// ============================================================================

module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    storageAccountName: storageAccountName
    location: location
  }
}

// ============================================================================
// AZURE COSMOS DB (memoria persistente degli agenti)
// ============================================================================

resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: cosmosDbName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    databaseAccountOfferType: 'Standard'
    enableFreeTier: environment == 'dev'
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  parent: cosmosDb
  name: 'bmad-memory'
  properties: {
    resource: {
      id: 'bmad-memory'
    }
  }
}

resource cosmosContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-04-15' = {
  parent: cosmosDatabase
  name: 'agent-contexts'
  properties: {
    resource: {
      id: 'agent-contexts'
      partitionKey: {
        paths: ['/projectId']
        kind: 'Hash'
      }
      defaultTtl: 604800 // 7 giorni in secondi
    }
  }
}

// ============================================================================
// AZURE KEY VAULT (segreti: API keys, connection strings)
// ============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true  // Usa RBAC invece di Access Policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// Salva la connection string di Service Bus in Key Vault
resource sbConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'ServiceBusConnectionString'
  properties: {
    value: serviceBus.outputs.primaryConnectionString
  }
}

// ============================================================================
// AZURE APP SERVICE (hosting dell'applicazione)
// ============================================================================

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: environment == 'prod' ? 'P1v3' : 'B1'
    tier: environment == 'prod' ? 'PremiumV3' : 'Basic'
  }
}

resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'  // Managed Identity per zero-trust
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storage.outputs.connectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'BmadFramework__AzureOpenAIEndpoint'
          value: openAI.outputs.endpoint
        }
        {
          name: 'BmadFramework__AzureOpenAIApiKey'
          // In produzione: recupera da Key Vault tramite reference
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=AzureOpenAIApiKey)'
        }
        {
          name: 'BmadFramework__ServiceBusConnectionString'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=ServiceBusConnectionString)'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'DOTNET_VERSION'
          value: '8.0'
        }
      ]
    }
  }
}

// ============================================================================
// OUTPUT
// ============================================================================

output openAIEndpoint string = openAI.outputs.endpoint
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
output keyVaultUri string = keyVault.properties.vaultUri
