namespace BmadAgentFramework.Agents.Developer;

/// <summary>
/// Generatore di prompt per la generazione del codice C#.
/// Costruisce prompt dettagliati per il Developer Agent.
/// </summary>
public static class CodeGenerator
{
    // =========================================================================
    // Snippet API corretti per i pacchetti più comuni.
    // Iniettati nel prompt per ridurre le allucinazioni del modello.
    // =========================================================================

    private const string AzureFunctionsHints = """
        ### Azure Functions Isolated Worker (.NET 8) — Program.cs CORRETTO
        ```csharp
        using Microsoft.Azure.Functions.Worker;
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Hosting;

        var host = new HostBuilder()
            .ConfigureFunctionsWebApplication()   // .NET 8 usa questo, NON ConfigureFunctionsWorkerDefaults
            .ConfigureServices(services =>
            {
                services.AddApplicationInsightsTelemetryWorkerService();
                services.ConfigureFunctionsApplicationInsights();
                // Registrare i servizi applicativi qui
            })
            .Build();

        host.Run();
        ```
        NON usare WebApplication.CreateBuilder() per Azure Functions Isolated Worker.
        Il .csproj DEVE contenere <AzureFunctionsVersion>v4</AzureFunctionsVersion>.
        Pacchetti richiesti: Microsoft.Azure.Functions.Worker/2.0.0, Microsoft.Azure.Functions.Worker.Sdk/2.0.7, Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore/2.1.0.
        File host.json e local.settings.json sono OBBLIGATORI.
        """;

    private const string AzureAiAgentsHints = """
        ### Azure AI Agents (Azure.AI.Agents.Persistent) — Client CORRETTO
        ```csharp
        using Azure.AI.Agents.Persistent;
        using Azure.Identity;

        // Client corretto: PersistentAgentsClient — NON ProjectsClient, NON AIProjectClient
        var client = new PersistentAgentsClient(
            endpoint: new Uri(configuration["AzureAI:Endpoint"]!),
            credential: new DefaultAzureCredential());

        // Creazione thread
        var thread = await client.Threads.CreateThreadAsync(cancellationToken: ct);

        // Invio messaggio
        await client.Messages.CreateMessageAsync(
            threadId: thread.Value.Id,
            role: MessageRole.User,
            content: userMessage,
            cancellationToken: ct);

        // Avvio run con agente pre-esistente
        var run = await client.Runs.CreateRunAsync(
            threadId: thread.Value.Id,
            assistantId: agentId,
            cancellationToken: ct);

        // Polling fino a completamento
        do {
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
                    if (content is MessageTextContent textContent)
                        return textContent.Text;
                break;
            }
        }
        ```
        Pacchetto: Azure.AI.Agents.Persistent/1.0.0-beta.3
        """;

    private const string TelegramBotHints = """
        ### Telegram.Bot v22.x — API CORRETTA
        ```csharp
        using Telegram.Bot;
        using Telegram.Bot.Types;

        // Invio messaggio — METODO CORRETTO in v22.x:
        await botClient.SendMessage(
            chatId: chatId,
            text: "Risposta",
            cancellationToken: ct);

        // NON usare (rimosso in v22.x):
        // await botClient.SendTextMessageAsync(chatId, text); // RIMOSSO — non esiste più
        ```
        Pacchetto: Telegram.Bot/22.0.0
        """;

    private const string EntityFrameworkHints = """
        ### Entity Framework Core (.NET 8) — DbContext CORRETTO
        ```csharp
        // Primary constructor (.NET 8):
        public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
        {
            public DbSet<MyEntity> MyEntities => Set<MyEntity>();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            }
        }

        // Registrazione in Program.cs:
        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
        ```
        Pacchetti: Microsoft.EntityFrameworkCore/8.0.11, Microsoft.EntityFrameworkCore.SqlServer/8.0.11
        """;

    private const string MediatRHints = """
        ### MediatR (.NET 8) — Pattern CORRETTO
        ```csharp
        // Registrazione in Program.cs:
        builder.Services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Command con risposta:
        public record CreateItemCommand(string Name) : IRequest<int>;

        // Handler con primary constructor:
        public class CreateItemHandler(IItemRepository repo) : IRequestHandler<CreateItemCommand, int>
        {
            public async Task<int> Handle(CreateItemCommand request, CancellationToken ct)
            {
                // implementazione completa
                return await repo.AddAsync(new Item { Name = request.Name }, ct);
            }
        }
        ```
        Pacchetto: MediatR/12.4.0
        """;

    private const string MinimalApiHints = """
        ### ASP.NET Core Minimal API (.NET 8) — Program.cs CORRETTO
        ```csharp
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        // Registrare i servizi applicativi qui

        var app = builder.Build();
        if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
        app.UseHttpsRedirection();
        // Registrare gli endpoint qui
        app.Run();
        ```
        """;

    /// <summary>
    /// Analizza il contenuto dell'architettura e restituisce gli snippet API
    /// pertinenti per ridurre le allucinazioni del modello LLM sulle API dei pacchetti.
    /// Cerca keyword nel testo dell'architettura e include solo gli snippet rilevanti.
    /// </summary>
    /// <param name="architectureContent">Contenuto del documento di architettura</param>
    /// <returns>Stringa con gli snippet API rilevanti, pronta per essere iniettata nel prompt</returns>
    public static string GetApiHintsForStack(string architectureContent)
    {
        if (string.IsNullOrWhiteSpace(architectureContent))
            return string.Empty;

        var hints = new List<string>();
        var arch = architectureContent;

        // Azure Functions Isolated Worker
        if (arch.Contains("Azure Function", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("Functions Worker", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("Microsoft.Azure.Functions.Worker", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add(AzureFunctionsHints);
        }

        // Azure AI Agents / Azure AI Foundry
        if (arch.Contains("Azure.AI.Agents", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("Azure.AI.Projects", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("PersistentAgent", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("AI Foundry", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("AI Agent", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add(AzureAiAgentsHints);
        }

        // Telegram Bot
        if (arch.Contains("Telegram", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("Telegram.Bot", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add(TelegramBotHints);
        }

        // Entity Framework Core
        if (arch.Contains("Entity Framework", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("EF Core", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("DbContext", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add(EntityFrameworkHints);
        }

        // MediatR
        if (arch.Contains("MediatR", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("CQRS", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("Mediator", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add(MediatRHints);
        }

        // ASP.NET Core Minimal API (aggiungi sempre se non Azure Functions)
        var hasAzureFunctions = arch.Contains("Azure Function", StringComparison.OrdinalIgnoreCase)
            || arch.Contains("Functions Worker", StringComparison.OrdinalIgnoreCase);
        if (!hasAzureFunctions
            && (arch.Contains("Minimal API", StringComparison.OrdinalIgnoreCase)
                || arch.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase)
                || arch.Contains("WebApplication", StringComparison.OrdinalIgnoreCase)
                || arch.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)))
        {
            hints.Add(MinimalApiHints);
        }

        if (hints.Count == 0)
            return string.Empty;

        return $"""
            ## Riferimento API (snippet corretti per i pacchetti usati)
            {string.Join("\n\n", hints)}

            IMPORTANTE: Usa ESATTAMENTE i nomi di classi, metodi e pattern mostrati sopra.
            NON inventare nomi di metodi o classi che non esistono nei pacchetti.
            """;
    }

    /// <summary>
    /// Costruisce il prompt per la generazione del codice.
    /// Include requisiti, architettura, API hints pertinenti e istruzioni specifiche per C# .NET 8.
    /// </summary>
    public static string BuildCodeGenerationPrompt(
        string requirementsContent,
        string architectureContent,
        string? projectName,
        Dictionary<string, string>? metadata = null,
        string? techReferenceContent = null)
    {
        var appType = metadata?.TryGetValue("appType", out var type) == true
            ? type
            : "REST API";

        var apiHints = GetApiHintsForStack(architectureContent);
        var apiHintsSection = string.IsNullOrEmpty(apiHints)
            ? string.Empty
            : $"\n{apiHints}\n";

        var techReferenceSection = string.IsNullOrEmpty(techReferenceContent)
            ? string.Empty
            : $"""

            ## Riferimento Tecnico (OBBLIGATORIO DA SEGUIRE)
            {techReferenceContent}

            ATTENZIONE: le informazioni nel Riferimento Tecnico hanno la PRECEDENZA su qualsiasi tua conoscenza pregressa.
            Usa ESATTAMENTE le versioni NuGet, i nomi di classi e i pattern di inizializzazione documentati sopra.

            """;

        return $"""
            Genera il codice C# .NET 8 completo per il progetto "{projectName ?? "App"}".

            ## Requisiti
            {requirementsContent}

            ## Architettura Definita
            {architectureContent}
            {techReferenceSection}
            {apiHintsSection}
            ## Istruzioni per il Codice
            IMPORTANTE: Segui ESATTAMENTE la struttura definita nella sezione "Struttura del Progetto .NET"
            del documento di architettura qui sopra. NON inventare progetti o layer non previsti.

            Per ogni file:
            - Una sola classe, interfaccia o record per file
            - Codice COMPLETO e compilabile (niente placeholder, TODO o "// ... resto del codice")
            - Namespace che rispecchia il percorso del file (es. `src/MyApp.Domain/Entities/` → `MyApp.Domain.Entities`)

            ## File Obbligatori
            Genera SEMPRE i seguenti file per ogni progetto principale (API, Worker, Azure Functions):
            - `Program.cs` — entry point con configurazione DI, middleware e routing
            - `appsettings.json` — configurazione base dell'applicazione
            - `appsettings.Development.json` — override per l'ambiente di sviluppo
            (Per Azure Functions: `host.json` e `local.settings.json` al posto di appsettings)

            ## Standard di Codice
            - Usa C# 12 syntax (record types, primary constructors, collection expressions)
            - Tutti i metodi async/await
            - Nullable reference types abilitati
            - XML documentation su classi e metodi pubblici
            - Dependency injection su tutto
            - Logging con ILogger<T>

            ## Formato Output OBBLIGATORIO
            Ogni file DEVE essere preceduto da un heading Markdown con il path completo relativo alla root della solution.
            Il path DEVE iniziare con `src/` per i sorgenti o `tests/` per i test.

            Formato ESATTO da rispettare per ogni file C#:

            ### src/NomeProgetto.Layer/Cartella/NomeClasse.cs
            ```csharp
            namespace NomeProgetto.Layer.Cartella;
            // codice completo della classe...
            ```

            Formato ESATTO per file JSON di configurazione (appsettings.json, host.json, ecc.):

            ### src/NomeProgetto.API/appsettings.json
            ```json
            (contenuto JSON dell'appsettings)
            ```

            NON usare mai un heading diverso da `### path/to/File.ext` prima del blocco di codice.
            Genera TUTTI i file necessari seguendo la struttura dell'Architect.
            Inizia dal layer più interno (Domain) e procedi verso l'esterno.

            ## Checklist di Completezza (OBBLIGATORIA)
            Alla fine della generazione, verifica mentalmente:
            □ Il progetto principale ha Program.cs con configurazione DI completa
            □ Il progetto principale ha appsettings.json (o host.json per Azure Functions)
            □ Ogni interfaccia custom usata ha il suo file di definizione generato
            □ Ogni DTO/Request/Response usato ha il suo file di definizione generato
            □ Ogni enum custom usato ha il suo file di definizione generato
            □ Ogni classe base/astratta usata ha il suo file di definizione generato
            □ I namespace nei `using` corrispondono ai path dei file generati
            □ Tutti i tipi referenziati che non vengono da NuGet esterni sono definiti nel codice generato
            """;
    }
}
