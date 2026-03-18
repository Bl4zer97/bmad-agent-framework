namespace BmadAgentFramework.Agents.Architect;

/// <summary>
/// Designer per i prompt di progettazione architetturale.
/// Costruisce prompt specializzati per l'Architect Agent.
/// </summary>
public static class ArchitectureDesigner
{
    /// <summary>
    /// Costruisce il prompt per la progettazione dell'architettura.
    /// Include i requisiti analizzati e richiede un documento tecnico dettagliato.
    /// </summary>
    public static string BuildArchitecturePrompt(
        string requirementsContent,
        string projectName,
        Dictionary<string, string>? metadata = null)
    {
        var techStack = metadata?.TryGetValue("techStack", out var stack) == true
            ? stack
            : "C# .NET 8, Azure";

        return $"""
            Basandoti sui seguenti requisiti, progetta l'architettura completa del sistema.

            ## Requisiti del Progetto
            {requirementsContent}

            ## Stack Tecnologico
            {techStack}

            ## Output Richiesto
            Produci un documento di architettura tecnica con questa struttura:

            # Documento di Architettura Tecnica

            ## 1. Overview Architetturale
            - Pattern architetturale scelto e motivazione
            - Diagramma ASCII dei componenti principali

            ## 2. Componenti del Sistema
            Per ogni componente:
            - Nome e responsabilità
            - Tecnologia usata
            - Interfacce esposte

            ## 3. Servizi Azure
            Lista dei servizi Azure con:
            - Nome servizio
            - Tier/SKU raccomandato
            - Motivazione della scelta
            - Costo stimato mensile

            ## 4. Layer Applicativi (Clean Architecture)
            ```
            ┌─────────────────────┐
            │   Presentation      │  ← API Controllers, Minimal API
            ├─────────────────────┤
            │   Application       │  ← Use Cases, CQRS Handlers
            ├─────────────────────┤
            │   Domain            │  ← Entities, Business Logic
            ├─────────────────────┤
            │   Infrastructure    │  ← EF Core, Azure Services
            └─────────────────────┘
            ```

            ## 5. Schema Dati
            - Entità principali e relazioni
            - Strategia di storage (SQL, NoSQL, Blob)

            ## 6. API Design
            - Endpoint REST principali
            - Formato request/response

            ## 7. Security Architecture
            - Authentication (Azure AD / Entra ID)
            - Authorization (RBAC)
            - Secrets management (Key Vault)

            ## 8. Resilienza e Scalabilità
            - Retry policy
            - Circuit breaker
            - Auto-scaling strategy

            ## 9. Struttura del Progetto .NET
            Struttura di cartelle/namespace raccomandata

            ## 10. ADR (Architecture Decision Records)
            Principali decisioni tecniche con motivazioni

            Sii preciso e dettagliato. Il documento è direttamente usato dal Developer Agent.
            """;
    }
}
