using BmadAgentFramework.Core.Models;

namespace BmadAgentFramework.Core.Abstractions;

/// <summary>
/// Interfaccia base per tutti gli agenti del framework BMAD.
/// Ogni agente specializzato (Analyst, Architect, Developer, QA, DevOps)
/// deve implementare questa interfaccia.
/// </summary>
public interface IAgent
{
    /// <summary>Nome identificativo dell'agente</summary>
    string Name { get; }

    /// <summary>Ruolo dell'agente nel ciclo BMAD</summary>
    string Role { get; }

    /// <summary>
    /// Processa il contesto corrente e produce un messaggio di risposta.
    /// Questo è il metodo principale che invoca Azure OpenAI con il prompt specifico dell'agente.
    /// </summary>
    /// <param name="context">Contesto condiviso tra tutti gli agenti, contiene requisiti e artefatti</param>
    /// <param name="ct">Token di cancellazione per operazioni asincrone</param>
    /// <returns>Messaggio prodotto dall'agente con il risultato elaborato</returns>
    Task<AgentMessage> ProcessAsync(AgentContext context, CancellationToken ct = default);

    /// <summary>
    /// Verifica se questo agente può gestire il contesto corrente.
    /// Utilizzato dall'Orchestratore per determinare il prossimo agente da eseguire.
    /// </summary>
    /// <param name="context">Contesto corrente del workflow</param>
    /// <returns>True se l'agente può gestire la fase corrente</returns>
    Task<bool> CanHandleAsync(AgentContext context);
}
