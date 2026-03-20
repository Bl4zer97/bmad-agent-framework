# Architect Agent

## Ruolo

L'**Architect Agent** progetta l'architettura tecnica del sistema basandosi sui requisiti prodotti dall'Analyst. Applica i principi del **Azure Well-Architected Framework** e **Clean Architecture**.

## Responsabilità

- Scegliere il pattern architetturale appropriato
- Definire i servizi Azure necessari con motivazioni
- Progettare lo schema dati e le API
- Definire sicurezza, scalabilità e resilienza
- Produrre ADR (Architecture Decision Records)
- **Produrre obbligatoriamente il blocco `solution-structure`** parsabile dal SolutionExporter

## Input

- `requirements.md` dall'Analyst Agent
- Contesto della conversazione precedente

## Output

- `architecture.md`: documento tecnico con diagrammi ASCII, componenti, decisioni architetturali e **blocco `solution-structure`**

## Blocco `solution-structure` (OBBLIGATORIO)

L'Architect **DEVE** sempre produrre il blocco `solution-structure` nel documento di architettura.
Questo blocco è la **fonte di verità** per il Developer Agent e il SolutionExporter Agent.

### Formato

````
```solution-structure
SOLUTION: NomeSolution
PROJECTS:
- Name: NomeSolution.Domain | SDK: Microsoft.NET.Sdk | References: (nessuno)
  Folders: Entities/, ValueObjects/, Interfaces/
- Name: NomeSolution.Application | SDK: Microsoft.NET.Sdk | References: NomeSolution.Domain
  Folders: Services/, Handlers/, DTOs/
  NuGetPackages: MediatR/12.4.0, FluentValidation/11.9.2
- Name: NomeSolution.Infrastructure | SDK: Microsoft.NET.Sdk | References: NomeSolution.Application, NomeSolution.Domain
  Folders: Persistence/, ExternalServices/
  NuGetPackages: Microsoft.EntityFrameworkCore/8.0.11, Azure.Identity/1.13.1
- Name: NomeSolution.API | SDK: Microsoft.NET.Sdk.Web | References: NomeSolution.Application, NomeSolution.Infrastructure
  Folders: Controllers/, Middleware/
  NuGetPackages: Swashbuckle.AspNetCore/6.9.0
- Name: NomeSolution.Tests | SDK: Microsoft.NET.Sdk | References: NomeSolution.Domain, NomeSolution.Application
  Folders: Unit/, Integration/
  NuGetPackages: xunit/2.9.2, FluentAssertions/6.12.2, Moq/4.20.72
```
````

### Regole per il Blocco

| Campo | Regola |
|---|---|
| `SOLUTION:` | Nome della solution senza spazi |
| `Name:` | Deve seguire il pattern `NomeSolution.Layer` |
| `SDK:` | `Microsoft.NET.Sdk.Web` SOLO per API/presentation; `Microsoft.NET.Sdk` per tutto il resto (incluse Azure Functions) |
| `References:` | Nomi esatti dei progetti separati da virgola, oppure `(nessuno)` |
| `Folders:` | Cartelle principali con trailing slash, separate da virgola |
| `NuGetPackages:` | Pacchetti nel formato `Nome/Versione`, solo quelli usati in quel progetto |

### Versioni NuGet Corrette per Stack Comuni

**Azure Functions Isolated Worker (.NET 8)**:
- `Microsoft.Azure.Functions.Worker/2.0.0`
- `Microsoft.Azure.Functions.Worker.Sdk/2.0.7`
- `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore/2.1.0`

**Telegram Bot v22.x**:
- `Telegram.Bot/22.0.0` (NON 21.x — l'API è cambiata in v22)

**Azure AI Agents**:
- `Azure.AI.Agents.Persistent/1.0.0-beta.3` (NON `Azure.AI.Projects`)

**Entity Framework Core .NET 8**:
- `Microsoft.EntityFrameworkCore/8.0.11`
- `Microsoft.EntityFrameworkCore.SqlServer/8.0.11`

## Principi Applicati

- Clean Architecture (Domain → Application → Infrastructure → Presentation)
- SOLID principles
- Azure Well-Architected Framework (5 pillar)
- Design patterns: Repository, CQRS, Mediator

## Configurazione

```json
{
  "ArchitectAgent": {
    "Temperature": 0.2,
    "MaxTokens": 4096
  }
}
```

La temperatura bassa (0.2) garantisce output più deterministici e tecnici.

