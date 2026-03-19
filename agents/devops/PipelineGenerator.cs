namespace BmadAgentFramework.Agents.DevOps;

/// <summary>
/// Generatore di prompt per pipeline CI/CD e infrastruttura.
/// Costruisce prompt specializzati per il DevOps Agent.
/// </summary>
public static class PipelineGenerator
{
    /// <summary>
    /// Costruisce il prompt per la generazione della pipeline e dell'infrastruttura.
    /// </summary>
    public static string BuildPipelinePrompt(
        string requirementsContent,
        string architectureContent,
        string? projectName,
        Dictionary<string, string>? metadata = null)
    {
        var deployTarget = metadata?.TryGetValue("deployTarget", out var target) == true
            ? target
            : "Azure App Service";

        return $"""
            Genera la pipeline CI/CD e l'infrastruttura Azure per il progetto "{projectName ?? "App"}".

            ## Architettura del Sistema
            {architectureContent}

            ## Requisiti Aggiuntivi
            {requirementsContent}

            ## Target di Deployment
            {deployTarget}

            ## Output Richiesto

            ### 1. GitHub Actions Pipeline (.github/workflows/ci.yml)
            Pipeline completa con:
            - Trigger: push a main e PR
            - Job: build → test → security-scan → deploy-staging → approval → deploy-prod
            - Caching dipendenze NuGet
            - Test con code coverage report
            - SAST security scan
            - Docker build e push su Azure Container Registry
            - Deployment su Azure

            ### 2. Infrastruttura Bicep (azure/main.bicep)
            - Azure App Service Plan + Web App (o Container Apps)
            - Azure SQL Database o Cosmos DB
            - Azure Key Vault per i segreti
            - Application Insights
            - Azure Service Bus (se necessario per architettura event-driven)
            - Managed Identity per autenticazione zero-trust

            ### 3. Dockerfile
            Multi-stage build ottimizzato per .NET 8

            ### 4. Documentazione Operativa (deploy.md)
            - Step di deployment manuale
            - Come configurare le variabili d'ambiente
            - Rollback procedure
            - Monitoring e alerting setup

            Usa best practice di sicurezza: mai segreti in chiaro, usa Azure Key Vault reference.
            Commenta tutto in italiano.
            """;
    }
}
