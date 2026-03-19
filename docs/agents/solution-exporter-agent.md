# Solution Exporter Agent

## Ruolo

Il **Solution Exporter Agent** è il **Build/Solution Engineer** del framework BMAD.
Scrive su disco la .NET solution generata dal Developer Agent, rispettando **fedelmente** la struttura definita dall'Architect Agent.

> **Principio guida**: L'Architect è il senior della struttura. Il SolutionExporter **ESEGUE**, non decide.

## Responsabilità

- Leggere l'artefatto `code` prodotto dal Developer Agent (Markdown con blocchi C# e JSON)
- Leggere l'artefatto `architecture` prodotto dall'Architect Agent (struttura multi-progetto)
- Estrarre i blocchi `csharp`, `json` e `xml` dal Markdown rispettando i path definiti dal Developer
- Parsare il blocco `solution-structure` dall'architettura come fonte di verità
- Creare la struttura multi-progetto su disco: `.sln`, `.csproj` multipli con `ProjectReference` e `PackageReference` corretti
- Organizzare i file `.cs`, `.json`, `.xml` nelle cartelle corrette rispettando i path relativi
- Creare le cartelle definite dall'Architect con `.gitkeep` se vuote
- Inferire i pacchetti NuGet dai `using` nel codice quando non definiti dall'Architect
- Generare un README con la struttura della solution

## Input

| Artefatto     | Prodotto da     | Obbligatorio | Descrizione                                          |
|---------------|-----------------|:------------:|------------------------------------------------------|
| `code`        | DeveloperAgent  | ✅           | Markdown con blocchi `csharp`/`json` e heading `### path`   |
| `architecture`| ArchitectAgent  | ⚠️ opzionale | Documento con blocco `solution-structure` parsabile  |

## Output

- Solution .NET su disco in `output/{ProjectName}-solution/`
- File `.sln` con tutti i progetti registrati (GUID univoci)
- File `.csproj` per ogni progetto con `ProjectReference` e `PackageReference` corretti
- File `.cs`, `.json`, `.xml` organizzati nelle cartelle corrette
- Cartelle vuote (definite dall'Architect) con file `.gitkeep`
- `README.md` con la struttura della solution

### Struttura generata (esempio)

```
TodoApp-solution/
├── TodoApp.sln                          ← tutti i progetti registrati
├── src/
│   ├── TodoApp.Domain/
│   │   ├── TodoApp.Domain.csproj        ← nessun ProjectReference, nessun PackageReference
│   │   ├── Entities/
│   │   │   └── TodoItem.cs
│   │   ├── Interfaces/
│   │   │   └── ITodoRepository.cs
│   │   └── ValueObjects/
│   │       └── .gitkeep                 ← cartella vuota definita dall'Architect
│   ├── TodoApp.Application/
│   │   ├── TodoApp.Application.csproj   ← ProjectReference → Domain
│   │   │                                   PackageReference: MediatR 12.4.0, FluentValidation 11.9.2
│   │   ├── Services/
│   │   │   └── TodoService.cs
│   │   └── DTOs/
│   │       └── CreateTodoRequest.cs
│   └── TodoApp.API/
│       ├── TodoApp.API.csproj           ← ProjectReference → Application, Infrastructure
│       │                                   PackageReference: Swashbuckle.AspNetCore 6.9.0
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── Controllers/
│           └── TodoController.cs
├── tests/
│   └── TodoApp.Tests/
│       ├── TodoApp.Tests.csproj         ← ProjectReference → Domain, Application
│       │                                   PackageReference: xunit, FluentAssertions, Moq
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
  NuGetPackages: MediatR/12.4.0, FluentValidation/11.9.2, AutoMapper/13.0.1
- Name: NomeSolution.Infrastructure | SDK: Microsoft.NET.Sdk | References: NomeSolution.Application
  Folders: Data/, Repositories/, Configurations/, Services/
  NuGetPackages: Microsoft.EntityFrameworkCore/8.0.11, Microsoft.EntityFrameworkCore.SqlServer/8.0.11
- Name: NomeSolution.API | SDK: Microsoft.NET.Sdk.Web | References: NomeSolution.Application, NomeSolution.Infrastructure
  Folders: Controllers/, Middleware/, Extensions/
  NuGetPackages: Swashbuckle.AspNetCore/6.9.0, Serilog.AspNetCore/8.0.3
- Name: NomeSolution.Tests | SDK: Microsoft.NET.Sdk | References: NomeSolution.Domain, NomeSolution.Application
  Folders: Unit/, Integration/
  NuGetPackages: xunit/2.9.2, FluentAssertions/6.12.2, Moq/4.20.72, Microsoft.NET.Test.Sdk/17.12.0
```

### Regole di parsing

| Campo          | Formato                                              | Esempio                              |
|----------------|------------------------------------------------------|--------------------------------------|
| `SOLUTION`     | `SOLUTION: <nome>`                                   | `SOLUTION: TodoApp`                  |
| `Name`         | Nome del progetto .NET                               | `TodoApp.Domain`                     |
| `SDK`          | SDK MSBuild                                          | `Microsoft.NET.Sdk.Web`              |
| `References`   | Nomi separati da virgola, oppure `(nessuno)`         | `TodoApp.Domain, TodoApp.Application`|
| `Folders`      | Cartelle suggerite separate da virgola               | `Entities/, ValueObjects/`           |
| `NuGetPackages`| Pacchetti NuGet nel formato `Nome/Versione`, separati da virgola | `MediatR/12.4.0, FluentValidation/11.9.2` |

### SDK supportati

| SDK                      | Tipo progetto                     |
|--------------------------|-----------------------------------|
| `Microsoft.NET.Sdk`      | Librerie, applicazioni console, test |
| `Microsoft.NET.Sdk.Web`  | API Web, applicazioni ASP.NET Core |
| `Microsoft.NET.Sdk.Worker` | Worker Service                  |

### Posizione dei progetti

- Progetti **non** di test → cartella `src/`
- Progetti con nome che termina in `Tests` o `Test` → cartella `tests/`

## Inferenza NuGet Automatica

Se l'Architect non specifica `NuGetPackages:` per un progetto, il SolutionExporter analizza
le direttive `using` nel codice generato e mappa i namespace noti ai pacchetti NuGet corrispondenti.

| Namespace prefix | NuGet Package | Versione |
|---|---|---|
| `MediatR` | MediatR | 12.4.0 |
| `FluentValidation.AspNetCore` | FluentValidation.AspNetCore | 11.3.0 |
| `FluentValidation` | FluentValidation | 11.9.2 |
| `Microsoft.EntityFrameworkCore.SqlServer` | Microsoft.EntityFrameworkCore.SqlServer | 8.0.11 |
| `Microsoft.EntityFrameworkCore` | Microsoft.EntityFrameworkCore | 8.0.11 |
| `Azure.Identity` | Azure.Identity | 1.13.1 |
| `Azure.Security.KeyVault` | Azure.Security.KeyVault.Secrets | 4.6.0 |
| `Azure.Messaging.ServiceBus` | Azure.Messaging.ServiceBus | 7.18.2 |
| `Azure.Storage.Blobs` | Azure.Storage.Blobs | 12.22.2 |
| `Azure.AI.Projects` | Azure.AI.Projects | 1.0.0-beta.6 |
| `Telegram.Bot` | Telegram.Bot | 21.3.1 |
| `Swashbuckle` | Swashbuckle.AspNetCore | 6.9.0 |
| `Polly` | Polly | 8.5.0 |
| `Serilog` | Serilog.AspNetCore | 8.0.3 |
| `AutoMapper` | AutoMapper | 13.0.1 |
| `Mapster` | Mapster | 7.4.0 |
| `Microsoft.Azure.Functions.Worker` | Microsoft.Azure.Functions.Worker | 1.23.0 |
| `Xunit` / `xunit` | xunit | 2.9.2 |
| `FluentAssertions` | FluentAssertions | 6.12.2 |
| `Moq` | Moq | 4.20.72 |
| `NSubstitute` | NSubstitute | 5.3.0 |

## Fallback

Se il blocco `solution-structure` non è presente nell'architettura, il SolutionExporter
**inferisce** la struttura dai path dei file generati:

- File con path `src/ProjectName/...` → crea progetto `ProjectName` in `src/`
- File con path `tests/ProjectName/...` → crea progetto `ProjectName` in `tests/`
- File senza path prefix → finiscono nel progetto di default `src/{ProjectName}/`
- I pacchetti NuGet vengono inferiti automaticamente dai `using` nel codice

## Caratteristiche

- **DETERMINISTICO**: zero chiamate AI → zero costi aggiuntivi
- **Non decide**: segue le decisioni dell'Architect, non le reinventa
- **Multi-progetto**: supporta da 1 a N progetti nella stessa solution
- **Portabile**: i path relativi nei `.csproj` funzionano su Windows, Linux e macOS
- **NuGet-aware**: include i `PackageReference` nei `.csproj` (da Architect o inferiti)
- **Completo**: genera file di configurazione JSON, crea cartelle vuote con `.gitkeep`

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

