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
            Definisci la struttura ESATTA della solution .NET in un blocco `solution-structure` parsabile.
            Il blocco è la FONTE DI VERITÀ per il Developer Agent e il SolutionExporter Agent.

            ```solution-structure
            SOLUTION: {projectName ?? "MyApp"}
            PROJECTS:
            - Name: {projectName ?? "MyApp"}.Domain | SDK: Microsoft.NET.Sdk | References: (nessuno)
              Folders: Entities/, ValueObjects/, Events/, Interfaces/
            - Name: {projectName ?? "MyApp"}.Application | SDK: Microsoft.NET.Sdk | References: {projectName ?? "MyApp"}.Domain
              Folders: DTOs/, Interfaces/, Services/, Validators/
            - Name: {projectName ?? "MyApp"}.Infrastructure | SDK: Microsoft.NET.Sdk | References: {projectName ?? "MyApp"}.Application
              Folders: Data/, Repositories/, Configurations/, Services/
            - Name: {projectName ?? "MyApp"}.API | SDK: Microsoft.NET.Sdk.Web | References: {projectName ?? "MyApp"}.Application, {projectName ?? "MyApp"}.Infrastructure
              Folders: Controllers/, Middleware/, Extensions/
            - Name: {projectName ?? "MyApp"}.Tests | SDK: Microsoft.NET.Sdk | References: {projectName ?? "MyApp"}.Domain, {projectName ?? "MyApp"}.Application
              Folders: Unit/, Integration/
            ```

            IMPORTANTE per la struttura:
            - La struttura NON è fissa: decidila in base ai requisiti (3 o 7 progetti, con/senza CQRS, Workers, ecc.)
            - Progetti `src/`: usano SDK `Microsoft.NET.Sdk` (librerie) o `Microsoft.NET.Sdk.Web` (API/web)
            - Progetti di test: vanno sotto `tests/` con SDK `Microsoft.NET.Sdk`
            - I `References` sono i nomi esatti dei progetti separati da virgola, oppure `(nessuno)`
            - L'esempio sopra è solo un template: adattalo ai requisiti effettivi del progetto

            ## 10. ADR (Architecture Decision Records)
            Principali decisioni tecniche con motivazioni

            Sii preciso e dettagliato. Il documento è direttamente usato dal Developer Agent.
            """;
    }
}
