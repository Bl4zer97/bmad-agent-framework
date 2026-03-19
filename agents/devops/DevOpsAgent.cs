using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BmadAgentFramework.Agents.DevOps;

/// <summary>
/// Agente DevOps del framework BMAD.
///
/// Responsabilità:
/// - Generare la pipeline CI/CD con GitHub Actions
/// - Creare l'infrastruttura Azure con Bicep (IaC)
/// - Configurare il deployment automatico
/// - Definire le strategie di monitoring e alerting
/// - Produrre documentazione operativa
///
/// Output prodotto:
/// - ci.yml (GitHub Actions pipeline)
/// - main.bicep (infrastruttura Azure)
/// - deploy.md (documentazione deployment)
///
/// Input necessari: artefatti "requirements", "architecture", "code", "tests"
/// </summary>
public class DevOpsAgent : IAgent
{
    private const string AgentSystemPrompt = """
        Sei l'Agente DevOps del framework BMAD per soluzioni C# .NET 8 su Azure.
        Il tuo ruolo è automatizzare il ciclo di vita del software dall'integrazione al deployment.

        Tecnologie che usi:
        - GitHub Actions: CI/CD pipeline
        - Azure Bicep: Infrastructure as Code
        - Docker: containerizzazione
        - Azure Container Registry / Azure Container Apps
        - Azure App Service / Azure Functions
        - Application Insights: monitoring
        - Azure Key Vault: gestione segreti

        Best practice che segui:
        - GitOps workflow
        - Infrastructure as Code (immutable infrastructure)
        - Blue-Green o Canary deployments
        - Secrets mai in chiaro (usa Azure Key Vault + Managed Identity)
        - Environment separation (dev, staging, prod)
        - Automated rollback on failure

        Per GitHub Actions:
        - Build, test, scan (security), deploy
        - Matrix builds per test su più OS/framework versions
        - Artifact caching per velocità
        - Environment approvals per prod

        Produci configurazioni YAML e Bicep complete e funzionanti.
        Commenta in italiano.
        """;

    public string Name => "DevOpsAgent";
    public string Role => "DevOps & Cloud Engineer";

    private readonly AzureOpenAIService _aiService;
    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly AgentConfiguration _config;
    private readonly ILogger<DevOpsAgent> _logger;

    public DevOpsAgent(
        AzureOpenAIService aiService,
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        IOptions<FrameworkOptions> options,
        ILogger<DevOpsAgent> logger)
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
                Temperature = 0.1f,   // Molto deterministico per IaC e pipeline
                MaxTokens = 8192
            };
    }

    public Task<bool> CanHandleAsync(AgentContext context)
    {
        var canHandle = context.CurrentPhase == WorkflowPhase.DevOps
            && context.GetArtifact("architecture") != null;
        return Task.FromResult(canHandle);
    }

    /// <summary>
    /// Genera la pipeline CI/CD e l'infrastruttura Azure.
    /// </summary>
    public async Task<AgentMessage> ProcessAsync(AgentContext context, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "DevOpsAgent avviato per progetto {ProjectId}",
            context.ProjectId);

        var architectureArtifact = context.GetArtifact("architecture")
            ?? throw new InvalidOperationException("Manca artefatto 'architecture'");
        var requirementsArtifact = context.GetArtifact("requirements");

        var prompt = PipelineGenerator.BuildPipelinePrompt(
            requirementsArtifact?.Content ?? string.Empty,
            architectureArtifact.Content,
            context.ProjectName,
            context.Metadata);

        var pipelineResult = await _aiService.GetCompletionAsync(
            agentName: Name,
            systemPrompt: AgentSystemPrompt,
            userMessage: prompt,
            conversationHistory: context.ConversationHistory,
            agentConfig: _config,
            ct: ct);

        // Salva la pipeline come artefatto
        var pipelineArtifact = new ProjectArtifact
        {
            Name = "pipeline-and-infrastructure.md",
            ArtifactType = "pipeline",
            ProducedBy = Name,
            Content = pipelineResult,
            ContentFormat = "markdown"
        };

        await _artifactStore.SaveArtifactAsync(context.ProjectId, pipelineArtifact, ct);
        context.SaveArtifact(pipelineArtifact);

        var message = new AgentMessage
        {
            AgentName = Name,
            Role = AgentRole.DevOps,
            Content = pipelineResult,
            Phase = WorkflowPhase.DevOps,
            Metadata = new Dictionary<string, string>
            {
                ["artifactId"] = pipelineArtifact.ArtifactId,
                ["artifactName"] = pipelineArtifact.Name
            }
        };

        await _memoryService.SaveConversationAsync(context.ProjectId, message, ct);
        context.AddMessage(message);

        _logger.LogInformation(
            "DevOpsAgent completato per progetto {ProjectId}",
            context.ProjectId);

        return message;
    }
}
