// ============================================================================
// Modulo Azure Service Bus
// Crea namespace, queue per ogni agente e topic per output
// ============================================================================

param serviceBusName string
param location string

resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

// Queue principale per i messaggi agli agenti
resource agentQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'bmad-agent-queue'
  properties: {
    lockDuration: 'PT5M'        // 5 minuti di lock per elaborazione
    maxDeliveryCount: 3          // Massimo 3 tentativi prima di dead-letter
    deadLetteringOnMessageExpiration: true
    defaultMessageTimeToLive: 'P1D'  // TTL 1 giorno
  }
}

// Topic per gli output degli agenti (fan-out verso più consumatori)
resource workflowTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBus
  name: 'bmad-workflow-events'
  properties: {
    defaultMessageTimeToLive: 'PT1H'
  }
}

// Authorization rule per l'app
resource authRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: serviceBus
  name: 'bmad-app-rule'
  properties: {
    rights: ['Send', 'Listen', 'Manage']
  }
}

output primaryConnectionString string = authRule.listKeys().primaryConnectionString
output namespaceName string = serviceBus.name
