# Developer Agent

## Ruolo

Il **Developer Agent** genera il codice C# di alta qualità implementando la struttura definita dall'Architect Agent.

> **Principio guida**: L'Architect è SENIOR sulla struttura. Il Developer **SEGUE** le decisioni dell'Architect, non le reinventa.

> **Istruzioni operative dettagliate**: vedere [`developer-agent-instructions.md`](developer-agent-instructions.md) per le regole complete di generazione del codice, checklist di validazione e snippet API corretti.
> **Reference API**: vedere [`api-reference-hints.md`](api-reference-hints.md) per gli snippet corretti dei pacchetti più usati (Azure Functions, Telegram.Bot v22.x, Azure.AI.Agents.Persistent, ecc.).

## Responsabilità

- Leggere la struttura del progetto dalla sezione "Struttura del Progetto .NET" del documento dell'Architect
- Generare codice C# 12 con best practice moderne per ogni layer definito dall'Architect
- Scrivere **ogni file** con il path completo relativo alla root della solution come heading Markdown
- Produrre codice compilabile e funzionante (nessun placeholder o TODO)
- NON inventare progetti, layer o namespace non previsti dall'architettura
- Generare **sempre** il file `Program.cs` per il progetto principale
- Generare i file di configurazione (`appsettings.json`, `host.json`, ecc.)
- Garantire la **completezza**: ogni tipo custom usato deve essere definito nel codice generato

## Input

- `requirements.md` dall'Analyst
- `architecture.md` dall'Architect (include il blocco `solution-structure` — fonte di verità per la struttura)
- Cronologia completa della conversazione

## Output

Documento Markdown con blocchi `csharp` e `json`, ognuno preceduto dal suo path completo:

```markdown
### src/MyApp.Domain/Entities/TodoItem.cs
```csharp
namespace MyApp.Domain.Entities;
public record TodoItem(int Id, string Title, bool IsDone);
```

### src/MyApp.API/Program.cs
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
// ...
```

### src/MyApp.API/appsettings.json
```json
{ "Logging": { "LogLevel": { "Default": "Information" } } }
```
```

Il path di ogni file:
- **Inizia con `src/`** per i progetti sorgente
- **Inizia con `tests/`** per i progetti di test
- Rispecchia esattamente la struttura definita dall'Architect nel blocco `solution-structure`

## Regole Critiche

### Regola di Completezza
Ogni tipo custom usato **DEVE** essere definito nel codice generato:
- Se usi `ITodoRepository`, genera il file `ITodoRepository.cs`
- Se usi `CreateTodoRequest`, genera il file `CreateTodoRequest.cs`
- Tipi da pacchetti NuGet (es. `ILogger`, `IMediator`, `ITelegramBotClient`) **non** vanno definiti

### Regola Entry Point
Il progetto principale (API, Worker, Azure Functions) **DEVE** avere `Program.cs`:
- Configura il DI container, middleware e routing
- Registra tutti i servizi di Application e Infrastructure
- Per Azure Functions: usa `HostBuilder` con `.ConfigureFunctionsWorkerDefaults()`

### Regola File di Configurazione
- **API/Web**: genera `appsettings.json` e `appsettings.Development.json`
- **Azure Functions**: genera `host.json` e `local.settings.json`
- **Worker Service**: genera `appsettings.json`

## Struttura Seguita

La struttura è definita dall'Architect nel blocco `solution-structure` dell'architettura.
Il Developer **non decide** la struttura — la segue fedelmente.

Esempio di struttura definita dall'Architect:

```solution-structure
SOLUTION: MyApp
PROJECTS:
- Name: MyApp.Domain | SDK: Microsoft.NET.Sdk | References: (nessuno)
  Folders: Entities/, ValueObjects/, Events/, Interfaces/
- Name: MyApp.Application | SDK: Microsoft.NET.Sdk | References: MyApp.Domain
  Folders: DTOs/, Interfaces/, Services/, Validators/
  NuGetPackages: MediatR/12.4.0, FluentValidation/11.9.2
- Name: MyApp.Infrastructure | SDK: Microsoft.NET.Sdk | References: MyApp.Application
  Folders: Data/, Repositories/, Configurations/
  NuGetPackages: Microsoft.EntityFrameworkCore/8.0.11, Microsoft.EntityFrameworkCore.SqlServer/8.0.11
- Name: MyApp.API | SDK: Microsoft.NET.Sdk.Web | References: MyApp.Application, MyApp.Infrastructure
  Folders: Controllers/, Middleware/, Extensions/
  NuGetPackages: Swashbuckle.AspNetCore/6.9.0, Serilog.AspNetCore/8.0.3
- Name: MyApp.Tests | SDK: Microsoft.NET.Sdk | References: MyApp.Domain, MyApp.Application
  Folders: Unit/, Integration/
  NuGetPackages: xunit/2.9.2, FluentAssertions/6.12.2, Moq/4.20.72
```

La struttura **non è fissa**: l'Architect può definire 3 o 7 progetti, con o senza CQRS, Workers, ecc.

## Standard di Codice

```csharp
// Una sola classe/interfaccia/record per file
// Codice COMPLETO e compilabile — niente placeholder

public record CreateTodoRequest(string Title, Priority Priority);

// Primary constructors C# 12
public class TodoService(ITodoRepository repo, ILogger<TodoService> logger)
{
    // Async/await ovunque
    public async Task<Todo> CreateAsync(CreateTodoRequest req, CancellationToken ct = default)
    {
        // implementazione completa...
    }
}
```

## Configurazione

```json
{
  "DeveloperAgent": {
    "Temperature": 0.2,
    "MaxTokens": 16384
  }
}
```

`MaxTokens` alto (16384) perché la generazione dell'intera solution richiede molti token.
