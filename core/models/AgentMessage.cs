namespace BmadAgentFramework.Core.Models;

/// <summary>
/// Ruolo dell'agente che ha prodotto il messaggio
/// </summary>
public enum AgentRole
{
    System,
    User,
    Analyst,
    Architect,
    Developer,
    QA,
    DevOps,
    Orchestrator,
    SolutionBuilder
}

/// <summary>
/// Messaggio prodotto da un agente nel framework BMAD.
/// Rappresenta sia l'input che l'output di ogni interazione con Azure OpenAI.
/// I messaggi vengono accumulati nel ConversationHistory del contesto,
/// permettendo ad ogni agente di vedere il lavoro degli agenti precedenti.
/// </summary>
public record AgentMessage
{
    /// <summary>Identificativo univoco del messaggio</summary>
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Timestamp del messaggio</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Nome dell'agente che ha prodotto il messaggio</summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>Ruolo dell'agente</summary>
    public AgentRole Role { get; init; }

    /// <summary>
    /// Contenuto del messaggio.
    /// Per gli agenti AI, contiene la risposta elaborata da Azure OpenAI.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Fase del workflow al momento del messaggio
    /// </summary>
    public WorkflowPhase Phase { get; init; }

    /// <summary>
    /// Metadati aggiuntivi (tokens usati, model utilizzato, latenza, ecc.)
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Indica se il messaggio rappresenta un errore
    /// </summary>
    public bool IsError { get; init; } = false;

    /// <summary>
    /// Crea un messaggio di errore
    /// </summary>
    public static AgentMessage CreateError(string agentName, AgentRole role, string errorMessage, WorkflowPhase phase) =>
        new()
        {
            AgentName = agentName,
            Role = role,
            Content = errorMessage,
            Phase = phase,
            IsError = true
        };
}
