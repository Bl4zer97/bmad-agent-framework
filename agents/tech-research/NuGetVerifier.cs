namespace BmadAgentFramework.Agents.TechResearch;

/// <summary>
/// Helper statico per la costruzione dei prompt del TechResearchAgent.
/// Analogo a RequirementsParser (per l'Analyst) e CodeGenerator (per il Developer).
/// </summary>
public static class NuGetVerifier
{
    /// <summary>
    /// Costruisce il prompt per il TechResearchAgent.
    /// Chiede al modello di produrre un documento di riferimento tecnico strutturato
    /// con versioni NuGet verificate, snippet di inizializzazione e anti-pattern.
    /// </summary>
    /// <param name="architectureContent">Contenuto del documento di architettura</param>
    /// <param name="projectName">Nome del progetto</param>
    /// <param name="metadata">Metadati aggiuntivi del contesto</param>
    /// <returns>Prompt completo per il TechResearchAgent</returns>
    public static string BuildTechResearchPrompt(
        string architectureContent,
        string? projectName,
        Dictionary<string, string>? metadata = null)
    {
        var name = projectName ?? "Progetto";

        return $"""
            Analizza il documento di architettura qui sotto e produci un documento di riferimento tecnico
            per il progetto "{name}".

            ## Documento di Architettura
            {architectureContent}

            ## Output Richiesto
            Produci un documento Markdown con la seguente struttura ESATTA:

            # Riferimento Tecnico — {name}

            ## 1. Pacchetti NuGet Verificati
            Per ogni pacchetto NuGet citato nell'architettura, fornisci:
            | Pacchetto | Versione Stable | Note |
            |-----------|----------------|------|
            | NomePacchetto | X.Y.Z | Breve descrizione dell'uso |

            Regole OBBLIGATORIE per questa sezione:
            - Usa SOLO versioni che conosci con certezza. Se non sei sicuro, scrivi "VERIFICARE MANUALMENTE"
            - Per Azure Functions Worker .NET 8: Microsoft.Azure.Functions.Worker 2.x, Sdk 2.0.7
            - Per Telegram.Bot: versione 22.x (NON 21.x — l'API è cambiata significativamente)
            - Per Azure AI Agents: Azure.AI.Agents.Persistent (NON Azure.AI.Projects che è deprecato)

            ## 2. Inizializzazione Servizi (Program.cs)
            Per ogni servizio/pacchetto principale, fornisci lo snippet DI completo da inserire in Program.cs.
            Formato per ogni servizio:

            ### NomeServizio
            ```csharp
            // Snippet di registrazione DI in Program.cs o nella classe di estensione
            // Completo e compilabile
            ```

            ## 3. API Reference per Libreria
            Per ogni libreria principale, documenta:

            ### NomeLibreria (NomePacchetto vX.Y.Z)
            **Classe principale**: `NomeClasse`
            **Namespace**: `NomeNamespace`
            **Costruttore**:
            ```csharp
            // Come istanziare la classe
            ```
            **Metodi chiave**:
            - `NomeMetodo(params)` → descrizione
            - `AltroMetodo(params)` → descrizione

            ## 4. Anti-Pattern da Evitare
            Lista ESPLICITA di errori comuni per questo progetto:

            | ❌ NON fare | ✅ Fare invece | Motivo |
            |------------|----------------|--------|
            | Classe/metodo sbagliato | Classe/metodo corretto | Spiegazione |

            Includi SEMPRE anti-pattern specifici per:
            - Azure Functions: NON WebApplication.CreateBuilder → usa HostBuilder + ConfigureFunctionsWebApplication
            - Azure AI Agents (se usati): NON ProjectsClient/AIProjectClient → usa PersistentAgentsClient
            - Telegram.Bot v22 (se usato): NON SendTextMessageAsync → usa SendMessage

            ## 5. File di Configurazione Richiesti
            Per ogni file di configurazione necessario, fornisci un template completo con tutti i campi richiesti:

            ### appsettings.json (o host.json per Azure Functions)
            Fornisci il template JSON completo con tutte le sezioni necessarie per il progetto.

            ### local.settings.json (solo per Azure Functions)
            Fornisci il template con:
            - IsEncrypted: false
            - Values.AzureWebJobsStorage: "UseDevelopmentStorage=true"
            - Values.FUNCTIONS_WORKER_RUNTIME: "dotnet-isolated"
            - Tutte le connection string e configuration keys necessarie

            ATTENZIONE: Se non sei sicuro di un'API o di una versione NuGet, scrivi SEMPRE
            "VERIFICARE MANUALMENTE" — non inventare mai classi, metodi o versioni che potrebbero
            non esistere. La precisione è prioritaria rispetto alla completezza.
            """;
    }
}
