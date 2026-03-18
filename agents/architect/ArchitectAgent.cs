using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BmadAgentFramework.Agents.Architect;

/// <summary>
/// Agente Architetto del framework BMAD.
///
/// Responsabilità:
/// - Leggere i requisiti prodotti dall'Analyst Agent
/// - Progettare l'architettura del sistema su Azure
/// - Definire i componenti, le API, lo schema dati
/// - Scegliere i pattern architetturali appropriati (Clean Architecture, CQRS, ecc.)
/// - Produrre un documento di architettura con diagrammi e decisioni tecniche
///
/// Output prodotto: architecture.md
/// Input necessari: artefatto "requirements" dell'Analyst
/// </summary>
public class ArchitectAgent : IAgent
{
    private const string AgentSystemPrompt = """
        Sei l'Agente Architect del framework BMAD per soluzioni C# .NET su Azure.
        Il tuo ruolo è progettare architetture enterprise-grade, scalabili e manutenibili.

        Principi che segui:
        - Clean Architecture (separazione dei layer)
        - SOLID principles
        - Azure Well-Architected Framework (Reliability, Security, Cost, Performance, Operations)
        - Design patterns appropriati (Repository, CQRS, Mediator, Factory)
        - Microservizi quando appropriato, monolite modulare quando sufficiente

        Per ogni soluzione Azure definisci:
        - Quali servizi Azure usare e perché
        - Come i componenti comunicano (REST, Service Bus, Event Grid)
        - Schema dati e storage strategy
        - Security e authentication (Azure AD, Managed Identity)
        - Scalability e resilience patterns

        Produci documenti tecnici professionali in Markdown con diagrammi ASCII.
        Rispondi in italiano.
        """;

    public string Name => "ArchitectAgent";
    public string Role => "Solution Architect";

    private readonly AzureOpenAIService _aiService;
    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly AgentConfiguration _config;
    private readonly ILogger<ArchitectAgent> _logger;

    public ArchitectAgent(
        AzureOpenAIService aiService,
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        IOptions<FrameworkOptions> options,
        ILogger<ArchitectAgent> logger)
    {
        _aiService = aiService;
        _artifactStore = artifactStore;
        _memoryService = memoryService;
        _logger = logger;

        _config = options.Value.Agents.TryGetValue(Name, out var cfg)
            ? cfg
            : new AgentConfiguration { AgentName = Name, Temperature = 0.2f };
    }

    /// <summary>
    /// L'Architect può operare solo dopo che l'Analyst ha completato la sua analisi.
    /// </summary>
    public Task<bool> CanHandleAsync(AgentContext context)
    {
        var canHandle = context.CurrentPhase == WorkflowPhase.Architecture
            && context.GetArtifact("requirements") != null;
        return Task.FromResult(canHandle);
    }

    /// <summary>
    /// Progetta l'architettura basandosi sui requisiti prodotti dall'Analyst.
    ///
    /// Flusso:
    /// 1. Legge l'artefatto dei requisiti
    /// 2. Costruisce il prompt con i requisiti e il contesto
    /// 3. Chiama Azure OpenAI per progettare l'architettura
    /// 4. Salva il risultato come artefatto "architecture"
    /// </summary>
    public async Task<AgentMessage> ProcessAsync(AgentContext context, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "ArchitectAgent avviato per progetto {ProjectId}",
            context.ProjectId);

        // Legge i requisiti prodotti dall'Analyst
        var requirementsArtifact = context.GetArtifact("requirements")
            ?? throw new InvalidOperationException(
                "ArchitectAgent richiede l'artefatto 'requirements' dall'AnalystAgent");

        // Costruisce il prompt per la progettazione architetturale
        var prompt = ArchitectureDesigner.BuildArchitecturePrompt(
            requirementsArtifact.Content,
            context.ProjectName,
            context.Metadata);

        // Chiama Azure OpenAI per progettare l'architettura
        var architectureResult = await _aiService.GetCompletionAsync(
            agentName: Name,
            systemPrompt: AgentSystemPrompt,
            userMessage: prompt,
            conversationHistory: context.ConversationHistory,
            agentConfig: _config,
            ct: ct);

        // Salva l'artefatto dell'architettura
        var artifact = ProjectArtifact.CreateArchitecture(architectureResult, Name);
        await _artifactStore.SaveArtifactAsync(context.ProjectId, artifact, ct);
        context.SaveArtifact(artifact);

        var message = new AgentMessage
        {
            AgentName = Name,
            Role = AgentRole.Architect,
            Content = architectureResult,
            Phase = WorkflowPhase.Architecture,
            Metadata = new Dictionary<string, string>
            {
                ["artifactId"] = artifact.ArtifactId,
                ["artifactName"] = artifact.Name
            }
        };

        await _memoryService.SaveConversationAsync(context.ProjectId, message, ct);
        context.AddMessage(message);

        _logger.LogInformation(
            "ArchitectAgent completato per progetto {ProjectId}",
            context.ProjectId);

        return message;
    }
}
