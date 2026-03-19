namespace BmadAgentFramework.Agents.Developer;

/// <summary>
/// Generatore di prompt per la generazione del codice C#.
/// Costruisce prompt dettagliati per il Developer Agent.
/// </summary>
public static class CodeGenerator
{
    /// <summary>
    /// Costruisce il prompt per la generazione del codice.
    /// Include requisiti, architettura e istruzioni specifiche per C# .NET 8.
    /// </summary>
    public static string BuildCodeGenerationPrompt(
        string requirementsContent,
        string architectureContent,
        string? projectName,
        Dictionary<string, string>? metadata = null)
    {
        var appType = metadata?.TryGetValue("appType", out var type) == true
            ? type
            : "REST API";

        return $"""
            Genera il codice C# .NET 8 completo per il progetto "{projectName ?? "App"}".

            ## Requisiti
            {requirementsContent}

            ## Architettura Definita
            {architectureContent}

            ## Istruzioni per il Codice
            IMPORTANTE: Segui ESATTAMENTE la struttura definita nella sezione "Struttura del Progetto .NET"
            del documento di architettura qui sopra. NON inventare progetti o layer non previsti.

            Per ogni file:
            - Una sola classe, interfaccia o record per file
            - Codice COMPLETO e compilabile (niente placeholder, TODO o "// ... resto del codice")
            - Namespace che rispecchia il percorso del file (es. `src/MyApp.Domain/Entities/` → `MyApp.Domain.Entities`)

            ## File Obbligatori
            Genera SEMPRE i seguenti file per ogni progetto principale (API, Worker, Azure Functions):
            - `Program.cs` — entry point con configurazione DI, middleware e routing
            - `appsettings.json` — configurazione base dell'applicazione
            - `appsettings.Development.json` — override per l'ambiente di sviluppo
            (Per Azure Functions: `host.json` e `local.settings.json` al posto di appsettings)

            ## Standard di Codice
            - Usa C# 12 syntax (record types, primary constructors, collection expressions)
            - Tutti i metodi async/await
            - Nullable reference types abilitati
            - XML documentation su classi e metodi pubblici
            - Dependency injection su tutto
            - Logging con ILogger<T>

            ## Formato Output OBBLIGATORIO
            Ogni file DEVE essere preceduto da un heading Markdown con il path completo relativo alla root della solution.
            Il path DEVE iniziare con `src/` per i sorgenti o `tests/` per i test.

            Formato ESATTO da rispettare per ogni file C#:

            ### src/NomeProgetto.Layer/Cartella/NomeClasse.cs
            ```csharp
            namespace NomeProgetto.Layer.Cartella;
            // codice completo della classe...
            ```

            Formato ESATTO per file JSON di configurazione (appsettings.json, host.json, ecc.):

            ### src/NomeProgetto.API/appsettings.json
            ```json
            (contenuto JSON dell'appsettings)
            ```

            NON usare mai un heading diverso da `### path/to/File.ext` prima del blocco di codice.
            Genera TUTTI i file necessari seguendo la struttura dell'Architect.
            Inizia dal layer più interno (Domain) e procedi verso l'esterno.

            ## Checklist di Completezza (OBBLIGATORIA)
            Alla fine della generazione, verifica mentalmente:
            □ Il progetto principale ha Program.cs con configurazione DI completa
            □ Il progetto principale ha appsettings.json (o host.json per Azure Functions)
            □ Ogni interfaccia custom usata ha il suo file di definizione generato
            □ Ogni DTO/Request/Response usato ha il suo file di definizione generato
            □ Ogni enum custom usato ha il suo file di definizione generato
            □ Ogni classe base/astratta usata ha il suo file di definizione generato
            □ I namespace nei `using` corrispondono ai path dei file generati
            □ Tutti i tipi referenziati che non vengono da NuGet esterni sono definiti nel codice generato
            """;
    }
}
