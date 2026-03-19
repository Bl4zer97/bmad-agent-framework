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

            Formato ESATTO da rispettare per ogni file:

            ### src/NomeProgetto.Layer/Cartella/NomeClasse.cs
            ```csharp
            namespace NomeProgetto.Layer.Cartella;
            // codice completo della classe...
            ```

            NON usare mai un heading diverso da `### path/to/File.cs` prima del blocco csharp.
            Genera TUTTI i file necessari seguendo la struttura dell'Architect.
            Inizia dal layer più interno (Domain) e procedi verso l'esterno.
            """;
    }
}
