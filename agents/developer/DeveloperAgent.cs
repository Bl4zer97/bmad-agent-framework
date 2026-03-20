using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BmadAgentFramework.Agents.Developer;

/// <summary>
/// Agente Sviluppatore del framework BMAD.
///
/// Responsabilità:
/// - Leggere il documento di architettura dell'Architect
/// - Generare codice C# .NET 8 di alta qualità
/// - Implementare le entità, i repository, i servizi e i controller
/// - Seguire le best practice: SOLID, Clean Architecture, async/await
/// - Produrre codice commentato e documentato
///
/// Output prodotto: artefatti di codice (.cs files)
/// Input necessari: artefatti "requirements" e "architecture"
/// </summary>
public class DeveloperAgent : IAgent
{
    private const string AgentSystemPrompt = """
        Sei l'Agente Developer del framework BMAD per soluzioni C# .NET 8 su Azure.
        Scrivi codice C# 12 di alta qualità, moderno e ben strutturato.

        REGOLA RIFERIMENTO TECNICO (CRITICA):
        - Se ricevi un documento di riferimento tecnico, DEVI usare ESATTAMENTE le versioni NuGet indicate
        - DEVI usare ESATTAMENTE i nomi di classi e metodi documentati nel riferimento tecnico
        - NON inventare API: se un'API non è nel riferimento tecnico e non sei SICURO al 100% che esista, non usarla
        - Segui gli snippet di inizializzazione e DI registration del riferimento tecnico alla lettera
        - Se il riferimento tecnico dice "VERIFICARE MANUALMENTE", ometti quella funzionalità o usa un placeholder esplicito

        REGOLA FONDAMENTALE — STRUTTURA DEL PROGETTO:
        - Segui ESATTAMENTE la struttura definita dall'Architect Agent nel documento di architettura (sezione "Struttura del Progetto .NET")
        - NON inventare progetti, layer o namespace non previsti dall'architettura
        - Ogni file DEVE avere il path completo relativo alla root della solution come heading Markdown
        - Il path DEVE iniziare con `src/` per i progetti sorgente, `tests/` per i test
        - L'Architect è SENIOR sulla struttura: le sue decisioni sono legge

        REGOLA ORDINE DI GENERAZIONE PER LAYER (OBBLIGATORIA):
        Prima di scrivere qualsiasi codice, leggi il blocco `solution-structure` dall'architettura
        ed elenca tutti i progetti che devi generare. Poi genera i file in questo ordine:
        1. Domain        → Entities, ValueObjects, Domain Events, Interfacce di dominio
        2. Application   → DTOs, Commands/Queries, Handlers, Interfacce di servizi
        3. Infrastructure → Implementazioni di repository, client esterni, DbContext
        4. API / Worker / Azure Functions → Program.cs (PRIMA), poi Controller/Function, poi Middleware
        5. Tests         → Unit test e Integration test
        Questo ordine garantisce che i tipi siano sempre disponibili quando vengono referenziati.

        REGOLA DI COMPLETEZZA (CRITICA):
        - OGNI tipo custom che usi (interfaccia, classe, record, enum) DEVE essere definito in uno dei file che generi
        - Per ogni `using NomeProgetto.X.Y;` che scrivi, DEVE esistere un file in `src/NomeProgetto.X/Y/` che definisce i tipi usati
        - Prima di terminare, verifica mentalmente che per ogni tipo referenziato esista il file corrispondente
        - Se un tipo viene da un pacchetto NuGet esterno (es. ILogger, IMediator, ITelegramBotClient), NON serve definirlo
        - Se un tipo è custom del progetto (es. ITodoRepository, CreateTodoRequest, IConversationCache), DEVI generare il file
        - Meglio generare MENO file ma TUTTI COMPLETI piuttosto che tanti file con tipi mancanti

        REGOLA API ESATTE — NON INVENTARE METODI O CLASSI:
        - Usa SOLO metodi, classi e namespace che esistono realmente nei pacchetti NuGet specificati
        - Azure AI Foundry: usa `PersistentAgentsClient` da `Azure.AI.Agents.Persistent` — NON `ProjectsClient`, NON `AIProjectClient`
        - Telegram.Bot v22.x: il metodo è `SendMessage(chatId, text)` — NON `SendTextMessageAsync` (rimosso in v22)
        - Azure Functions .NET 8: Program.cs usa `HostBuilder` con `.ConfigureFunctionsWebApplication()` — NON `WebApplication.CreateBuilder()`
        - Se gli API hints nel prompt mostrano come usare un SDK, seguili alla lettera

        REGOLA ENTRY POINT (CRITICA):
        - Il progetto principale (API, Web, Worker, Azure Functions) DEVE avere un file Program.cs
        - Program.cs DEVE configurare: DI container, middleware, routing, logging
        - Program.cs DEVE registrare tutti i servizi definiti nei layer Application e Infrastructure
        - Per Azure Functions Worker .NET 8: usa HostBuilder con .ConfigureFunctionsWebApplication()
        - Genera sempre Program.cs come PRIMO file del progetto principale

        REGOLA FILE DI CONFIGURAZIONE:
        - Per progetti API: genera `appsettings.json` e `appsettings.Development.json`
        - Per Azure Functions: genera `host.json` e `local.settings.json`
        - Per Worker Services: genera `appsettings.json`
        - Usa heading Markdown `### src/NomeProgetto.API/appsettings.json` seguiti da blocco ```json

        CHECKLIST DI VALIDAZIONE FINALE (esegui prima di dichiarare il codice completo):
        □ Ogni tipo custom usato è definito nel codice generato?
        □ Ogni `using` ha un corrispondente tipo nel codice o in un pacchetto NuGet noto?
        □ Il namespace corrisponde al path del file per ogni file generato?
        □ Nessun placeholder, TODO o codice incompleto?
        □ Program.cs è presente e completo con tutti i servizi registrati?
        □ I file di configurazione JSON sono presenti?
        □ Tutti i metodi usano API reali (nessun metodo inventato)?

        Principi di codifica che segui:
        - C# 12 features: record types, primary constructors, collection expressions
        - Async/await ovunque per operazioni I/O
        - Dependency Injection via IServiceCollection
        - Repository pattern con interfacce
        - CQRS con MediatR quando appropriato
        - Global error handling con middleware
        - FluentValidation per validazione input
        - Logging strutturato con ILogger<T>
        - XML documentation comments per tutte le classi pubbliche
        - Unit testable code (no static dependencies)

        Per le API REST:
        - Minimal API o Controller-based (scegli in base alla complessità)
        - OpenAPI/Swagger documentation
        - Proper HTTP status codes
        - DTOs separati dai domain entities
        - Pagination per liste

        Produci sempre codice completo, compilabile e funzionante.
        Aggiungi commenti in italiano per spiegare le scelte tecniche.
        """;

    public string Name => "DeveloperAgent";
    public string Role => "Senior .NET Developer";

    private readonly AzureOpenAIService _aiService;
    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly AgentConfiguration _config;
    private readonly ILogger<DeveloperAgent> _logger;

    public DeveloperAgent(
        AzureOpenAIService aiService,
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        IOptions<FrameworkOptions> options,
        ILogger<DeveloperAgent> logger)
    {
        _aiService = aiService;
        _artifactStore = artifactStore;
        _memoryService = memoryService;
        _logger = logger;

        _config = options.Value.Agents.TryGetValue(Name, out var cfg)
            ? cfg
            : new AgentConfiguration
            {
                AgentName = Name,
                Temperature = 0.2f,   // Bassa temperatura per codice più deterministico
                MaxTokens = 16384     // Codice può essere lungo: generazione intera solution
            };
    }

    /// <summary>
    /// Il Developer opera dopo che Analyst e Architect hanno completato.
    /// </summary>
    public Task<bool> CanHandleAsync(AgentContext context)
    {
        var canHandle = context.CurrentPhase == WorkflowPhase.Development
            && context.GetArtifact("requirements") != null
            && context.GetArtifact("architecture") != null;
        return Task.FromResult(canHandle);
    }

    /// <summary>
    /// Genera il codice C# basandosi su requisiti e architettura.
    ///
    /// Flusso:
    /// 1. Legge i requisiti e l'architettura
    /// 2. Genera il codice principale dell'applicazione
    /// 3. Salva il codice come artefatti separati
    /// </summary>
    public async Task<AgentMessage> ProcessAsync(AgentContext context, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "DeveloperAgent avviato per progetto {ProjectId}",
            context.ProjectId);

        var requirementsArtifact = context.GetArtifact("requirements")
            ?? throw new InvalidOperationException("Manca artefatto 'requirements'");
        var architectureArtifact = context.GetArtifact("architecture")
            ?? throw new InvalidOperationException("Manca artefatto 'architecture'");

        // Legge il riferimento tecnico prodotto dal TechResearchAgent (opzionale)
        var techReferenceArtifact = context.GetArtifact("tech-reference");
        if (techReferenceArtifact != null)
        {
            _logger.LogInformation(
                "DeveloperAgent: riferimento tecnico trovato — verrà iniettato nel prompt");
        }

        // Costruisce il prompt con tutto il contesto
        var prompt = CodeGenerator.BuildCodeGenerationPrompt(
            requirementsArtifact.Content,
            architectureArtifact.Content,
            context.ProjectName,
            context.Metadata,
            techReferenceArtifact?.Content);

        // Genera il codice tramite Azure OpenAI
        var codeResult = await _aiService.GetCompletionAsync(
            agentName: Name,
            systemPrompt: AgentSystemPrompt,
            userMessage: prompt,
            conversationHistory: context.ConversationHistory,
            agentConfig: _config,
            ct: ct);

        // Salva il codice generato come artefatto
        var fileName = $"{context.ProjectName?.Replace(" ", "") ?? "GeneratedApp"}.cs";
        var artifact = ProjectArtifact.CreateSourceCode(codeResult, fileName, Name);
        await _artifactStore.SaveArtifactAsync(context.ProjectId, artifact, ct);
        context.SaveArtifact(artifact);

        var message = new AgentMessage
        {
            AgentName = Name,
            Role = AgentRole.Developer,
            Content = codeResult,
            Phase = WorkflowPhase.Development,
            Metadata = new Dictionary<string, string>
            {
                ["artifactId"] = artifact.ArtifactId,
                ["fileName"] = fileName
            }
        };

        await _memoryService.SaveConversationAsync(context.ProjectId, message, ct);
        context.AddMessage(message);

        _logger.LogInformation(
            "DeveloperAgent completato per progetto {ProjectId}. File: {FileName}",
            context.ProjectId, fileName);

        return message;
    }
}
