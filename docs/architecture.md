# Architettura del BMAD Agent Framework

## Overview

Il BMAD Agent Framework è costruito su **Clean Architecture** con separazione netta tra:
- **Core**: astrazioni, modelli e servizi indipendenti dall'infrastruttura
- **Agents**: implementazioni degli agenti AI specializzati
- **Azure**: componenti specifici per Azure (Functions, infrastruttura)

## Diagramma dei Componenti

```
┌─────────────────────────────────────────────────────────────────┐
│                        PRESENTATION                              │
│  CLI (Program.cs)  │  HTTP API  │  Azure Functions (HTTP Trigger)│
└─────────────────────────────┬───────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────┐
│                       ORCHESTRATION                              │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                 OrchestratorAgent                          │ │
│  │  Registra agenti → Crea contesto → Delega a WorkflowEngine │ │
│  └────────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                  WorkflowEngine                            │ │
│  │  Esegue fasi → Gestisce errori → Aggiorna stato            │ │
│  └────────────────────────────────────────────────────────────┘ │
└──────────────────────────────┬──────────────────────────────────┘
                                │  AgentContext (shared state)
               ┌────────────────┴────────────────┐
               │                                  │
┌──────────────▼──────────────────────────────────▼──────────────┐
│                          AGENTS                                  │
│                                                                  │
│  AnalystAgent  ArchitectAgent  DeveloperAgent  QAAgent  DevOps  │
│       │              │               │            │        │     │
│  Requirements  Architecture       Code          Tests  Pipeline  │
└───────────────────────────┬─────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────┐
│                         CORE SERVICES                            │
│                                                                  │
│  AzureOpenAIService   MemoryService    ArtifactStore             │
│  (GPT-4o + Polly)     (In-Memory/      (In-Memory/              │
│                        CosmosDB)        Blob Storage)            │
└────────────────────────────┬────────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────┐
│                        AZURE CLOUD                               │
│                                                                  │
│  Azure OpenAI   CosmosDB   Blob Storage   Service Bus   Key Vault│
└─────────────────────────────────────────────────────────────────┘
```

## Flusso dei Dati

```
Input
  │
  ▼
AgentContext creato
  │
  ├──▶ AnalystAgent.ProcessAsync(context)
  │         │ Chiama Azure OpenAI con system prompt Analyst
  │         │ Salva requirements.md in ArtifactStore
  │         │ Salva messaggio in MemoryService
  │         ▼
  │    context.Artifacts["requirements"] = { ... }
  │
  ├──▶ ArchitectAgent.ProcessAsync(context)
  │         │ Legge context.Artifacts["requirements"]
  │         │ Chiama Azure OpenAI con system prompt Architect
  │         │ Salva architecture.md in ArtifactStore
  │         ▼
  │    context.Artifacts["architecture"] = { ... }
  │
  ├──▶ DeveloperAgent.ProcessAsync(context)
  │         │ Legge requirements + architecture
  │         │ Chiama Azure OpenAI con system prompt Developer
  │         │ Salva code.cs in ArtifactStore
  │         ▼
  │    context.Artifacts["code"] = { ... }
  │
  ... (QA, DevOps)
  │
  ▼
WorkflowState.CurrentPhase = Completed
Output con tutti gli Artifacts
```

## Pattern di Resilienza

Il framework usa **Polly** per gestire errori transitori nelle chiamate ad Azure OpenAI:

```
Chiamata API OpenAI
       │
       ▼
  Successo? ──YES──▶ Restituisce risultato
       │
      NO
       │
  Errore 429 (rate limit) o 5xx?
       │
      YES
       │
  Attende 2^n secondi (backoff esponenziale)
       │
  Ritenta (max 3 volte)
       │
  Ancora fallito? ──▶ Propaga eccezione
```

## Deployment su Azure

### Modalità Sviluppo (InMemory)
```
PC Developer
└── dotnet run
    └── OrchestratorAgent (in-process)
        └── AzureOpenAIService → Azure OpenAI
```

### Modalità Produzione (Azure)
```
GitHub Actions CI/CD
└── Deploy su Azure Functions
    └── OrchestratorFunction (Durable)
        ├── AnalystActivity Function
        ├── ArchitectActivity Function
        ├── DeveloperActivity Function
        ├── QAActivity Function
        └── DevOpsActivity Function
                │
                └── Azure OpenAI, CosmosDB, Blob Storage, Service Bus
```
