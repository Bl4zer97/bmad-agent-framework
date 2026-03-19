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
            Genera il codice per una {appType} con questa struttura:

            1. **Domain Layer**
               - Entità con record types C# 12
               - Value Objects se appropriati
               - Domain events

            2. **Application Layer**
               - Interfacce dei repository
               - Use cases / Command handlers
               - DTOs (Data Transfer Objects)

            3. **Infrastructure Layer**
               - Implementazione repository con EF Core
               - DbContext configurato
               - Configurazioni Entity Framework

            4. **API Layer** (se REST API)
               - Controller o Minimal API endpoints
               - Middleware per error handling
               - Program.cs con DI setup completo

            ## Standard di Codice
            - Usa C# 12 syntax (record types, primary constructors, collection expressions)
            - Tutti i metodi async/await
            - Nullable reference types abilitati
            - XML documentation su classi e metodi pubblici
            - Dependency injection su tutto
            - Logging con ILogger<T>

            ## Formato Output
            Produci il codice in blocchi ```csharp separati per ogni file, con il path completo come titolo.
            Esempio:
            ### src/Domain/Entities/TodoItem.cs
            ```csharp
            // codice...
            ```

            Inizia sempre con il domain layer e progredisci verso l'esterno.
            """;
    }
}
