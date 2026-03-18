using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BmadAgentFramework.Azure.Functions;

/// <summary>
/// Azure Function generica per l'esecuzione di un agente BMAD.
///
/// Questa funzione può essere triggherata da:
/// - Azure Service Bus (per architetture event-driven)
/// - HTTP (per testing e integrazione diretta)
/// - Timer (per workflow pianificati)
///
/// Ogni agente può essere deployato come Function separata
/// per scalabilità e isolamento degli errori.
/// </summary>
public class AgentFunction
{
    private readonly IAgent _agent;
    private readonly ILogger<AgentFunction> _logger;

    public AgentFunction(IAgent agent, ILogger<AgentFunction> logger)
    {
        _agent = agent;
        _logger = logger;
    }

    /// <summary>
    /// Service Bus Trigger: riceve messaggi da Azure Service Bus.
    /// Ogni agente è in ascolto sulla propria queue/topic.
    ///
    /// Architettura event-driven:
    /// Analyst → [analyst-output topic] → Architect
    /// Architect → [architect-output topic] → Developer
    /// Developer → [developer-output topic] → QA
    /// ...
    /// </summary>
    [Function("AgentServiceBusTrigger")]
    public async Task RunFromServiceBus(
        [ServiceBusTrigger("bmad-agent-queue", Connection = "ServiceBusConnection")] AgentContext context,
        FunctionContext functionContext)
    {
        _logger.LogInformation(
            "Agente {AgentName} ricevuto messaggio da Service Bus. Progetto: {ProjectId}",
            _agent.Name, context.ProjectId);

        try
        {
            if (!await _agent.CanHandleAsync(context))
            {
                _logger.LogWarning(
                    "Agente {AgentName} non può gestire il contesto corrente (fase: {Phase})",
                    _agent.Name, context.CurrentPhase);
                return;
            }

            var message = await _agent.ProcessAsync(context);

            _logger.LogInformation(
                "Agente {AgentName} ha completato per progetto {ProjectId}",
                _agent.Name, context.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Errore nell'agente {AgentName} per progetto {ProjectId}: {Error}",
                _agent.Name, context.ProjectId, ex.Message);
            throw; // Rilancia per Service Bus retry automatico
        }
    }

    /// <summary>
    /// HTTP Trigger: invoca direttamente un agente per testing.
    /// POST /api/agent/{agentName}/run
    /// </summary>
    [Function("AgentHttpTrigger")]
    public async Task<AgentMessage> RunFromHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "agent/run")] AgentContext context,
        FunctionContext functionContext)
    {
        _logger.LogInformation(
            "Agente {AgentName} invocato via HTTP. Progetto: {ProjectId}",
            _agent.Name, context.ProjectId);

        if (!await _agent.CanHandleAsync(context))
        {
            return AgentMessage.CreateError(
                _agent.Name,
                AgentRole.Orchestrator,
                $"L'agente {_agent.Name} non può gestire la fase corrente: {context.CurrentPhase}",
                context.CurrentPhase);
        }

        return await _agent.ProcessAsync(context);
    }
}
