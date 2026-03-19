namespace BmadAgentFramework.Core.Configuration;

/// <summary>
/// Opzioni globali del framework BMAD.
/// Configurate tramite appsettings.json e Azure Key Vault.
/// </summary>
public class FrameworkOptions
{
    /// <summary>Nome della sezione in appsettings.json</summary>
    public const string SectionName = "BmadFramework";

    /// <summary>
    /// Endpoint di Azure OpenAI (es. "https://my-openai.openai.azure.com/")
    /// </summary>
    public string AzureOpenAIEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// API Key di Azure OpenAI.
    /// In produzione, recuperare da Azure Key Vault tramite Managed Identity.
    /// </summary>
    public string AzureOpenAIApiKey { get; set; } = string.Empty;

    /// <summary>Nome del deployment GPT-4o su Azure OpenAI</summary>
    public string DefaultModelDeployment { get; set; } = "gpt-4o";

    /// <summary>
    /// Connection string di Azure Service Bus per la comunicazione tra agenti.
    /// In sviluppo locale, può essere omessa (usa memoria in-process).
    /// </summary>
    public string ServiceBusConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Connection string di Azure Storage per gli artefatti.
    /// In sviluppo locale, usa Azurite o storage locale.
    /// </summary>
    public string StorageConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Connection string di Azure Cosmos DB per la memoria degli agenti.
    /// In sviluppo locale, usa l'emulatore Cosmos DB.
    /// </summary>
    public string CosmosDbConnectionString { get; set; } = string.Empty;

    /// <summary>Abilita il logging dettagliato delle chiamate AI (utile per debug)</summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Modalità di esecuzione: "InMemory" per sviluppo, "Azure" per produzione
    /// </summary>
    public string ExecutionMode { get; set; } = "InMemory";

    /// <summary>Configurazioni specifiche per ogni agente</summary>
    public Dictionary<string, AgentConfiguration> Agents { get; set; } = new();

    /// <summary>Indica se siamo in modalità sviluppo locale</summary>
    public bool IsLocalDevelopment => ExecutionMode == "InMemory";
}
