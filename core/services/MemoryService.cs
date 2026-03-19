using BmadAgentFramework.Core.Models;
using Microsoft.Extensions.Logging;

namespace BmadAgentFramework.Core.Services;

/// <summary>
/// Interfaccia per il servizio di memoria degli agenti.
/// Permette di avere diverse implementazioni: in-memory (sviluppo) e Azure Cosmos DB (produzione).
/// </summary>
public interface IMemoryService
{
    Task SaveConversationAsync(string projectId, AgentMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<AgentMessage>> GetConversationHistoryAsync(string projectId, CancellationToken ct = default);
    Task SaveContextAsync(string projectId, AgentContext context, CancellationToken ct = default);
    Task<AgentContext?> LoadContextAsync(string projectId, CancellationToken ct = default);
    Task ClearProjectDataAsync(string projectId, CancellationToken ct = default);
}

/// <summary>
/// Implementazione in-memory del servizio di memoria degli agenti.
/// Usata per sviluppo locale e demo.
///
/// In produzione, sostituire con CosmosDbMemoryService che usa Azure Cosmos DB
/// per la persistenza distribuita della memoria tra le istanze delle Azure Functions.
///
/// La memoria è fondamentale nel pattern BMAD perché:
/// - Ogni agente deve "ricordare" cosa hanno prodotto gli agenti precedenti
/// - Il contesto accumulato è ciò che permette agli agenti di essere coerenti
/// - Supporta il resume del workflow in caso di interruzione
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly Dictionary<string, List<AgentMessage>> _conversations = new();
    private readonly Dictionary<string, AgentContext> _contexts = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(ILogger<MemoryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Salva un messaggio nella cronologia del progetto.
    /// In produzione: persiste su Azure Cosmos DB per durabilità.
    /// </summary>
    public async Task SaveConversationAsync(
        string projectId,
        AgentMessage message,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_conversations.ContainsKey(projectId))
                _conversations[projectId] = new List<AgentMessage>();

            _conversations[projectId].Add(message);
            _logger.LogDebug(
                "Messaggio salvato per progetto {ProjectId}: agente {Agent}",
                projectId, message.AgentName);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Recupera tutta la cronologia di conversazione di un progetto.
    /// </summary>
    public async Task<IReadOnlyList<AgentMessage>> GetConversationHistoryAsync(
        string projectId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _conversations.TryGetValue(projectId, out var history)
                ? history.AsReadOnly()
                : Array.Empty<AgentMessage>();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Salva il contesto completo del progetto (per resume del workflow).
    /// In produzione: usa Cosmos DB con TTL automatico.
    /// </summary>
    public async Task SaveContextAsync(
        string projectId,
        AgentContext context,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _contexts[projectId] = context;
            _logger.LogDebug("Contesto salvato per progetto {ProjectId}", projectId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Carica il contesto salvato di un progetto (per resume del workflow).
    /// </summary>
    public async Task<AgentContext?> LoadContextAsync(
        string projectId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _contexts.TryGetValue(projectId, out var context) ? context : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Elimina tutti i dati di un progetto dalla memoria.
    /// </summary>
    public async Task ClearProjectDataAsync(
        string projectId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _conversations.Remove(projectId);
            _contexts.Remove(projectId);
            _logger.LogInformation("Dati progetto {ProjectId} eliminati dalla memoria", projectId);
        }
        finally
        {
            _lock.Release();
        }
    }
}
