# TechResearch Agent

## Ruolo

Il **TechResearch Agent** è il Tech Lead virtuale del framework BMAD. Opera tra l'Architect e il Developer
per verificare e documentare le API corrette delle librerie scelte, prevenendo allucinazioni del modello
su nomi di classi, metodi e versioni NuGet.

## Responsabilità

- Analizzare il documento di architettura per identificare pacchetti NuGet, librerie e servizi Azure
- Produrre versioni NuGet stable verificate (senza inventare versioni inesistenti)
- Documentare snippet di inizializzazione corretti per ogni libreria
- Identificare anti-pattern comuni da evitare (es. API deprecate o rinominate)
- Produrre pattern DI registration per ogni servizio
- Fornire template per file di configurazione (appsettings.json, host.json, local.settings.json)

## Input

- `architecture.md` dall'Architect Agent (obbligatorio)

## Output

- `tech-reference.md`: documento di riferimento tecnico con versioni NuGet, snippet API e anti-pattern

## Fase

`WorkflowPhase.TechResearch` (fase 2, tra Architecture=1 e Development=3)

## Workflow Aggiornato

```
Analyst (Analysis=0)
  ↓
Architect (Architecture=1)
  ↓
TechResearchAgent (TechResearch=2)  ← NUOVO
  ↓
Developer (Development=3)
  ↓
QA (QualityAssurance=4)
  ↓
DevOps (DevOps=5)
  ↓
SolutionExporter
```

## Struttura del Documento Prodotto

Il TechResearchAgent produce un `tech-reference.md` con questa struttura:

```markdown
# Riferimento Tecnico — {ProjectName}

## 1. Pacchetti NuGet Verificati
| Pacchetto | Versione Stable | Note |
|-----------|----------------|------|
| ... | ... | ... |

## 2. Inizializzazione Servizi (Program.cs)
Per ogni servizio, snippet DI completo...

## 3. API Reference per Libreria
Per ogni libreria, le classi principali con metodi corretti...

## 4. Anti-Pattern da Evitare
Lista di errori comuni e la versione corretta...

## 5. File di Configurazione Richiesti
Template per appsettings.json, host.json, etc...
```

## Principio Fondamentale: Precisione > Completezza

Il TechResearchAgent usa **temperatura molto bassa (0.1)** per massimizzare la precisione.
Se non è certo di un'API o di una versione NuGet, scrive esplicitamente **"VERIFICARE MANUALMENTE"**
piuttosto che inventare informazioni. Questo è fondamentale per evitare che il Developer Agent
generi codice con API inesistenti.

## Come il Developer Agent Usa il tech-reference

Il `DeveloperAgent` legge l'artefatto `tech-reference` (opzionale) e lo inietta nel prompt
del `CodeGenerator` come sezione prioritaria:

```
## Riferimento Tecnico (OBBLIGATORIO DA SEGUIRE)
{contenuto del tech-reference.md}

ATTENZIONE: le informazioni nel Riferimento Tecnico hanno la PRECEDENZA su qualsiasi tua
conoscenza pregressa. Usa ESATTAMENTE le versioni NuGet, i nomi di classi e i pattern
di inizializzazione documentati sopra.
```

## Anti-Pattern Documentati per Stack Comuni

### Azure AI Agents
| ❌ NON fare | ✅ Fare invece |
|------------|----------------|
| `using Azure.AI.Projects;` | `using Azure.AI.Agents.Persistent;` |
| `new ProjectsClient(...)` | `new PersistentAgentsClient(endpoint, credential)` |
| `new AIProjectClient(...)` | `new PersistentAgentsClient(endpoint, credential)` |

### Telegram.Bot v22.x
| ❌ NON fare | ✅ Fare invece |
|------------|----------------|
| `bot.SendTextMessageAsync(chatId, text)` | `bot.SendMessage(chatId, text)` |

### Azure Functions .NET 8 Isolated Worker
| ❌ NON fare | ✅ Fare invece |
|------------|----------------|
| `WebApplication.CreateBuilder()` | `new HostBuilder().ConfigureFunctionsWebApplication()` |
| SDK: `Microsoft.NET.Sdk.Web` | SDK: `Microsoft.NET.Sdk` |

## Configurazione

```json
{
  "TechResearchAgent": {
    "Temperature": 0.1,
    "MaxTokens": 8192
  }
}
```

La temperatura molto bassa (0.1) garantisce output deterministici e precisi,
minimizzando le allucinazioni su nomi di API e versioni NuGet.
