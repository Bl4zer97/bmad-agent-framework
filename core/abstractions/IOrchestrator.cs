using BmadAgentFramework.Core.Models;

namespace BmadAgentFramework.Core.Abstractions;

/// <summary>
/// Interfaccia per l'Orchestratore del framework BMAD.
/// L'orchestratore coordina l'esecuzione sequenziale degli agenti:
/// Analyst → Architect → Developer → QA → DevOps
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// Avvia un nuovo workflow completo partendo dal requisito dell'utente.
    /// Coordina tutti gli agenti in sequenza e raccoglie gli artefatti prodotti.
    /// </summary>
    /// <param name="userRequest">Descrizione del progetto da sviluppare (es. "Crea una REST API in .NET")</param>
    /// <param name="ct">Token di cancellazione</param>
    /// <returns>Stato finale del workflow con tutti gli artefatti prodotti</returns>
    Task<WorkflowState> RunWorkflowAsync(string userRequest, CancellationToken ct = default);

    /// <summary>
    /// Riprende un workflow esistente dalla fase corrente.
    /// Utile per il pattern human-in-the-loop dove un umano approva ogni fase.
    /// </summary>
    /// <param name="state">Stato del workflow da riprendere</param>
    /// <param name="ct">Token di cancellazione</param>
    /// <returns>Stato aggiornato del workflow</returns>
    Task<WorkflowState> ResumeWorkflowAsync(WorkflowState state, CancellationToken ct = default);

    /// <summary>
    /// Registra un agente nell'orchestratore per la gestione del workflow.
    /// </summary>
    /// <param name="agent">Istanza dell'agente da registrare</param>
    void RegisterAgent(IAgent agent);
}
