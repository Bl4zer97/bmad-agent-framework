using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BmadAgentFramework.Agents.Analyst;

/// <summary>
/// Agente Analista del framework BMAD.
///
/// Responsabilità:
/// - Analizzare i requisiti dell'utente (spesso informali e incompleti)
/// - Estrarre le funzionalità chiave del sistema
/// - Identificare gli utenti finali e i casi d'uso
/// - Produrre un documento di requisiti strutturato (PRD - Product Requirements Document)
///
/// Output prodotto: requirements.md
/// Input necessari: stringa di requisiti dell'utente
/// </summary>
public class AnalystAgent : IAgent
{
    private const string AgentSystemPrompt = """
        Sei l'Agente Analyst del framework BMAD per soluzioni C# .NET su Azure.
        Il tuo ruolo è analizzare i requisiti del cliente e produrre un documento
        di requisiti strutturato e professionale.

        Quando ricevi una richiesta, devi:
        1. Identificare le funzionalità core del sistema
        2. Estrarre i requisiti funzionali e non funzionali
        3. Identificare gli utenti finali e i loro casi d'uso
        4. Definire i criteri di accettazione
        5. Suggerire lo stack tecnologico .NET + Azure appropriato

        Produci sempre output in formato Markdown strutturato con sezioni chiare.
        Sii preciso, tecnico e professionale. Rispondi in italiano.
        """;

    public string Name => "AnalystAgent";
    public string Role => "Business & Requirements Analyst";

    private readonly AzureOpenAIService _aiService;
    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly AgentConfiguration _config;
    private readonly ILogger<AnalystAgent> _logger;

    public AnalystAgent(
        AzureOpenAIService aiService,
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        IOptions<FrameworkOptions> options,
        ILogger<AnalystAgent> logger)
    {
        _aiService = aiService;
        _artifactStore = artifactStore;
        _memoryService = memoryService;
        _logger = logger;

        // Carica la configurazione specifica dell'agente, con fallback ai default
        _config = options.Value.Agents.TryGetValue(Name, out var cfg)
            ? cfg
            : new AgentConfiguration { AgentName = Name, Temperature = 0.5f };
    }

    /// <summary>
    /// L'Analyst può gestire qualsiasi contesto nella fase di Analysis.
    /// </summary>
    public Task<bool> CanHandleAsync(AgentContext context)
    {
        return Task.FromResult(context.CurrentPhase == WorkflowPhase.Analysis);
    }

    /// <summary>
    /// Processa i requisiti dell'utente e produce un documento di analisi strutturato.
    ///
    /// Flusso:
    /// 1. Costruisce il prompt con i requisiti dell'utente
    /// 2. Chiama Azure OpenAI con il system prompt dell'Analyst
    /// 3. Salva il risultato come artefatto "requirements"
    /// 4. Aggiorna il contesto con le informazioni estratte
    /// </summary>
    public async Task<AgentMessage> ProcessAsync(AgentContext context, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "AnalystAgent avviato per progetto {ProjectId}: {Requirements}",
            context.ProjectId, context.Requirements[..Math.Min(100, context.Requirements.Length)]);

        // Costruisce il prompt per l'analisi dei requisiti
        var prompt = RequirementsParser.BuildAnalysisPrompt(context.Requirements, context.Metadata);

        // Chiama Azure OpenAI per analizzare i requisiti
        var analysisResult = await _aiService.GetCompletionAsync(
            agentName: Name,
            systemPrompt: AgentSystemPrompt,
            userMessage: prompt,
            conversationHistory: context.ConversationHistory,
            agentConfig: _config,
            ct: ct);

        // Crea e salva l'artefatto dei requisiti
        var artifact = ProjectArtifact.CreateRequirements(analysisResult, Name);
        await _artifactStore.SaveArtifactAsync(context.ProjectId, artifact, ct);
        context.SaveArtifact(artifact);

        // Crea il messaggio di risposta dell'agente
        var message = new AgentMessage
        {
            AgentName = Name,
            Role = AgentRole.Analyst,
            Content = analysisResult,
            Phase = WorkflowPhase.Analysis,
            Metadata = new Dictionary<string, string>
            {
                ["artifactId"] = artifact.ArtifactId,
                ["artifactName"] = artifact.Name
            }
        };

        // Salva nella memoria del progetto per gli agenti successivi
        await _memoryService.SaveConversationAsync(context.ProjectId, message, ct);
        context.AddMessage(message);

        _logger.LogInformation(
            "AnalystAgent completato per progetto {ProjectId}. Artefatto: {ArtifactName}",
            context.ProjectId, artifact.Name);

        return message;
    }
}
