// ============================================================================
// Modulo Azure OpenAI Service
// Deploya Azure OpenAI con il deployment del modello GPT-4o
// ============================================================================

param openAIName string
param location string
param sku string = 'S0'

@description('Nome del deployment GPT-4o')
param gpt4oDeploymentName string = 'gpt-4o'

resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: location
  kind: 'OpenAI'
  sku: {
    name: sku
  }
  properties: {
    publicNetworkAccess: 'Enabled'
    customSubDomainName: openAIName
  }
}

// Deployment del modello GPT-4o
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: gpt4oDeploymentName
  sku: {
    name: 'Standard'
    capacity: 10  // 10K tokens per minuto
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
  }
}

output endpoint string = openAI.properties.endpoint
output id string = openAI.id
