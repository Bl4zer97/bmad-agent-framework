# Developer Agent — Istruzioni Operative

Questo documento contiene le istruzioni operative dettagliate per il Developer Agent.
Per il ruolo e le responsabilità generali, vedere [`developer-agent.md`](developer-agent.md).

---

## 1. Regole di Generazione del Codice

### Formato Output Obbligatorio

Ogni file generato **DEVE** essere preceduto da un heading Markdown con il path completo relativo alla root della solution:

```
### src/NomeSolution.Layer/Cartella/NomeClasse.cs
```csharp
namespace NomeSolution.Layer.Cartella;

// codice completo...
```
```

Regole per il path:
- **Inizia con `src/`** per i progetti sorgente
- **Inizia con `tests/`** per i progetti di test
- Il namespace del file **DEVE** corrispondere al path (es. `src/MyApp.Domain/Entities/` → `namespace MyApp.Domain.Entities`)
- **Una sola classe, interfaccia, record o enum per file**
- **Nessun placeholder, TODO o `// ... resto del codice`**

### Naming Conventions

- **Classi e interfacce**: PascalCase (es. `OrderService`, `IOrderRepository`)
- **Metodi**: PascalCase (es. `CreateOrderAsync`)
- **Proprietà**: PascalCase (es. `OrderId`)
- **Variabili locali e parametri**: camelCase (es. `orderId`, `cancellationToken`)
- **Campi privati**: underscore + camelCase (es. `_repository`, `_logger`)
- **Costanti**: PascalCase o SCREAMING_SNAKE_CASE in base al contesto
- **Namespace**: corrisponde al path del file, usa il punto come separatore

### Completezza Obbligatoria

Il codice **DEVE** essere completo e compilabile. Non sono accettati:
- `// TODO: implementare`
- `throw new NotImplementedException();` (a meno che non sia il comportamento atteso)
- `// ... resto del codice`
- Metodi con body vuoto senza motivazione
- Riferimenti a tipi non definiti nel codice generato o in pacchetti NuGet noti

---

## 2. Ordine di Generazione per Layer

Il Developer **DEVE** generare i file seguendo l'ordine delle dipendenze della Clean Architecture:

```
1. Domain          → Entities, ValueObjects, Domain Events, Interfaces di dominio
2. Application     → DTOs, Commands/Queries, Handlers, Interfacce di servizi
3. Infrastructure  → Implementazioni di repository, client esterni, DbContext
4. API / Worker / Azure Functions  → Program.cs, Controller/Function, Middleware
5. Tests           → Unit test e Integration test
```

**Perché questo ordine**: ogni layer dipende solo dai layer interni. Generando dall'interno verso l'esterno, i tipi sono sempre disponibili quando vengono referenziati.

**Prima di generare qualsiasi file**, leggere il blocco `solution-structure` nell'architettura e elencare mentalmente tutti i progetti che si devono generare. Generare file **SOLO** per quei progetti.

---

## 3. Checklist di Validazione Pre-Completamento

Prima di dichiarare il codice completo, verificare **ogni punto**:

### Tipi e Dipendenze
- [ ] Ogni tipo custom usato (classe, interfaccia, record, enum) è definito in uno dei file generati?
- [ ] Ogni `using NomeSolution.X.Y;` ha un file corrispondente in `src/NomeSolution.X/Y/`?
- [ ] I tipi che non sono definiti localmente vengono da pacchetti NuGet noti (es. `ILogger`, `IMediator`, `ITelegramBotClient`)?
- [ ] Nessun tipo "inventato" (es. `ProjectClient` che non esiste in nessun SDK)?

### Entry Point e Configurazione
- [ ] Il progetto principale ha `Program.cs` completo con configurazione DI?
- [ ] `Program.cs` registra tutti i servizi definiti nei layer Application e Infrastructure?
- [ ] Per API/Web: sono presenti `appsettings.json` e `appsettings.Development.json`?
- [ ] Per Azure Functions: sono presenti `host.json` e `local.settings.json`?
- [ ] Per Worker Service: è presente `appsettings.json`?

### Coerenza Strutturale
- [ ] Il namespace di ogni file corrisponde al suo path nel filesystem?
- [ ] Tutti i file sono stati generati SOLO per i progetti definiti nel blocco `solution-structure`?
- [ ] Le cartelle create corrispondono alla struttura definita dall'Architect?

### Qualità del Codice
- [ ] Nessun placeholder, TODO o codice incompleto?
- [ ] Tutti i metodi asincroni usano `async/await`?
- [ ] Le classi pubbliche hanno XML documentation comments?
- [ ] Il logging usa `ILogger<T>` con messaggi strutturati?

---

## 4. Regole per la Coerenza Strutturale

### Seguire la Struttura dell'Architect

1. **Prima di scrivere qualsiasi codice**, leggere il blocco `solution-structure` dall'architettura
2. Elencare tutti i progetti: `NomeSolution.Domain`, `NomeSolution.Application`, ecc.
3. Generare file **ESCLUSIVAMENTE** per quei progetti
4. NON inventare progetti aggiuntivi (es. `NomeSolution.Shared`, `NomeSolution.Common`) se non previsti

### Rispetto della Gerarchia di Dipendenze

```
Domain         → nessuna dipendenza su altri layer del progetto
Application    → dipende solo da Domain
Infrastructure → dipende da Application (e indirettamente Domain)
API/Worker     → dipende da Application e Infrastructure
Tests          → dipende da Domain, Application (e opzionalmente Infrastructure)
```

**NON creare riferimenti circolari** tra i layer.

### Namespace e Path

Regola ferrea: il namespace **DEVE** rispecchiare il path del file.

| Path del file | Namespace |
|---|---|
| `src/MyApp.Domain/Entities/Order.cs` | `MyApp.Domain.Entities` |
| `src/MyApp.Application/Services/OrderService.cs` | `MyApp.Application.Services` |
| `src/MyApp.Infrastructure/Data/AppDbContext.cs` | `MyApp.Infrastructure.Data` |
| `src/MyApp.API/Controllers/OrdersController.cs` | `MyApp.API.Controllers` |
| `tests/MyApp.Tests/Unit/OrderServiceTests.cs` | `MyApp.Tests.Unit` |

---

## 5. Regole per Pacchetti NuGet Noti

### Azure Functions Isolated Worker (.NET 8)

Il `Program.cs` **DEVE** usare questo pattern esatto:

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        // registrare i propri servizi qui
    })
    .Build();

host.Run();
```

Il `.csproj` **DEVE** includere `<AzureFunctionsVersion>v4</AzureFunctionsVersion>` nel `PropertyGroup`.

I file `host.json` e `local.settings.json` sono **obbligatori**.

### Azure AI Agents (Azure.AI.Agents.Persistent)

Usare **SEMPRE** `PersistentAgentsClient`, **MAI** `ProjectsClient` o `AIProjectClient`:

```csharp
using Azure.AI.Agents.Persistent;

var client = new PersistentAgentsClient(endpoint, credential);
var thread = await client.Threads.CreateThreadAsync();
```

### Telegram.Bot v22.x

In v22.x il metodo **è `SendMessage`**, NON `SendTextMessageAsync` (rimosso):

```csharp
await botClient.SendMessage(chatId: chatId, text: "Risposta");
// NON usare: SendTextMessageAsync (rimosso in v22.x)
```

### MediatR (.NET 8)

```csharp
// Registrazione in Program.cs:
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Handler:
public record CreateOrderCommand(string ProductId) : IRequest<int>;

public class CreateOrderHandler(IOrderRepository repo) : IRequestHandler<CreateOrderCommand, int>
{
    public async Task<int> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        // implementazione completa
    }
}
```

### Entity Framework Core (.NET 8)

```csharp
// DbContext con primary constructor:
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

// Registrazione in Program.cs:
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

---

## 6. Anti-Pattern da Evitare

### ❌ Tipi inesistenti

```csharp
// SBAGLIATO: ProjectClient non esiste in nessun SDK Azure
var client = new ProjectClient(endpoint, credential);

// CORRETTO: usa il client effettivo del SDK
var client = new PersistentAgentsClient(endpoint, credential);
```

### ❌ Metodi inesistenti o rinominati

```csharp
// SBAGLIATO: SendTextMessageAsync è rimosso in Telegram.Bot v22.x
await botClient.SendTextMessageAsync(chatId, text);

// CORRETTO:
await botClient.SendMessage(chatId: chatId, text: text);
```

### ❌ Program.cs incompleto per Azure Functions

```csharp
// SBAGLIATO: non funzionerà per dotnet-isolated
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();

// CORRETTO per Azure Functions Isolated Worker:
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .Build();
host.Run();
```

### ❌ Interfacce definite solo dove vengono usate

```csharp
// SBAGLIATO: ITelegramMessageSender è usato ma non definito
public class TelegramWebhookHandler(ITelegramMessageSender sender) { }

// CORRETTO: generare anche il file dell'interfaccia
// src/MyApp.Application/Interfaces/ITelegramMessageSender.cs
public interface ITelegramMessageSender
{
    Task SendMessageAsync(long chatId, string text, CancellationToken ct = default);
}
```

### ❌ Namespace che non corrispondono al path

```csharp
// SBAGLIATO: il file è in src/MyApp.Domain/Entities/ ma il namespace è diverso
namespace MyApp.Domain; // file in src/MyApp.Domain/Entities/

// CORRETTO:
namespace MyApp.Domain.Entities;
```
