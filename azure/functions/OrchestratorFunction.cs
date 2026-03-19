using BmadAgentFramework.Agents.Analyst;
using BmadAgentFramework.Agents.Architect;
using BmadAgentFramework.Agents.Developer;
using BmadAgentFramework.Agents.DevOps;
using BmadAgentFramework.Agents.Orchestrator;
using BmadAgentFramework.Agents.QA;
using BmadAgentFramework.Core.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace BmadAgentFramework.Azure.Functions;

/// <summary>
/// Azure Durable Functions per l'orchestrazione del workflow BMAD su Azure.
///
/// Usa il pattern Durable Functions per:
/// - Persistenza dello stato del workflow (sopravvive a restart)
/// - Fan-out/fan-in per step paralleli (es. analisi e architettura in parallelo)
/// - Human-in-the-loop con external events (approvazione umana)
/// - Retry automatico con backoff esponenziale
/// - Timer per operazioni long-running
///
/// Architettura Durable Functions:
/// HTTP Trigger (start) → Orchestrator Function → Activity Functions (agenti)
///                                              ↓
///                                    External Event (approvazione)
/// </summary>
public class OrchestratorFunction
{
    private readonly OrchestratorAgent _orchestrator;
    private readonly ILogger<OrchestratorFunction> _logger;

    public OrchestratorFunction(
        OrchestratorAgent orchestrator,
        ILogger<OrchestratorFunction> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// HTTP Trigger: avvia un nuovo workflow BMAD.
    /// POST /api/workflow/start
    /// Body: { "request": "Crea una REST API per gestire task in .NET 8" }
    /// </summary>
    [Function("StartWorkflow")]
    public async Task<string> StartWorkflow(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "workflow/start")] WorkflowStartRequest request,
        [DurableClient] DurableTaskClient client,
        FunctionContext context)
    {
        _logger.LogInformation(
            "Avvio workflow BMAD per richiesta: {Request}",
            request.Request[..Math.Min(100, request.Request.Length)]);

        // Avvia l'istanza dell'orchestratore Durable
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(BmadWorkflowOrchestrator),
            input: request.Request);

        _logger.LogInformation("Workflow avviato con instanceId: {InstanceId}", instanceId);

        // Restituisce l'URL per controllare lo stato
        return instanceId;
    }

    /// <summary>
    /// HTTP Trigger: recupera lo stato di un workflow.
    /// GET /api/workflow/{instanceId}/status
    /// </summary>
    [Function("GetWorkflowStatus")]
    public async Task<OrchestrationMetadata?> GetWorkflowStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "workflow/{instanceId}/status")] object request,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        FunctionContext context)
    {
        return await client.GetInstancesAsync(instanceId);
    }

    /// <summary>
    /// HTTP Trigger: approva una fase del workflow (human-in-the-loop).
    /// POST /api/workflow/{instanceId}/approve
    /// </summary>
    [Function("ApproveWorkflowStep")]
    public async Task ApproveWorkflowStep(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "workflow/{instanceId}/approve")] object request,
        [DurableClient] DurableTaskClient client,
        string instanceId,
        FunctionContext context)
    {
        // Invia l'evento di approvazione all'orchestratore in attesa
        await client.RaiseEventAsync(instanceId, "HumanApproval", true);
        _logger.LogInformation("Approvazione inviata per workflow {InstanceId}", instanceId);
    }
}

/// <summary>
/// Request model per l'avvio del workflow
/// </summary>
public record WorkflowStartRequest(string Request, Dictionary<string, string>? Metadata = null);

/// <summary>
/// Orchestrator Function Durable: coordina tutti gli agenti in sequenza.
///
/// Questo è il "cervello" del sistema Durable Functions.
/// Ogni chiamata a CallActivityAsync esegue un agente come Activity Function
/// separata, che può essere eseguita su un worker diverso.
/// </summary>
public class BmadWorkflowOrchestrator
{
    /// <summary>
    /// Funzione orchestratore Durable che esegue il workflow BMAD.
    ///
    /// Nota: le Orchestrator Functions devono essere deterministiche
    /// (non chiamare DateTime.Now, GUID, o operazioni I/O dirette).
    /// Usare sempre ctx.CallActivityAsync per operazioni con effetti collaterali.
    /// </summary>
    [Function(nameof(BmadWorkflowOrchestrator))]
    public async Task<WorkflowState> RunOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext ctx,
        string userRequest)
    {
        var logger = ctx.CreateReplaySafeLogger(nameof(BmadWorkflowOrchestrator));
        logger.LogInformation("Orchestrazione BMAD avviata per: {Request}",
            userRequest[..Math.Min(100, userRequest.Length)]);

        var state = new WorkflowState { ProjectId = ctx.InstanceId };

        // === FASE 1: ANALYST ===
        logger.LogInformation("Fase 1/5: Analyst Agent");
        state = await ctx.CallActivityAsync<WorkflowState>(
            nameof(RunAnalystActivity),
            (userRequest, state));

        if (state.CurrentPhase == WorkflowPhase.Failed)
        {
            logger.LogError("Workflow fallito nella fase Analyst");
            return state;
        }

        // === APPROVAZIONE UMANA (opzionale) ===
        // Attende approvazione umana con timeout di 24 ore
        // Commentare le righe seguenti per disabilitare human-in-the-loop
        /*
        logger.LogInformation("In attesa di approvazione umana per i requisiti...");
        var approved = await ctx.WaitForExternalEvent<bool>("HumanApproval",
            timeout: TimeSpan.FromHours(24));
        if (!approved) { state.CurrentPhase = WorkflowPhase.Failed; return state; }
        */

        // === FASE 2: ARCHITECT ===
        logger.LogInformation("Fase 2/5: Architect Agent");
        state = await ctx.CallActivityAsync<WorkflowState>(
            nameof(RunArchitectActivity),
            state);

        if (state.CurrentPhase == WorkflowPhase.Failed) return state;

        // === FASE 3: DEVELOPER ===
        logger.LogInformation("Fase 3/5: Developer Agent");
        state = await ctx.CallActivityAsync<WorkflowState>(
            nameof(RunDeveloperActivity),
            state);

        if (state.CurrentPhase == WorkflowPhase.Failed) return state;

        // === FASE 4: QA (può essere parallela ad altri controlli) ===
        logger.LogInformation("Fase 4/5: QA Agent");
        state = await ctx.CallActivityAsync<WorkflowState>(
            nameof(RunQAActivity),
            state);

        if (state.CurrentPhase == WorkflowPhase.Failed) return state;

        // === FASE 5: DEVOPS ===
        logger.LogInformation("Fase 5/5: DevOps Agent");
        state = await ctx.CallActivityAsync<WorkflowState>(
            nameof(RunDevOpsActivity),
            state);

        logger.LogInformation(
            "Orchestrazione completata! Fase finale: {Phase}",
            state.CurrentPhase);

        return state;
    }

    /// <summary>Activity Function per l'Analyst Agent</summary>
    [Function(nameof(RunAnalystActivity))]
    public async Task<WorkflowState> RunAnalystActivity(
        [ActivityTrigger] (string UserRequest, WorkflowState State) input,
        FunctionContext context)
    {
        // L'Activity Function invoca l'agente reale con tutti i servizi iniettati
        // In una implementazione reale, qui si recupera il servizio dall'host
        throw new NotImplementedException("Implementare con i servizi iniettati via DI");
    }

    /// <summary>Activity Function per l'Architect Agent</summary>
    [Function(nameof(RunArchitectActivity))]
    public async Task<WorkflowState> RunArchitectActivity(
        [ActivityTrigger] WorkflowState state,
        FunctionContext context)
    {
        throw new NotImplementedException("Implementare con i servizi iniettati via DI");
    }

    /// <summary>Activity Function per il Developer Agent</summary>
    [Function(nameof(RunDeveloperActivity))]
    public async Task<WorkflowState> RunDeveloperActivity(
        [ActivityTrigger] WorkflowState state,
        FunctionContext context)
    {
        throw new NotImplementedException("Implementare con i servizi iniettati via DI");
    }

    /// <summary>Activity Function per il QA Agent</summary>
    [Function(nameof(RunQAActivity))]
    public async Task<WorkflowState> RunQAActivity(
        [ActivityTrigger] WorkflowState state,
        FunctionContext context)
    {
        throw new NotImplementedException("Implementare con i servizi iniettati via DI");
    }

    /// <summary>Activity Function per il DevOps Agent</summary>
    [Function(nameof(RunDevOpsActivity))]
    public async Task<WorkflowState> RunDevOpsActivity(
        [ActivityTrigger] WorkflowState state,
        FunctionContext context)
    {
        throw new NotImplementedException("Implementare con i servizi iniettati via DI");
    }
}
