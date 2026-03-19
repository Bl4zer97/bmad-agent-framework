using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;

namespace BmadAgentFramework.Agents.Orchestrator;

/// <summary>
/// Orchestratore principale del framework BMAD.
///
/// È il coordinatore centrale che:
/// 1. Riceve l'input dell'utente (requisiti in linguaggio naturale)
/// 2. Crea il contesto condiviso (AgentContext)
/// 3. Esegue gli agenti in sequenza: Analyst → Architect → Developer → QA → DevOps
/// 4. Passa il contesto da un agente all'altro (ogni agente vede il lavoro del precedente)
/// 5. Raccoglie tutti gli artefatti prodotti
/// 6. Gestisce errori e retry
///
/// Pattern implementato: Chain of Responsibility + Strategy
/// In Azure: viene eseguito come Azure Durable Functions (vedi OrchestratorFunction.cs)
/// </summary>
public class OrchestratorAgent : IOrchestrator
{
    // Lista ordinata degli agenti registrati
    private readonly List<IAgent> _agents = new();
    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly ILogger<OrchestratorAgent> _logger;
    private readonly WorkflowEngine _workflowEngine;

    public OrchestratorAgent(
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        ILogger<OrchestratorAgent> logger,
        WorkflowEngine workflowEngine)
    {
        _artifactStore = artifactStore;
        _memoryService = memoryService;
        _logger = logger;
        _workflowEngine = workflowEngine;
    }

    /// <summary>
    /// Registra un agente nell'orchestratore.
    /// Gli agenti vengono eseguiti nell'ordine in cui vengono registrati.
    /// </summary>
    public void RegisterAgent(IAgent agent)
    {
        _agents.Add(agent);
        _logger.LogInformation("Agente registrato: {AgentName} ({Role})", agent.Name, agent.Role);
    }

    /// <summary>
    /// Avvia un nuovo workflow BMAD completo.
    ///
    /// Flusso completo:
    /// Input utente → [Analyst] → requirements.md
    ///             → [Architect] → architecture.md
    ///             → [Developer] → codice .cs
    ///             → [QA] → test suite
    ///             → [DevOps] → pipeline + bicep
    ///             → Output finale con tutti gli artefatti
    ///
    /// Ogni agente riceve il contesto completo con gli artefatti
    /// prodotti dagli agenti precedenti.
    /// </summary>
    /// <param name="userRequest">Descrizione del progetto (linguaggio naturale)</param>
    /// <param name="ct">Token di cancellazione</param>
    /// <returns>Stato finale del workflow con tutti gli artefatti</returns>
    public async Task<WorkflowState> RunWorkflowAsync(
        string userRequest,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "=== BMAD Workflow avviato ===\nRichiesta: {Request}",
            userRequest[..Math.Min(200, userRequest.Length)]);

        // Inizializza il contesto condiviso tra tutti gli agenti
        var context = new AgentContext
        {
            Requirements = userRequest,
            Metadata = new Dictionary<string, string>
            {
                ["techStack"] = "C# .NET 8, Azure",
                ["appType"] = "REST API"
            }
        };

        // Crea lo stato del workflow
        var state = new WorkflowState
        {
            ProjectId = context.ProjectId,
            Context = context
        };

        // Salva il contesto iniziale in memoria
        await _memoryService.SaveContextAsync(context.ProjectId, context, ct);

        // Esegui il workflow attraverso tutti gli agenti
        return await _workflowEngine.ExecuteWorkflowAsync(state, _agents, ct);
    }

    /// <summary>
    /// Riprende un workflow interrotto dalla fase corrente.
    /// Utile per il pattern human-in-the-loop.
    /// </summary>
    public async Task<WorkflowState> ResumeWorkflowAsync(
        WorkflowState state,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "=== Ripresa workflow {WorkflowId} dalla fase {Phase} ===",
            state.WorkflowId, state.CurrentPhase);

        return await _workflowEngine.ExecuteWorkflowAsync(state, _agents, ct);
    }
}
