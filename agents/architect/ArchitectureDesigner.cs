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
            Definisci la struttura della solution .NET nel blocco `solution-structure` (vedi sezione 11).
            La struttura è la FONTE DI VERITÀ per il Developer Agent e il SolutionExporter Agent.
            Adattala ai requisiti del progetto: non esiste una struttura fissa.

            ## 10. ADR (Architecture Decision Records)
            Principali decisioni tecniche con motivazioni

            ## 11. Struttura della Solution .NET (OBBLIGATORIO)
            Produci il seguente blocco ESATTAMENTE in questo formato (è parsato automaticamente dal SolutionExporter):

            ```solution-structure
            SOLUTION: {projectName ?? "NomeSolution"}
            PROJECTS:
            - Name: {projectName ?? "NomeSolution"}.Domain | SDK: Microsoft.NET.Sdk | References: (nessuno)
              Folders: Entities/, ValueObjects/, Events/, Interfaces/
            - Name: {projectName ?? "NomeSolution"}.Application | SDK: Microsoft.NET.Sdk | References: {projectName ?? "NomeSolution"}.Domain
              Folders: DTOs/, Interfaces/, Services/, Validators/
              NuGetPackages: MediatR/12.4.0, FluentValidation/11.9.2, AutoMapper/13.0.1
            - Name: {projectName ?? "NomeSolution"}.Infrastructure | SDK: Microsoft.NET.Sdk | References: {projectName ?? "NomeSolution"}.Application
              Folders: Data/, Repositories/, Configurations/, Services/
              NuGetPackages: Microsoft.EntityFrameworkCore/8.0.11, Microsoft.EntityFrameworkCore.SqlServer/8.0.11, Azure.Identity/1.13.1
            - Name: {projectName ?? "NomeSolution"}.API | SDK: Microsoft.NET.Sdk.Web | References: {projectName ?? "NomeSolution"}.Application, {projectName ?? "NomeSolution"}.Infrastructure
              Folders: Controllers/, Middleware/, Extensions/
              NuGetPackages: Swashbuckle.AspNetCore/6.9.0, Serilog.AspNetCore/8.0.3
            - Name: {projectName ?? "NomeSolution"}.Tests | SDK: Microsoft.NET.Sdk | References: {projectName ?? "NomeSolution"}.Domain, {projectName ?? "NomeSolution"}.Application
              Folders: Unit/, Integration/
              NuGetPackages: xunit/2.9.2, FluentAssertions/6.12.2, Moq/4.20.72, Microsoft.NET.Test.Sdk/17.12.0
            ```

            Regole OBBLIGATORIE per il blocco solution-structure:
            - Il nome di ogni progetto DEVE usare il pattern NomeSolution.Layer (es. MyApp.Domain, MyApp.API)
            - SDK: usare Microsoft.NET.Sdk.Web SOLO per il progetto API/presentation; tutti gli altri usano Microsoft.NET.Sdk
            - SDK: usare Microsoft.NET.Sdk per Azure Functions (non Microsoft.NET.Sdk.Web)
            - References: elencare i nomi ESATTI dei progetti referenziati separati da virgola, oppure (nessuno)
            - Folders: elencare le cartelle principali del progetto con trailing slash, separate da virgola
            - NuGetPackages: elencare SOLO i pacchetti effettivamente usati in quel progetto, nel formato Nome/Versione
            - Per Azure Functions: includere Microsoft.Azure.Functions.Worker/2.0.0, Microsoft.Azure.Functions.Worker.Sdk/2.0.7, Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore/2.1.0
            - Per Telegram Bot: includere Telegram.Bot/22.0.0 (NON 21.x)
            - Per Azure AI Agents: includere Azure.AI.Agents.Persistent/1.0.0-beta.3 (NON Azure.AI.Projects)
            - La struttura NON è fissa: adattala ai requisiti (3 o 7 progetti, con/senza CQRS, Workers, ecc.)
            - L'esempio sopra è solo un template Clean Architecture: adattalo al progetto specifico

            Sii preciso e dettagliato. Il documento è direttamente usato dal Developer Agent.
            """;
    }
}
