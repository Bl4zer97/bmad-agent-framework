using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;

namespace BmadAgentFramework.Agents.Orchestrator;

/// <summary>
/// Motore di orchestrazione del workflow BMAD.
///
/// Gestisce l'esecuzione sequenziale degli agenti, inclusi:
/// - Verifica delle precondizioni per ogni agente
/// - Gestione degli errori con retry
/// - Logging dettagliato dell'esecuzione
/// - Aggiornamento dello stato del workflow
/// - Supporto per il pattern human-in-the-loop
///
/// Il WorkflowEngine è separato dall'OrchestratorAgent per rispettare
/// il principio Single Responsibility e facilitare il testing.
/// </summary>
public class WorkflowEngine
{
    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly ILogger<WorkflowEngine> _logger;

    // Mappa fase → agente per trovare l'agente corretto per ogni fase
    private static readonly Dictionary<WorkflowPhase, AgentRole> PhaseToRole = new()
    {
        [WorkflowPhase.Analysis] = AgentRole.Analyst,
        [WorkflowPhase.Architecture] = AgentRole.Architect,
        [WorkflowPhase.Development] = AgentRole.Developer,
        [WorkflowPhase.QualityAssurance] = AgentRole.QA,
        [WorkflowPhase.DevOps] = AgentRole.DevOps
    };

    public WorkflowEngine(
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        ILogger<WorkflowEngine> logger)
    {
        _artifactStore = artifactStore;
        _memoryService = memoryService;
        _logger = logger;
    }

    /// <summary>
    /// Esegue il workflow completo attraverso tutti gli agenti registrati.
    ///
    /// Per ogni fase del workflow:
    /// 1. Trova l'agente appropriato per la fase corrente
    /// 2. Verifica che l'agente possa gestire il contesto
    /// 3. Esegue l'agente e raccoglie l'output
    /// 4. Avanza alla fase successiva
    /// 5. Salva lo stato aggiornato
    /// </summary>
    public async Task<WorkflowState> ExecuteWorkflowAsync(
        WorkflowState state,
        IReadOnlyList<IAgent> agents,
        CancellationToken ct = default)
    {
        var phases = new[]
        {
            WorkflowPhase.Analysis,
            WorkflowPhase.Architecture,
            WorkflowPhase.Development,
            WorkflowPhase.QualityAssurance,
            WorkflowPhase.DevOps
        };

        // Esegui solo le fasi dalla fase corrente in avanti
        var remainingPhases = phases
            .Where(p => p >= state.CurrentPhase)
            .ToArray();

        foreach (var phase in remainingPhases)
        {
            if (ct.IsCancellationRequested) break;

            state.Context.CurrentPhase = phase;
            var stepLog = new StepExecutionLog
            {
                StepName = phase.ToString(),
                Phase = phase
            };
            state.ExecutionLog.Add(stepLog);

            _logger.LogInformation(
                "=== Fase {Phase} iniziata per progetto {ProjectId} ===",
                phase, state.ProjectId);

            // Trova l'agente per questa fase
            var agent = await FindAgentForPhaseAsync(agents, state.Context);

            if (agent == null)
            {
                _logger.LogWarning(
                    "Nessun agente trovato per la fase {Phase}. Salto la fase.",
                    phase);
                stepLog.Success = true;
                stepLog.CompletedAt = DateTimeOffset.UtcNow;
                state.AdvanceToNextPhase();
                continue;
            }

            // Esegui l'agente con gestione degli errori
            try
            {
                var message = await ExecuteAgentWithRetryAsync(agent, state.Context, ct);

                stepLog.Success = !message.IsError;
                stepLog.CompletedAt = DateTimeOffset.UtcNow;

                if (message.IsError)
                {
                    _logger.LogError(
                        "Agente {AgentName} ha fallito nella fase {Phase}: {Error}",
                        agent.Name, phase, message.Content);

                    state.ErrorMessage = message.Content;
                    state.CurrentPhase = WorkflowPhase.Failed;
                    return state;
                }

                _logger.LogInformation(
                    "=== Fase {Phase} completata ({Duration}ms) ===",
                    phase, stepLog.Duration.TotalMilliseconds);

                // Aggiorna il nome del progetto se siamo nella fase di analisi
                if (phase == WorkflowPhase.Analysis)
                {
                    UpdateProjectMetadata(state.Context);
                }

                // Avanza alla fase successiva
                state.AdvanceToNextPhase();

                // Salva lo stato aggiornato in memoria
                await _memoryService.SaveContextAsync(state.ProjectId, state.Context, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Workflow cancellato durante la fase {Phase}", phase);
                state.CurrentPhase = WorkflowPhase.Failed;
                state.ErrorMessage = "Workflow cancellato dall'utente";
                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Errore non gestito nella fase {Phase}: {Error}",
                    phase, ex.Message);

                stepLog.Success = false;
                stepLog.ErrorMessage = ex.Message;
                stepLog.CompletedAt = DateTimeOffset.UtcNow;

                state.CurrentPhase = WorkflowPhase.Failed;
                state.ErrorMessage = ex.Message;
                return state;
            }
        }

        // Log finale con riepilogo artefatti prodotti
        LogWorkflowSummary(state);

        return state;
    }

    /// <summary>
    /// Trova l'agente appropriato per la fase corrente del workflow.
    /// </summary>
    private static async Task<IAgent?> FindAgentForPhaseAsync(
        IReadOnlyList<IAgent> agents,
        AgentContext context)
    {
        foreach (var agent in agents)
        {
            if (await agent.CanHandleAsync(context))
                return agent;
        }
        return null;
    }

    /// <summary>
    /// Esegue un agente con retry in caso di errori transitori.
    /// Massimo 2 tentativi con delay esponenziale.
    /// </summary>
    private async Task<AgentMessage> ExecuteAgentWithRetryAsync(
        IAgent agent,
        AgentContext context,
        CancellationToken ct,
        int maxAttempts = 2)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await agent.ProcessAsync(context, ct);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "Agente {AgentName} tentativo {Attempt}/{Max} fallito: {Error}. Riprovo...",
                    agent.Name, attempt, maxAttempts, ex.Message);

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
            }
        }

        // Ultimo tentativo - lascia propagare l'eccezione
        return await agent.ProcessAsync(context, ct);
    }

    /// <summary>
    /// Aggiorna i metadati del progetto dopo la fase di analisi.
    /// </summary>
    private static void UpdateProjectMetadata(AgentContext context)
    {
        var requirementsArtifact = context.GetArtifact("requirements");
        if (requirementsArtifact != null && string.IsNullOrEmpty(context.ProjectName))
        {
            // Estrai il nome del progetto dai requisiti analizzati
            var lines = requirementsArtifact.Content.Split('\n');
            var titleLine = lines.FirstOrDefault(l => l.StartsWith("# ") && !l.StartsWith("## "));
            if (titleLine != null)
            {
                context.ProjectName = titleLine[2..].Trim()
                    .Replace("Product Requirements Document", "")
                    .Replace("(PRD)", "")
                    .Trim();
            }

            if (string.IsNullOrEmpty(context.ProjectName))
                context.ProjectName = "BmadProject";
        }
    }

    /// <summary>
    /// Logga il riepilogo del workflow completato.
    /// </summary>
    private void LogWorkflowSummary(WorkflowState state)
    {
        var artifacts = state.Context.Artifacts;
        _logger.LogInformation(
            """
            === WORKFLOW COMPLETATO ===
            Progetto: {ProjectName}
            ProjectId: {ProjectId}
            Fase finale: {Phase}
            Artefatti prodotti: {ArtifactCount}
            {ArtifactList}
            Durata totale: {Duration}
            """,
            state.Context.ProjectName,
            state.ProjectId,
            state.CurrentPhase,
            artifacts.Count,
            string.Join(", ", artifacts.Keys),
            state.ExecutionLog.Any()
                ? state.ExecutionLog.Sum(l => l.Duration.TotalSeconds).ToString("F1") + "s"
                : "N/A");
    }
}
