namespace BmadAgentFramework.Core.Configuration;

/// <summary>
/// Configurazione di un singolo agente BMAD.
/// Contiene il system prompt, i parametri del modello AI e le impostazioni operative.
/// </summary>
public class AgentConfiguration
{
    /// <summary>Nome identificativo dell'agente</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// System prompt che definisce il comportamento e il ruolo dell'agente.
    /// È il "manuale operativo" che Azure OpenAI usa per capire come deve comportarsi.
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>Modello Azure OpenAI da usare (es. "gpt-4o", "gpt-4-turbo")</summary>
    public string ModelDeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// Temperatura del modello (0.0 = deterministico, 1.0 = creativo).
    /// Per agenti tecnici (Developer, Architect) usare valori bassi (0.2-0.4).
    /// Per agenti creativi (Analyst) usare valori più alti (0.5-0.7).
    /// </summary>
    public float Temperature { get; set; } = 0.3f;

    /// <summary>Numero massimo di token per la risposta</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Numero massimo di tentativi in caso di errore (Polly retry)</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Timeout per ogni chiamata ad Azure OpenAI (secondi)</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Indica se questo agente richiede approvazione umana prima di procedere
    /// (pattern human-in-the-loop)
    /// </summary>
    public bool RequiresHumanApproval { get; set; } = false;
}
