using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BmadAgentFramework.Agents.QA;

/// <summary>
/// Agente Quality Assurance del framework BMAD.
///
/// Responsabilità:
/// - Analizzare il codice prodotto dal Developer
/// - Generare unit test con xUnit e FluentAssertions
/// - Generare integration test
/// - Identificare potenziali bug e vulnerabilità
/// - Produrre un test plan e un report di qualità
///
/// Output prodotto: artefatti di test (.cs) e test report (markdown)
/// Input necessari: artefatti "requirements", "architecture", "code"
/// </summary>
public class QAAgent : IAgent
{
    private const string AgentSystemPrompt = """
        Sei l'Agente QA del framework BMAD per soluzioni C# .NET 8 su Azure.
        Il tuo ruolo è garantire la qualità del codice attraverso test completi.

        Metodologia di testing:
        - Test Pyramid: molti unit test, meno integration test, pochi E2E test
        - AAA Pattern: Arrange, Act, Assert
        - Test coverage target: >80% per business logic
        - Mutation testing awareness

        Strumenti che usi:
        - xUnit: framework di test principale
        - FluentAssertions: assertions leggibili
        - Moq: mocking dependencies
        - WebApplicationFactory: integration test per API
        - Bogus/AutoFixture: generazione dati di test

        Per ogni test:
        - Nome descrittivo: Should_[ExpectedBehavior]_When_[Condition]
        - Test isolati e indipendenti
        - Test sia i happy path che gli error path
        - Verifica dei boundary values

        Produci anche:
        - Test plan documento
        - Lista dei casi di test
        - Identificazione dei rischi di qualità

        Rispondi in italiano per commenti e documentazione.
        """;

    public string Name => "QAAgent";
    public string Role => "QA Engineer & Test Automation";

    private readonly AzureOpenAIService _aiService;
    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly AgentConfiguration _config;
    private readonly ILogger<QAAgent> _logger;

    public QAAgent(
        AzureOpenAIService aiService,
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        IOptions<FrameworkOptions> options,
        ILogger<QAAgent> logger)
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
                Temperature = 0.2f,
                MaxTokens = 8192
            };
    }

    public Task<bool> CanHandleAsync(AgentContext context)
    {
        var canHandle = context.CurrentPhase == WorkflowPhase.QualityAssurance
            && context.GetArtifact("code") != null;
        return Task.FromResult(canHandle);
    }

    /// <summary>
    /// Genera i test per il codice prodotto dal Developer Agent.
    /// </summary>
    public async Task<AgentMessage> ProcessAsync(AgentContext context, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "QAAgent avviato per progetto {ProjectId}",
            context.ProjectId);

        var requirementsArtifact = context.GetArtifact("requirements");
        var codeArtifact = context.GetArtifact("code")
            ?? throw new InvalidOperationException("Manca artefatto 'code'");

        var prompt = TestGenerator.BuildTestGenerationPrompt(
            requirementsArtifact?.Content ?? string.Empty,
            codeArtifact.Content,
            context.ProjectName);

        var testResult = await _aiService.GetCompletionAsync(
            agentName: Name,
            systemPrompt: AgentSystemPrompt,
            userMessage: prompt,
            conversationHistory: context.ConversationHistory,
            agentConfig: _config,
            ct: ct);

        // Salva i test come artefatto
        var testArtifact = new ProjectArtifact
        {
            Name = $"{context.ProjectName?.Replace(" ", "") ?? "App"}Tests.cs",
            ArtifactType = "tests",
            ProducedBy = Name,
            Content = testResult,
            ContentFormat = "csharp"
        };

        await _artifactStore.SaveArtifactAsync(context.ProjectId, testArtifact, ct);
        context.SaveArtifact(testArtifact);

        var message = new AgentMessage
        {
            AgentName = Name,
            Role = AgentRole.QA,
            Content = testResult,
            Phase = WorkflowPhase.QualityAssurance,
            Metadata = new Dictionary<string, string>
            {
                ["artifactId"] = testArtifact.ArtifactId,
                ["testFileName"] = testArtifact.Name
            }
        };

        await _memoryService.SaveConversationAsync(context.ProjectId, message, ct);
        context.AddMessage(message);

        _logger.LogInformation(
            "QAAgent completato per progetto {ProjectId}",
            context.ProjectId);

        return message;
    }
}
