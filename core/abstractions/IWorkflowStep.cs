using BmadAgentFramework.Core.Models;

namespace BmadAgentFramework.Core.Abstractions;

/// <summary>
/// Rappresenta un singolo step nel workflow BMAD.
/// Ogni step è associato a un agente e produce un artefatto specifico.
/// </summary>
public interface IWorkflowStep
{
    /// <summary>Nome identificativo dello step</summary>
    string StepName { get; }

    /// <summary>Ordine di esecuzione nello workflow (0=primo)</summary>
    int Order { get; }

    /// <summary>Indica se questo step può essere eseguito in parallelo con altri</summary>
    bool CanRunInParallel { get; }

    /// <summary>
    /// Esegue lo step del workflow.
    /// </summary>
    /// <param name="context">Contesto condiviso con tutti i dati del progetto</param>
    /// <param name="ct">Token di cancellazione</param>
    /// <returns>Artefatto prodotto da questo step</returns>
    Task<ProjectArtifact> ExecuteAsync(AgentContext context, CancellationToken ct = default);

    /// <summary>
    /// Verifica se le precondizioni per eseguire questo step sono soddisfatte.
    /// Esempio: il Developer step richiede che Architect abbia prodotto un artefatto.
    /// </summary>
    Task<bool> ArePreconditionsMetAsync(AgentContext context);
}
