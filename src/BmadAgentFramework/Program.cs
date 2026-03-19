using BmadAgentFramework.Agents.Analyst;
using BmadAgentFramework.Agents.Architect;
using BmadAgentFramework.Agents.Developer;
using BmadAgentFramework.Agents.DevOps;
using BmadAgentFramework.Agents.Orchestrator;
using BmadAgentFramework.Agents.QA;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

// ============================================================================
// BMAD Agent Framework - Entry Point
// Configura la Dependency Injection e avvia il workflow di esempio
// ============================================================================

// Configura Serilog per logging strutturato
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("=== BMAD Agent Framework avviato ===");

    // Costruisce il host con DI
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: true);
            config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
            config.AddEnvironmentVariables(prefix: "BMAD_");
            config.AddCommandLine(args);
        })
        .ConfigureServices((context, services) =>
        {
            // Configurazione del framework
            services.Configure<FrameworkOptions>(
                context.Configuration.GetSection(FrameworkOptions.SectionName));

            // ================================================================
            // CORE SERVICES
            // ================================================================

            // Servizi core (in-memory per sviluppo, Azure per produzione)
            services.AddSingleton<IMemoryService, MemoryService>();
            services.AddSingleton<IArtifactStore, ArtifactStore>();
            services.AddSingleton<AzureOpenAIService>();

            // ================================================================
            // AGENTI BMAD
            // L'ordine di registrazione determina l'ordine di esecuzione!
            // ================================================================

            // Analyst: analizza i requisiti
            services.AddTransient<AnalystAgent>();

            // Architect: progetta l'architettura
            services.AddTransient<ArchitectAgent>();

            // Developer: scrive il codice
            services.AddTransient<DeveloperAgent>();

            // QA: genera i test
            services.AddTransient<QAAgent>();

            // DevOps: crea pipeline e infrastruttura
            services.AddTransient<DevOpsAgent>();

            // ================================================================
            // ORCHESTRATORE
            // ================================================================

            services.AddSingleton<WorkflowEngine>();
            services.AddSingleton<OrchestratorAgent>(provider =>
            {
                var orchestrator = new OrchestratorAgent(
                    provider.GetRequiredService<IArtifactStore>(),
                    provider.GetRequiredService<IMemoryService>(),
                    provider.GetRequiredService<ILogger<OrchestratorAgent>>(),
                    provider.GetRequiredService<WorkflowEngine>());

                // Registra gli agenti nell'orchestratore in ordine di esecuzione
                orchestrator.RegisterAgent(provider.GetRequiredService<AnalystAgent>());
                orchestrator.RegisterAgent(provider.GetRequiredService<ArchitectAgent>());
                orchestrator.RegisterAgent(provider.GetRequiredService<DeveloperAgent>());
                orchestrator.RegisterAgent(provider.GetRequiredService<QAAgent>());
                orchestrator.RegisterAgent(provider.GetRequiredService<DevOpsAgent>());

                return orchestrator;
            });
        })
        .Build();

    // ========================================================================
    // ESECUZIONE DEL WORKFLOW DI ESEMPIO
    // ========================================================================

    var orchestrator = host.Services.GetRequiredService<OrchestratorAgent>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();

    // Esempio: richiesta di generazione di una Todo App
    var userRequest = args.Length > 0
        ? string.Join(" ", args)
        : """
          Crea una REST API completa in C# .NET 8 per gestire una lista di task (Todo App).
          L'API deve supportare:
          - CRUD completo per i task (Create, Read, Update, Delete)
          - Filtraggio per stato (completato/non completato)
          - Assegnazione task a utenti
          - Autenticazione con Azure AD
          - Persistenza su Azure SQL Database
          - Deployment su Azure App Service
          """;

    logger.LogInformation("Avvio workflow con richiesta:\n{Request}", userRequest);

    // Avvia il workflow completo
    var finalState = await orchestrator.RunWorkflowAsync(userRequest);

    // Mostra il risultato
    if (finalState.IsCompleted && finalState.CurrentPhase != WorkflowPhase.Failed)
    {
        logger.LogInformation(
            "\n=== WORKFLOW COMPLETATO CON SUCCESSO ===\n" +
            "Progetto: {ProjectName}\n" +
            "Artefatti prodotti: {Count}\n" +
            "- {Artifacts}",
            finalState.Context.ProjectName,
            finalState.Context.Artifacts.Count,
            string.Join("\n- ", finalState.Context.Artifacts.Keys));

        // Esporta tutti gli artefatti in un unico documento
        var artifactStore = host.Services.GetRequiredService<IArtifactStore>();
        var exportPath = Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(exportPath);

        var exportContent = await artifactStore.ExportProjectAsync(finalState.ProjectId);
        var exportFile = Path.Combine(exportPath, $"bmad-output-{finalState.ProjectId[..8]}.md");
        await File.WriteAllTextAsync(exportFile, exportContent);

        logger.LogInformation("Output completo salvato in: {Path}", exportFile);
    }
    else
    {
        logger.LogError(
            "Workflow fallito nella fase {Phase}: {Error}",
            finalState.CurrentPhase,
            finalState.ErrorMessage ?? "Errore sconosciuto");
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Errore fatale nell'applicazione BMAD");
}
finally
{
    Log.CloseAndFlush();
}

// Classe parziale necessaria per i test di integrazione
public partial class Program { }
