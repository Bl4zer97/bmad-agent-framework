# Developer Agent

## Ruolo

Il **Developer Agent** genera il codice C# di alta qualità implementando la struttura definita dall'Architect Agent.

> **Principio guida**: L'Architect è SENIOR sulla struttura. Il Developer **SEGUE** le decisioni dell'Architect, non le reinventa.

## Responsabilità

- Leggere la struttura del progetto dalla sezione "Struttura del Progetto .NET" del documento dell'Architect
- Generare codice C# 12 con best practice moderne per ogni layer definito dall'Architect
- Scrivere **ogni file** con il path completo relativo alla root della solution come heading Markdown
- Produrre codice compilabile e funzionante (nessun placeholder o TODO)
- NON inventare progetti, layer o namespace non previsti dall'architettura

## Input

- `requirements.md` dall'Analyst
- `architecture.md` dall'Architect (include il blocco `solution-structure` — fonte di verità per la struttura)
- Cronologia completa della conversazione

## Output

Documento Markdown con blocchi `csharp`, uno per file, ognuno preceduto dal suo path completo:

```markdown
### src/MyApp.Domain/Entities/TodoItem.cs
```csharp
namespace MyApp.Domain.Entities;
public record TodoItem(int Id, string Title, bool IsDone);
```

### src/MyApp.Application/Services/TodoService.cs
```csharp
namespace MyApp.Application.Services;
public class TodoService(ITodoRepository repo) { }
```
```

Il path di ogni file:
- **Inizia con `src/`** per i progetti sorgente
- **Inizia con `tests/`** per i progetti di test
- Rispecchia esattamente la struttura definita dall'Architect nel blocco `solution-structure`

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
- Name: MyApp.Infrastructure | SDK: Microsoft.NET.Sdk | References: MyApp.Application
  Folders: Data/, Repositories/, Configurations/
- Name: MyApp.API | SDK: Microsoft.NET.Sdk.Web | References: MyApp.Application, MyApp.Infrastructure
  Folders: Controllers/, Middleware/, Extensions/
- Name: MyApp.Tests | SDK: Microsoft.NET.Sdk | References: MyApp.Domain, MyApp.Application
  Folders: Unit/, Integration/
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

