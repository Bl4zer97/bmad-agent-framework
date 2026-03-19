# Solution Exporter Agent

## Ruolo

Il **Solution Exporter Agent** è il **Build/Solution Engineer** del framework BMAD.
Scrive su disco la .NET solution generata dal Developer Agent, rispettando **fedelmente** la struttura definita dall'Architect Agent.

> **Principio guida**: L'Architect è il senior della struttura. Il SolutionExporter **ESEGUE**, non decide.

## Responsabilità

- Leggere l'artefatto `code` prodotto dal Developer Agent (Markdown con blocchi C#)
- Leggere l'artefatto `architecture` prodotto dall'Architect Agent (struttura multi-progetto)
- Estrarre i blocchi `csharp` dal Markdown rispettando i path definiti dal Developer
- Parsare il blocco `solution-structure` dall'architettura come fonte di verità
- Creare la struttura multi-progetto su disco: `.sln`, `.csproj` multipli con `ProjectReference` corretti
- Organizzare i file `.cs` nelle cartelle corrette rispettando i path relativi
- Generare un README con la struttura della solution

## Input

| Artefatto     | Prodotto da     | Obbligatorio | Descrizione                                          |
|---------------|-----------------|:------------:|------------------------------------------------------|
| `code`        | DeveloperAgent  | ✅           | Markdown con blocchi `csharp` e heading `### path`   |
| `architecture`| ArchitectAgent  | ⚠️ opzionale | Documento con blocco `solution-structure` parsabile  |

## Output

- Solution .NET su disco in `output/{ProjectName}-solution/`
- File `.sln` con tutti i progetti registrati (GUID univoci)
- File `.csproj` per ogni progetto con `ProjectReference` corretti
- File `.cs` organizzati nelle cartelle corrette
- `README.md` con la struttura della solution

### Struttura generata (esempio)

```
TodoApp-solution/
├── TodoApp.sln                          ← tutti i progetti registrati
├── src/
│   ├── TodoApp.Domain/
│   │   ├── TodoApp.Domain.csproj        ← nessun ProjectReference
│   │   ├── Entities/
│   │   │   └── TodoItem.cs
│   │   └── Interfaces/
│   │       └── ITodoRepository.cs
│   ├── TodoApp.Application/
│   │   ├── TodoApp.Application.csproj   ← ProjectReference → Domain
│   │   ├── Services/
│   │   │   └── TodoService.cs
│   │   └── DTOs/
│   │       └── CreateTodoRequest.cs
│   ├── TodoApp.Infrastructure/
│   │   ├── TodoApp.Infrastructure.csproj← ProjectReference → Application
│   │   └── Data/
│   │       └── AppDbContext.cs
│   └── TodoApp.API/
│       ├── TodoApp.API.csproj           ← ProjectReference → Application, Infrastructure
│       └── Controllers/
│           └── TodoController.cs
├── tests/
│   └── TodoApp.Tests/
│       ├── TodoApp.Tests.csproj         ← ProjectReference → Domain, Application
│       └── Unit/
│           └── TodoServiceTests.cs
└── README.md
```

## Formato del Blocco `solution-structure`

Il blocco `solution-structure` deve essere presente nel documento di architettura (sezione 9).
È la **fonte di verità** per la struttura multi-progetto.

```solution-structure
SOLUTION: NomeSolution
PROJECTS:
- Name: NomeSolution.Domain | SDK: Microsoft.NET.Sdk | References: (nessuno)
  Folders: Entities/, ValueObjects/, Events/, Interfaces/
- Name: NomeSolution.Application | SDK: Microsoft.NET.Sdk | References: NomeSolution.Domain
  Folders: DTOs/, Interfaces/, Services/, Validators/
- Name: NomeSolution.Infrastructure | SDK: Microsoft.NET.Sdk | References: NomeSolution.Application
  Folders: Data/, Repositories/, Configurations/, Services/
- Name: NomeSolution.API | SDK: Microsoft.NET.Sdk.Web | References: NomeSolution.Application, NomeSolution.Infrastructure
  Folders: Controllers/, Middleware/, Extensions/
- Name: NomeSolution.Tests | SDK: Microsoft.NET.Sdk | References: NomeSolution.Domain, NomeSolution.Application
  Folders: Unit/, Integration/
```

### Regole di parsing

| Campo        | Formato                                              | Esempio                              |
|--------------|------------------------------------------------------|--------------------------------------|
| `SOLUTION`   | `SOLUTION: <nome>`                                   | `SOLUTION: TodoApp`                  |
| `Name`       | Nome del progetto .NET                               | `TodoApp.Domain`                     |
| `SDK`        | SDK MSBuild                                          | `Microsoft.NET.Sdk.Web`              |
| `References` | Nomi separati da virgola, oppure `(nessuno)`         | `TodoApp.Domain, TodoApp.Application`|
| `Folders`    | Cartelle suggerite separate da virgola               | `Entities/, ValueObjects/`           |

### SDK supportati

| SDK                      | Tipo progetto                     |
|--------------------------|-----------------------------------|
| `Microsoft.NET.Sdk`      | Librerie, applicazioni console, test |
| `Microsoft.NET.Sdk.Web`  | API Web, applicazioni ASP.NET Core |
| `Microsoft.NET.Sdk.Worker` | Worker Service                  |

### Posizione dei progetti

- Progetti **non** di test → cartella `src/`
- Progetti con nome che termina in `Tests` o `Test` → cartella `tests/`

## Fallback

Se il blocco `solution-structure` non è presente nell'architettura, il SolutionExporter
**inferisce** la struttura dai path dei file generati:

- File con path `src/ProjectName/...` → crea progetto `ProjectName` in `src/`
- File con path `tests/ProjectName/...` → crea progetto `ProjectName` in `tests/`
- File senza path prefix → finiscono nel progetto di default `src/{ProjectName}/`

I `.csproj` generati nel fallback **non** hanno `ProjectReference` (non è possibile inferirli).

## Caratteristiche

- **DETERMINISTICO**: zero chiamate AI → zero costi aggiuntivi
- **Non decide**: segue le decisioni dell'Architect, non le reinventa
- **Multi-progetto**: supporta da 1 a N progetti nella stessa solution
- **Portabile**: i path relativi nei `.csproj` funzionano su Windows, Linux e macOS

## Configurazione

Il SolutionExporterAgent non ha configurazione AI (temperatura, MaxTokens) perché
non effettua chiamate Azure OpenAI.

```json
{
  "SolutionExporterAgent": {
    // Nessuna configurazione AI necessaria
  }
}
```

## Fase di Attivazione

Il SolutionExporterAgent si aggancia alla fase **QualityAssurance** (dopo il DeveloperAgent),
richiedendo la presenza dell'artefatto `code` nel contesto.
