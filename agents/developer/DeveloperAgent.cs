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

        REGOLA FONDAMENTALE — STRUTTURA DEL PROGETTO:
        - Segui ESATTAMENTE la struttura definita dall'Architect Agent nel documento di architettura (sezione "Struttura del Progetto .NET")
        - NON inventare progetti, layer o namespace non previsti dall'architettura
        - Ogni file DEVE avere il path completo relativo alla root della solution come heading Markdown
        - Il path DEVE iniziare con `src/` per i progetti sorgente, `tests/` per i test
        - L'Architect è SENIOR sulla struttura: le sue decisioni sono legge

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

        // Costruisce il prompt con tutto il contesto
        var prompt = CodeGenerator.BuildCodeGenerationPrompt(
            requirementsArtifact.Content,
            architectureArtifact.Content,
            context.ProjectName,
            context.Metadata);

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
