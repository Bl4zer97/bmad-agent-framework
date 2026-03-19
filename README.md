# 🤖 BMAD Agent Framework

[![CI/CD](https://github.com/Bl4zer97/bmad-agent-framework/actions/workflows/ci.yml/badge.svg)](https://github.com/Bl4zer97/bmad-agent-framework/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Azure](https://img.shields.io/badge/Azure-OpenAI-0078D4?logo=microsoftazure)](https://azure.microsoft.com/en-us/products/ai-services/openai-service)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**BMAD** (Breakthrough Method of Agile AI-Driven Development) è un framework di sviluppo software basato su **agenti AI specializzati** che collaborano per trasformare un'idea in codice funzionante su Azure.

Ogni agente ha un ruolo preciso e passa il proprio output all'agente successivo, creando una **pipeline di produzione automatizzata** che va dai requisiti al deployment in produzione.

---

## 🏗️ Architettura del Framework

```
  Input Utente: "Crea una REST API in .NET 8 per gestire task"
         │
         ▼
┌────────────────────────────────────────────────────────────┐
│                    ORCHESTRATOR AGENT                       │
│              (Azure Durable Functions)                      │
│                                                            │
│   Coordina il flusso: gestisce stato, retry ed errori      │
└──────┬─────────────────────────────────────────────────────┘
       │
       │  Contesto condiviso (AgentContext)
       │  passato da un agente all'altro
       │
   ┌───▼────┐    ┌────────────┐    ┌───────────┐
   │ANALYST │───▶│ ARCHITECT  │───▶│ DEVELOPER │
   │        │    │            │    │           │
   │requirements │ architecture│    │  code.cs  │
   │   .md  │    │    .md     │    │           │
   └────────┘    └────────────┘    └─────┬─────┘
                                         │
                                   ┌─────▼─────┐    ┌────────┐
                                   │  QA AGENT │───▶│ DEVOPS │
                                   │           │    │ AGENT  │
                                   │ tests.cs  │    │ci.yml  │
                                   └───────────┘    │bicep   │
                                                    └────────┘
                                                         │
                                                         ▼
                                              Output Completo:
                                              📄 requirements.md
                                              📐 architecture.md
                                              💻 codice .cs
                                              🧪 test suite
                                              🚀 pipeline CI/CD
                                              ☁️  infrastruttura
```

---

## ⚡ Quick Start

```bash
# 1. Clona il repository
git clone https://github.com/Bl4zer97/bmad-agent-framework.git
cd bmad-agent-framework

# 2. Configura le credenziali Azure OpenAI
cp src/BmadAgentFramework/appsettings.json src/BmadAgentFramework/appsettings.local.json
# Modifica appsettings.local.json con il tuo endpoint e API key

# 3. Avvia il framework con una richiesta di esempio
dotnet run --project src/BmadAgentFramework "Crea una REST API per gestire una lista di task"
```

L'output verrà salvato nella cartella `output/` nella directory corrente.

---

## 🧩 Come Funziona

### Il Flusso degli Agenti

Il framework BMAD orchestra 5 agenti specializzati in sequenza:

| # | Agente | Input | Output | Tecnologia |
|---|--------|-------|--------|------------|
| 1 | **Analyst** | Requisiti utente (testo libero) | `requirements.md` (PRD) | Azure OpenAI GPT-4o |
| 2 | **Architect** | requirements.md | `architecture.md` | Azure OpenAI GPT-4o |
| 3 | **Developer** | requirements.md + architecture.md | `codice.cs` | Azure OpenAI GPT-4o |
| 4 | **QA** | codice.cs + requirements.md | `tests.cs` | Azure OpenAI GPT-4o |
| 5 | **DevOps** | architecture.md | `ci.yml` + `main.bicep` | Azure OpenAI GPT-4o |

### Il Contesto Condiviso

Il segreto del framework è l'`AgentContext`: un oggetto condiviso che viene arricchito da ogni agente e passato al successivo. Questo permette a ogni agente di vedere il lavoro di tutti gli agenti precedenti.

```csharp
// Ogni agente riceve il contesto con tutto il lavoro fatto finora
var context = new AgentContext {
    Requirements = "Crea una REST API...",
    Artifacts = {
        "requirements" → "# PRD\n...",    // dall'Analyst
        "architecture" → "# Arch\n...",   // dall'Architect
        "code"         → "// TodoApp.cs", // dal Developer
    },
    ConversationHistory = [ ... ] // tutti i messaggi degli agenti precedenti
};
```

---

## 📁 Struttura del Progetto

```
bmad-agent-framework/
├── 📁 src/BmadAgentFramework/        # Progetto principale .NET 8
│   ├── Program.cs                    # Entry point con DI setup
│   └── BmadAgentFramework.csproj     # Progetto .NET 8
│
├── 📁 core/                          # Nucleo del framework
│   ├── abstractions/
│   │   ├── IAgent.cs                 # Interfaccia base tutti gli agenti
│   │   ├── IOrchestrator.cs          # Interfaccia orchestratore
│   │   └── IWorkflowStep.cs          # Interfaccia step workflow
│   ├── models/
│   │   ├── AgentContext.cs           # Contesto condiviso tra agenti ⭐
│   │   ├── AgentMessage.cs           # Messaggi tra agenti
│   │   ├── WorkflowState.cs          # Stato del workflow
│   │   └── ProjectArtifact.cs        # Artefatti prodotti
│   ├── services/
│   │   ├── AzureOpenAIService.cs     # Integrazione Azure OpenAI + Polly
│   │   ├── MemoryService.cs          # Memoria condivisa (in-memory/CosmosDB)
│   │   └── ArtifactStore.cs          # Store artefatti (in-memory/Blob)
│   └── configuration/
│       ├── AgentConfiguration.cs     # Config per ogni agente
│       └── FrameworkOptions.cs       # Opzioni globali framework
│
├── 📁 agents/                        # Implementazioni degli agenti
│   ├── analyst/     ← RequirementsParser + AnalystAgent
│   ├── architect/   ← ArchitectureDesigner + ArchitectAgent
│   ├── developer/   ← CodeGenerator + DeveloperAgent
│   ├── qa/          ← TestGenerator + QAAgent
│   ├── devops/      ← PipelineGenerator + DevOpsAgent
│   └── orchestrator/ ← OrchestratorAgent + WorkflowEngine ⭐
│
├── 📁 azure/                         # Azure-specific components
│   ├── functions/
│   │   ├── OrchestratorFunction.cs   # Durable Functions orchestrazione
│   │   └── AgentFunction.cs          # Activity Functions per agenti
│   └── infrastructure/
│       ├── main.bicep                # IaC principale
│       └── modules/
│           ├── openai.bicep
│           ├── storage.bicep
│           └── servicebus.bicep
│
├── 📁 tests/                         # Test suite
│   └── BmadAgentFramework.Tests/
│       ├── AgentTests.cs             # Unit test agenti e modelli
│       └── OrchestratorTests.cs      # Unit test orchestratore
│
├── 📁 samples/                       # Esempi pratici
│   └── TodoAppGeneration/
│       ├── input.json                # Input di esempio
│       └── expected-output/          # Output atteso
│
└── 📁 docs/                          # Documentazione tecnica
    ├── architecture.md
    ├── workflow.md
    └── agents/
```

---

## ⚙️ Configurazione Azure OpenAI

1. **Crea una risorsa Azure OpenAI** nel tuo Azure Portal
2. **Deploy del modello** GPT-4o (o GPT-4-turbo)
3. **Configura** `appsettings.json`:

```json
{
  "BmadFramework": {
    "AzureOpenAIEndpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "AzureOpenAIApiKey": "YOUR-API-KEY",
    "DefaultModelDeployment": "gpt-4o",
    "ExecutionMode": "InMemory"
  }
}
```

> 💡 **Tip per la demo**: usa `"ExecutionMode": "InMemory"` per girare localmente senza Azure Storage o Service Bus.

---

## 💡 Esempio End-to-End

**Input** (stringa libera):
```
"Crea una REST API in C# .NET 8 per gestire una lista di task.
Deve supportare CRUD, filtri per priorità, autenticazione Azure AD
e deployment su Azure App Service."
```

**Output prodotto automaticamente**:

| Agente | File Prodotto | Contenuto |
|--------|---------------|-----------|
| Analyst | `requirements.md` | PRD con user stories, RF e RNF |
| Architect | `architecture.md` | Diagramma architetturale, servizi Azure, struttura progetto |
| Developer | `TodoApp.cs` | Codice C# completo: entity, controller, DI setup |
| QA | `TodoAppTests.cs` | Suite xUnit con unit e integration test |
| DevOps | `pipeline-and-infrastructure.md` | GitHub Actions YAML + Bicep |

---

## 🗺️ Roadmap

- [x] Framework base con 5 agenti specializzati
- [x] Integrazione Azure OpenAI (GPT-4o)
- [x] Orchestrazione con WorkflowEngine
- [x] Azure Durable Functions per scalabilità
- [x] Infrastruttura Bicep completa
- [x] Unit test con xUnit + FluentAssertions
- [ ] Integrazione CosmosDB per memoria persistente
- [ ] Interfaccia web (Blazor) per visualizzare il workflow in tempo reale
- [ ] Agente Security per code review automatica
- [ ] Support LangChain.NET / Semantic Kernel
- [ ] Multi-language support (Python, Java, TypeScript)
- [ ] Plugin system per agenti custom

---

## 🤝 Come Contribuire

1. Fork del repository
2. Crea un branch: `git checkout -b feat/nuova-funzionalita`
3. Implementa la funzionalità con test
4. Esegui i test: `dotnet test`
5. Apri una Pull Request

---

## 📄 Licenza

MIT License - vedi [LICENSE](LICENSE) per i dettagli.

---

*Costruito con ❤️ usando Azure OpenAI GPT-4o e .NET 8*