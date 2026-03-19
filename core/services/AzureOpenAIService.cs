using Azure;
using Azure.AI.OpenAI;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Polly;
using Polly.Retry;

namespace BmadAgentFramework.Core.Services;

/// <summary>
/// Servizio di integrazione con Azure OpenAI.
/// Gestisce tutte le chiamate al modello gpt-4o-mini con retry logic (Polly),
/// system prompt per agente e gestione degli errori.
///
/// Flusso di una chiamata:
/// 1. Riceve il system prompt dell'agente + la cronologia della conversazione
/// 2. Aggiunge il contesto del progetto corrente
/// 3. Chiama Azure OpenAI con retry in caso di errori transitori
/// 4. Restituisce la risposta come AgentMessage
/// </summary>
public class AzureOpenAIService
{
    private readonly AzureOpenAIClient _client;
    private readonly FrameworkOptions _options;
    private readonly ILogger<AzureOpenAIService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public AzureOpenAIService(
        IOptions<FrameworkOptions> options,
        ILogger<AzureOpenAIService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Inizializza il client Azure OpenAI con API Key
        // In produzione: usa DefaultAzureCredential() per Managed Identity
        _client = new AzureOpenAIClient(
            new Uri(_options.AzureOpenAIEndpoint),
            new AzureKeyCredential(_options.AzureOpenAIApiKey));

        // Configura la retry policy con Polly:
        // - 3 tentativi con backoff esponenziale
        // - Gestisce errori 429 (rate limit) e errori di rete
        _retryPolicy = Policy
            .Handle<RequestFailedException>(ex => ex.Status == 429 || ex.Status >= 500)
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, delay, attempt, _) =>
                {
                    _logger.LogWarning(
                        "Tentativo {Attempt} fallito: {Error}. Riprovando in {Delay}s...",
                        attempt, exception.Message, delay.TotalSeconds);
                });
    }

    /// <summary>
    /// Invia un prompt ad Azure OpenAI e riceve la risposta dell'agente.
    /// </summary>
    /// <param name="agentName">Nome dell'agente (usato per logging)</param>
    /// <param name="systemPrompt">Il system prompt che definisce il ruolo dell'agente</param>
    /// <param name="userMessage">Il messaggio/task dell'agente</param>
    /// <param name="conversationHistory">Cronologia precedente (per contesto)</param>
    /// <param name="agentConfig">Configurazione specifica dell'agente</param>
    /// <param name="ct">Token di cancellazione</param>
    /// <returns>Risposta testuale del modello</returns>
    public async Task<string> GetCompletionAsync(
        string agentName,
        string systemPrompt,
        string userMessage,
        IEnumerable<AgentMessage>? conversationHistory = null,
        AgentConfiguration? agentConfig = null,
        CancellationToken ct = default)
    {
        var config = agentConfig ?? new AgentConfiguration
        {
            ModelDeploymentName = _options.DefaultModelDeployment
        };

        _logger.LogInformation(
            "Agente {AgentName} sta elaborando con modello {Model}...",
            agentName, config.ModelDeploymentName);

        // Costruisce i messaggi per la chiamata
        var messages = BuildMessages(systemPrompt, userMessage, conversationHistory);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

            var chatClient = _client.GetChatClient(config.ModelDeploymentName);

            var chatOptions = new ChatCompletionOptions
            {
                Temperature = config.Temperature,
                MaxOutputTokenCount = config.MaxTokens
            };

            var response = await chatClient.CompleteChatAsync(messages, chatOptions, cts.Token);

            var result = response.Value.Content[0].Text;

            _logger.LogInformation(
                "Agente {AgentName} ha completato. Token usati: {Tokens}",
                agentName, response.Value.Usage.TotalTokenCount);

            if (_options.EnableVerboseLogging)
            {
                _logger.LogDebug("Risposta agente {AgentName}: {Response}", agentName, result);
            }

            return result;
        });
    }

    /// <summary>
    /// Costruisce la lista di messaggi per Azure OpenAI includendo
    /// il system prompt, la cronologia e il messaggio corrente.
    /// </summary>
    private static List<ChatMessage> BuildMessages(
        string systemPrompt,
        string userMessage,
        IEnumerable<AgentMessage>? history)
    {
        var messages = new List<ChatMessage>
        {
            // System prompt: definisce il comportamento dell'agente
            new SystemChatMessage(systemPrompt)
        };

        // Aggiunge la cronologia delle conversazioni precedenti
        // Questo permette ad ogni agente di vedere il lavoro degli agenti precedenti
        if (history != null)
        {
            foreach (var historyMessage in history.Where(m => !m.IsError))
            {
                // I messaggi degli agenti precedenti vengono aggiunti come "assistant" messages
                messages.Add(new AssistantChatMessage(
                    $"[{historyMessage.AgentName}]: {historyMessage.Content}"));
            }
        }

        // Il messaggio corrente (il task dell'agente)
        messages.Add(new UserChatMessage(userMessage));

        return messages;
    }
}
