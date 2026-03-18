# Workflow BMAD - Come Funziona

## Il Ciclo di Vita di un Workflow

### 1. Avvio

L'utente fornisce una richiesta in linguaggio naturale:

```
"Crea una REST API in C# .NET 8 per gestire prenotazioni 
 di una palestra con Azure AD e SQL Database"
```

### 2. Inizializzazione

L'`OrchestratorAgent` crea un `AgentContext` condiviso:
- Genera un `ProjectId` univoco
- Imposta la fase iniziale (`Analysis`)
- Inizializza i repository per artefatti e memoria

### 3. Esecuzione delle Fasi

```
Fase 1: ANALYSIS (AnalystAgent)
├── Input: stringa dei requisiti
├── System prompt: "Sei un analista esperto in .NET/Azure..."
├── Invoca Azure OpenAI GPT-4o
└── Output: requirements.md (PRD strutturato)

Fase 2: ARCHITECTURE (ArchitectAgent)
├── Input: requirements.md + contesto
├── System prompt: "Sei un architect .NET/Azure esperto..."
├── Invoca Azure OpenAI GPT-4o
└── Output: architecture.md (diagrammi, servizi Azure, struttura)

Fase 3: DEVELOPMENT (DeveloperAgent)
├── Input: requirements.md + architecture.md + contesto
├── System prompt: "Sei un senior .NET developer..."
├── Invoca Azure OpenAI GPT-4o
└── Output: codice C# 12 (.cs files)

Fase 4: QA (QAAgent)
├── Input: code.cs + requirements.md + contesto
├── System prompt: "Sei un QA engineer con xUnit..."
├── Invoca Azure OpenAI GPT-4o
└── Output: test suite (xUnit + FluentAssertions)

Fase 5: DEVOPS (DevOpsAgent)
├── Input: architecture.md + contesto
├── System prompt: "Sei un DevOps engineer Azure/GitHub..."
├── Invoca Azure OpenAI GPT-4o
└── Output: ci.yml + main.bicep + deploy.md
```

### 4. Il Contesto Condiviso

Il segreto del BMAD è che ogni agente **non riparte da zero**. 
Riceve il contesto accumulato da tutti gli agenti precedenti:

```csharp
// Quando il Developer elabora, vede già:
context.Artifacts["requirements"]  // dall'Analyst
context.Artifacts["architecture"]  // dall'Architect
context.ConversationHistory        // tutti i messaggi precedenti
```

Questo riduce drasticamente le **allucinazioni** dell'AI e garantisce **coerenza** tra gli output.

### 5. Completamento

Al termine, tutti gli artefatti vengono esportati in un unico documento:
```
output/bmad-output-{projectId}.md
```

## Human-in-the-Loop

Il framework supporta l'approvazione umana tra le fasi (via Azure Durable Functions):

```
Analyst completa → ⏸️ Attende approvazione umana → Architect inizia
```

Configurazione:
```json
{
  "Agents": {
    "AnalystAgent": {
      "RequiresHumanApproval": true
    }
  }
}
```

## Gestione degli Errori

```
Errore nell'agente
       │
       ├── Errore transitorio (rete, rate limit)?
       │       └── Retry con backoff esponenziale (Polly)
       │
       ├── Errore permanente?
       │       └── WorkflowState.CurrentPhase = Failed
       │           WorkflowState.ErrorMessage = "..."
       │
       └── Cancellazione utente?
               └── OperationCanceledException → WorkflowState.Failed
```
