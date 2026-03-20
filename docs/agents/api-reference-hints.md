# API Reference Hints — Snippet Corretti per i Pacchetti Usati

Questo documento contiene snippet di codice **reali e funzionanti** per i pattern più comuni
usati nelle solution generate dal Developer Agent. Viene iniettato nel prompt del Developer
per ridurre le allucinazioni sulle API.

> **IMPORTANTE**: Usa ESATTAMENTE i nomi di classi, metodi e pattern mostrati.
> NON inventare nomi di metodi o classi che non esistono nei pacchetti.

---

## Azure Functions Isolated Worker (.NET 8)

### Program.cs

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        // Registrare i servizi applicativi qui
    })
    .Build();

host.Run();
```

> **NON usare** `WebApplication.CreateBuilder()` per Azure Functions Isolated Worker.
> **NON usare** `ConfigureFunctionsWorkerDefaults()` per .NET 8 — usa `ConfigureFunctionsWebApplication()`.

### Pacchetti NuGet richiesti

| Pacchetto | Versione |
|---|---|
| `Microsoft.Azure.Functions.Worker` | `2.0.0` |
| `Microsoft.Azure.Functions.Worker.Sdk` | `2.0.7` (con `OutputItemType="Analyzer"`) |
| `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` | `2.1.0` |
| `Microsoft.ApplicationInsights.WorkerService` | `2.22.0` |

### PropertyGroup .csproj richiesto

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <AzureFunctionsVersion>v4</AzureFunctionsVersion>
  <OutputType>Exe</OutputType>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.0.0" />
  <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.0.7" OutputItemType="Analyzer" />
  <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="2.1.0" />
  <PackageReference Include="Microsoft.ApplicationInsights.WorkerService" Version="2.22.0" />
</ItemGroup>
```

### HTTP Trigger Function

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MyApp.Functions;

public class MyHttpFunction(ILogger<MyHttpFunction> logger)
{
    [Function("MyHttpFunction")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req,
        CancellationToken ct)
    {
        logger.LogInformation("HTTP trigger elaborato.");
        var body = await new StreamReader(req.Body).ReadToEndAsync(ct);
        return new OkObjectResult(new { message = "OK", body });
    }
}
```

### host.json obbligatorio

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request"
      },
      "enableLiveDiagnosticsExceptionToErrorLevel": true
    }
  }
}
```

### local.settings.json obbligatorio

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

---

## ASP.NET Core Minimal API (.NET 8)

### Program.cs

```csharp
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Registrare i servizi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Aggiungere i propri servizi qui
builder.Services.AddScoped<IMyService, MyService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Registrare gli endpoint
app.MapGet("/api/items", async (IMyService svc, CancellationToken ct) =>
    await svc.GetAllAsync(ct));

app.Run();
```

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

## Telegram.Bot v22.x

> **ATTENZIONE**: In v22.x `SendTextMessageAsync` è stato **rimosso**.
> Il metodo corretto è `SendMessage`.

```csharp
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

// Inizializzazione client
var botClient = new TelegramBotClient("TOKEN");

// Invio messaggio — METODO CORRETTO in v22.x:
await botClient.SendMessage(
    chatId: chatId,
    text: "Risposta dell'agente",
    cancellationToken: ct);

// NON usare (rimosso in v22.x):
// await botClient.SendTextMessageAsync(chatId, text); // ❌ RIMOSSO

// Parsing del webhook update:
var update = JsonSerializer.Deserialize<Update>(body);
if (update?.Message?.Text is { } messageText)
{
    var chatId = update.Message.Chat.Id;
    // elabora il messaggio
}
```

### Pacchetti NuGet

| Pacchetto | Versione |
|---|---|
| `Telegram.Bot` | `22.0.0` |

---

## Azure.AI.Agents.Persistent (ex Azure.AI.Projects)

> **ATTENZIONE**: Il client corretto è `PersistentAgentsClient`, **NON** `ProjectsClient` o `AIProjectClient`.

```csharp
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;

// Inizializzazione client — USA PersistentAgentsClient
var credential = new DefaultAzureCredential();
var client = new PersistentAgentsClient(
    endpoint: new Uri(configuration["AzureAI:Endpoint"]!),
    credential: credential);

// Creazione thread
var thread = await client.Threads.CreateThreadAsync(cancellationToken: ct);

// Invio messaggio al thread
await client.Messages.CreateMessageAsync(
    threadId: thread.Value.Id,
    role: MessageRole.User,
    content: userMessage,
    cancellationToken: ct);

// Avvio run con agente pre-esistente
var run = await client.Runs.CreateRunAsync(
    threadId: thread.Value.Id,
    assistantId: configuration["AzureAI:AgentId"]!,
    cancellationToken: ct);

// Polling del run fino a completamento
do
{
    await Task.Delay(500, ct);
    run = await client.Runs.GetRunAsync(thread.Value.Id, run.Value.Id, ct);
} while (run.Value.Status == RunStatus.Queued || run.Value.Status == RunStatus.InProgress);

// Lettura risposta
var messages = client.Messages.GetMessagesAsync(thread.Value.Id, cancellationToken: ct);
await foreach (var msg in messages)
{
    if (msg.Role == MessageRole.Assistant)
    {
        foreach (var content in msg.ContentItems)
        {
            if (content is MessageTextContent textContent)
                return textContent.Text;
        }
        break;
    }
}
```

### Pacchetti NuGet

| Pacchetto | Versione |
|---|---|
| `Azure.AI.Agents.Persistent` | `1.0.0-beta.3` |
| `Azure.Identity` | `1.13.1` |

---

## Entity Framework Core (.NET 8)

```csharp
using Microsoft.EntityFrameworkCore;

// DbContext con primary constructor (.NET 8):
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MyEntity> MyEntities => Set<MyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

// Configurazione entità:
public class MyEntityConfiguration : IEntityTypeConfiguration<MyEntity>
{
    public void Configure(EntityTypeBuilder<MyEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
    }
}

// Registrazione in Program.cs:
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

### Pacchetti NuGet

| Pacchetto | Versione |
|---|---|
| `Microsoft.EntityFrameworkCore` | `8.0.11` |
| `Microsoft.EntityFrameworkCore.SqlServer` | `8.0.11` |
| `Microsoft.EntityFrameworkCore.Design` | `8.0.11` |

---

## MediatR (.NET 8)

```csharp
using MediatR;

// Registrazione in Program.cs:
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Command con risposta:
public record CreateItemCommand(string Name, string Description) : IRequest<int>;

// Handler con primary constructor (.NET 8):
public class CreateItemHandler(IItemRepository repo, ILogger<CreateItemHandler> logger)
    : IRequestHandler<CreateItemCommand, int>
{
    public async Task<int> Handle(CreateItemCommand request, CancellationToken ct)
    {
        logger.LogInformation("Creazione item: {Name}", request.Name);
        var item = new Item { Name = request.Name, Description = request.Description };
        return await repo.AddAsync(item, ct);
    }
}

// Query:
public record GetItemByIdQuery(int Id) : IRequest<ItemDto?>;

public class GetItemByIdHandler(IItemRepository repo)
    : IRequestHandler<GetItemByIdQuery, ItemDto?>
{
    public async Task<ItemDto?> Handle(GetItemByIdQuery request, CancellationToken ct)
        => await repo.GetByIdAsync(request.Id, ct) is { } item
            ? new ItemDto(item.Id, item.Name)
            : null;
}
```

### Pacchetti NuGet

| Pacchetto | Versione |
|---|---|
| `MediatR` | `12.4.0` |

---

## FluentValidation (.NET 8)

```csharp
using FluentValidation;

public class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);
    }
}

// Registrazione in Program.cs:
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
```

### Pacchetti NuGet

| Pacchetto | Versione |
|---|---|
| `FluentValidation` | `11.9.2` |
| `FluentValidation.AspNetCore` | `11.3.0` |

---

## Azure Service Bus (.NET 8)

```csharp
using Azure.Messaging.ServiceBus;
using Azure.Identity;

// Client con DefaultAzureCredential (raccomandato):
var client = new ServiceBusClient(
    fullyQualifiedNamespace: configuration["ServiceBus:Namespace"]!,
    credential: new DefaultAzureCredential());

// Sender:
var sender = client.CreateSender(queueOrTopicName);
await sender.SendMessageAsync(new ServiceBusMessage(body), ct);

// Processor:
var processor = client.CreateProcessor(queueName);
processor.ProcessMessageAsync += async args =>
{
    var body = args.Message.Body.ToString();
    await args.CompleteMessageAsync(args.Message, ct);
};
processor.ProcessErrorAsync += args => { /* log */ return Task.CompletedTask; };
await processor.StartProcessingAsync(ct);
```

### Pacchetti NuGet

| Pacchetto | Versione |
|---|---|
| `Azure.Messaging.ServiceBus` | `7.18.2` |
| `Azure.Identity` | `1.13.1` |
