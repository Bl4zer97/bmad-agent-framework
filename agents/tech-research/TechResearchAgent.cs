using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BmadAgentFramework.Agents.TechResearch;

/// <summary>
/// Agente di ricerca tecnica del framework BMAD.
///
/// Responsabilità:
/// - Leggere il documento di architettura prodotto dall'Architect
/// - Identificare tutti i pacchetti NuGet, librerie e servizi Azure referenziati
/// - Produrre un documento di riferimento tecnico con versioni NuGet verificate,
///   snippet di inizializzazione corretti, anti-pattern da evitare e pattern DI
/// - Fornire al Developer Agent una knowledge base affidabile per generare codice corretto
///
/// Output prodotto: artefatto "tech-reference" (tech-reference.md)
/// Input necessari: artefatti "requirements" e "architecture"
/// Fase: WorkflowPhase.TechResearch (tra Architecture e Development)
/// </summary>
public class TechResearchAgent : IAgent
{
    private const string AgentSystemPrompt = """
        Sei un Tech Lead senior specializzato in .NET e Azure.
        Il tuo ruolo è verificare e documentare le API CORRETTE delle librerie scelte dall'Architect.

        COMPITO PRINCIPALE:
        Analizza il documento di architettura fornito e produci un documento di riferimento tecnico
        che il Developer Agent utilizzerà per scrivere codice corretto e compilabile.

        REGOLE FONDAMENTALI:
        - Per ogni pacchetto NuGet, fornisci la versione stable più recente conosciuta
        - Per ogni libreria, mostra il NOME ESATTO delle classi principali, i metodi chiave, e come inizializzarla
        - Se non sei sicuro di un'API, scrivi esplicitamente 'VERIFICARE MANUALMENTE' — non inventare mai
        - NON inventare classi, metodi o namespace che non esistono nei pacchetti NuGet reali
        - Produci output in formato Markdown strutturato

        ANTI-PATTERN COMUNI DA DOCUMENTARE:
        - Azure AI: NON usare ProjectsClient o AIProjectClient → usa PersistentAgentsClient (pacchetto Azure.AI.Agents.Persistent)
        - Telegram.Bot v22.x: NON usare SendTextMessageAsync → usa SendMessage
        - Azure Functions .NET 8: NON usare WebApplication.CreateBuilder() → usa HostBuilder con .ConfigureFunctionsWebApplication()
        - Azure Functions .NET 8: NON usare Microsoft.NET.Sdk.Web → usa Microsoft.NET.Sdk

        Rispondi in italiano. Il documento deve essere preciso, dettagliato e immediatamente utilizzabile.
        """;

    public string Name => "TechResearchAgent";
    public string Role => "Tech Lead / Technical Reference Verifier";

    private readonly AzureOpenAIService _aiService;
    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly AgentConfiguration _config;
    private readonly ILogger<TechResearchAgent> _logger;

    public TechResearchAgent(
        AzureOpenAIService aiService,
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        IOptions<FrameworkOptions> options,
        ILogger<TechResearchAgent> logger)
    {
        _aiService = aiService;
        _artifactStore = artifactStore;
        _memoryService = memoryService;
        _logger = logger;

        // Temperatura molto bassa per massima precisione (meno creatività, più accuratezza)
        _config = options.Value.Agents.TryGetValue(Name, out var cfg)
            ? cfg
            : new AgentConfiguration
            {
                AgentName = Name,
                Temperature = 0.1f,
                MaxTokens = 8192
            };
    }

    /// <summary>
    /// Il TechResearchAgent opera dopo Architect e prima di Developer.
    /// Richiede l'artefatto "architecture" come input.
    /// </summary>
    public Task<bool> CanHandleAsync(AgentContext context)
    {
        var canHandle = context.CurrentPhase == WorkflowPhase.TechResearch
            && context.GetArtifact("architecture") != null;
        return Task.FromResult(canHandle);
    }

    /// <summary>
    /// Analizza l'architettura e produce un documento di riferimento tecnico verificato.
    ///
    /// Flusso:
    /// 1. Legge il documento di architettura
    /// 2. Costruisce il prompt specializzato per la ricerca tecnica
    /// 3. Chiama Azure OpenAI per produrre il documento tech-reference.md
    /// 4. Salva il risultato come artefatto "tech-reference"
    /// </summary>
    public async Task<AgentMessage> ProcessAsync(AgentContext context, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "TechResearchAgent avviato per progetto {ProjectId}",
            context.ProjectId);

        var architectureArtifact = context.GetArtifact("architecture")
            ?? throw new InvalidOperationException(
                "TechResearchAgent richiede l'artefatto 'architecture' dall'ArchitectAgent");

        // Costruisce il prompt specializzato per la ricerca tecnica
        var prompt = NuGetVerifier.BuildTechResearchPrompt(
            architectureArtifact.Content,
            context.ProjectName,
            context.Metadata);

        // Genera il documento di riferimento tecnico tramite Azure OpenAI
        var techReferenceResult = await _aiService.GetCompletionAsync(
            agentName: Name,
            systemPrompt: AgentSystemPrompt,
            userMessage: prompt,
            conversationHistory: context.ConversationHistory,
            agentConfig: _config,
            ct: ct);

        // Salva l'artefatto tech-reference
        var artifact = ProjectArtifact.CreateTechReference(techReferenceResult, Name);
        await _artifactStore.SaveArtifactAsync(context.ProjectId, artifact, ct);
        context.SaveArtifact(artifact);

        var message = new AgentMessage
        {
            AgentName = Name,
            Role = AgentRole.TechResearch,
            Content = techReferenceResult,
            Phase = WorkflowPhase.TechResearch,
            Metadata = new Dictionary<string, string>
            {
                ["artifactId"] = artifact.ArtifactId,
                ["artifactName"] = artifact.Name
            }
        };

        await _memoryService.SaveConversationAsync(context.ProjectId, message, ct);
        context.AddMessage(message);

        _logger.LogInformation(
            "TechResearchAgent completato per progetto {ProjectId}. Artefatto: {ArtifactName}",
            context.ProjectId, artifact.Name);

        return message;
    }
}
