namespace BmadAgentFramework.Core.Models;

/// <summary>
/// Stato completo del workflow BMAD in un dato momento.
/// Viene serializzato e deserializzato per supportare la persistenza
/// e il pattern human-in-the-loop (Azure Durable Functions).
/// </summary>
public class WorkflowState
{
    /// <summary>Identificativo univoco del workflow</summary>
    public string WorkflowId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Identificativo del progetto associato</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Timestamp di avvio del workflow</summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp di completamento (null se ancora in corso)</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Fase corrente del workflow</summary>
    public WorkflowPhase CurrentPhase { get; set; } = WorkflowPhase.Analysis;

    /// <summary>Contesto completo condiviso tra gli agenti</summary>
    public AgentContext Context { get; set; } = new();

    /// <summary>
    /// Log delle esecuzioni degli step nel workflow.
    /// Contiene i tempi di esecuzione e gli esiti di ogni agente.
    /// </summary>
    public List<StepExecutionLog> ExecutionLog { get; init; } = new();

    /// <summary>
    /// Indica se il workflow è completato (con successo o fallimento)
    /// </summary>
    public bool IsCompleted => CurrentPhase is WorkflowPhase.Completed or WorkflowPhase.Failed;

    /// <summary>
    /// Indica se il workflow è in attesa di approvazione umana
    /// </summary>
    public bool IsWaitingForApproval => CurrentPhase == WorkflowPhase.PendingApproval;

    /// <summary>
    /// Eventuali errori verificatisi durante il workflow
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Avanza il workflow alla fase successiva
    /// </summary>
    public void AdvanceToNextPhase()
    {
        CurrentPhase = CurrentPhase switch
        {
            WorkflowPhase.Analysis => WorkflowPhase.Architecture,
            WorkflowPhase.Architecture => WorkflowPhase.Development,
            WorkflowPhase.Development => WorkflowPhase.SolutionBuilding,
            WorkflowPhase.SolutionBuilding => WorkflowPhase.QualityAssurance,
            WorkflowPhase.QualityAssurance => WorkflowPhase.DevOps,
            WorkflowPhase.DevOps => WorkflowPhase.Completed,
            _ => CurrentPhase
        };

        if (CurrentPhase == WorkflowPhase.Completed)
            CompletedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Log di esecuzione di un singolo step del workflow
/// </summary>
public record StepExecutionLog
{
    public string StepName { get; init; } = string.Empty;
    public WorkflowPhase Phase { get; init; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : TimeSpan.Zero;
}
