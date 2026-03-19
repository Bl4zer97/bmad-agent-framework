using System.Text;
using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BmadAgentFramework.Agents.SolutionBuilder;

/// <summary>
/// Agente Solution Builder del framework BMAD.
///
/// Responsabilità:
/// - Leggere l'artefatto di codice prodotto dal DeveloperAgent (markdown con blocchi ```csharp)
/// - Estrarre i singoli file C# con i relativi percorsi
/// - Scrivere i file .cs effettivi nella cartella output/{ProjectName}/
/// - Generare automaticamente il file .csproj se non presente nel codice generato
/// - Produrre un README.md con il riepilogo della soluzione
///
/// Input necessari: artefatto "code" prodotto dal DeveloperAgent
/// Output prodotto: file su disco + artefatto "solution" con il riepilogo
/// </summary>
public class SolutionBuilderAgent : IAgent
{
    /// <inheritdoc/>
    public string Name => "SolutionBuilderAgent";

    /// <inheritdoc/>
    public string Role => "Solution Materializer";

    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly FrameworkOptions _options;
    private readonly ILogger<SolutionBuilderAgent> _logger;

    /// <summary>
    /// Inizializza una nuova istanza di <see cref="SolutionBuilderAgent"/>.
    /// </summary>
    public SolutionBuilderAgent(
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        IOptions<FrameworkOptions> options,
        ILogger<SolutionBuilderAgent> logger)
    {
        _artifactStore = artifactStore;
        _memoryService = memoryService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Il SolutionBuilder opera nella fase <see cref="WorkflowPhase.SolutionBuilding"/>,
    /// subito dopo il DeveloperAgent. Richiede che l'artefatto "code" sia disponibile.
    /// </summary>
    public Task<bool> CanHandleAsync(AgentContext context)
    {
        var canHandle = context.CurrentPhase == WorkflowPhase.SolutionBuilding
            && context.GetArtifact("code") != null;
        return Task.FromResult(canHandle);
    }

    /// <summary>
    /// Materializza il codice generato dal DeveloperAgent in file effettivi su disco.
    ///
    /// Flusso di esecuzione:
    /// 1. Legge l'artefatto "code" (markdown con blocchi ```csharp)
    /// 2. Estrae i blocchi di codice con i loro percorsi relativi
    /// 3. Scrive i file nella cartella output/{ProjectName}/
    /// 4. Genera il file .csproj se non già presente nel codice generato
    /// 5. Genera un README.md con il riepilogo della struttura
    /// 6. Salva l'artefatto "solution" con il riepilogo dei file creati
    /// </summary>
    public async Task<AgentMessage> ProcessAsync(AgentContext context, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "SolutionBuilderAgent avviato per progetto {ProjectId} ({ProjectName})",
            context.ProjectId, context.ProjectName);

        var codeArtifact = context.GetArtifact("code")
            ?? throw new InvalidOperationException("Manca artefatto 'code' dal DeveloperAgent");

        var projectName = SanitizeProjectName(context.ProjectName);

        // Calcola la directory di output assoluta
        var baseOutputPath = Path.IsPathRooted(_options.OutputPath)
            ? _options.OutputPath
            : Path.Combine(Directory.GetCurrentDirectory(), _options.OutputPath);
        var outputDir = Path.Combine(baseOutputPath, projectName);
        Directory.CreateDirectory(outputDir);

        // 1. Estrai i blocchi di codice dal markdown prodotto dal DeveloperAgent
        var files = SolutionWriter.ParseCodeBlocks(codeArtifact.Content);

        _logger.LogInformation(
            "Trovati {FileCount} blocchi di codice nell'output del DeveloperAgent",
            files.Count);

        // 2. Scrivi i file su disco preservando la struttura delle cartelle
        await SolutionWriter.WriteFilesAsync(outputDir, files, ct);

        // 3. Genera il file .csproj se non già presente nel codice generato
        var hasCsproj = files.Any(f =>
            f.FilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));

        if (!hasCsproj)
        {
            var csprojContent = SolutionWriter.GenerateProjectFile(projectName);
            var csprojPath = Path.Combine(outputDir, $"{projectName}.csproj");
            await File.WriteAllTextAsync(csprojPath, csprojContent, ct);
            _logger.LogInformation("Generato file progetto: {CsprojPath}", csprojPath);
        }

        // 4. Genera README.md con il riepilogo della struttura del progetto
        var readmeContent = BuildReadme(projectName, files, context, hasCsproj);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "README.md"), readmeContent, ct);

        // 5. Crea e salva l'artefatto "solution"
        var solutionSummary = BuildSolutionSummary(projectName, outputDir, files, hasCsproj);
        var artifact = ProjectArtifact.CreateSolution(solutionSummary, outputDir, Name);
        await _artifactStore.SaveArtifactAsync(context.ProjectId, artifact, ct);
        context.SaveArtifact(artifact);

        var message = new AgentMessage
        {
            AgentName = Name,
            Role = AgentRole.SolutionBuilder,
            Content = solutionSummary,
            Phase = WorkflowPhase.SolutionBuilding,
            Metadata = new Dictionary<string, string>
            {
                ["outputPath"] = outputDir,
                ["fileCount"] = files.Count.ToString(),
                ["projectName"] = projectName
            }
        };

        await _memoryService.SaveConversationAsync(context.ProjectId, message, ct);
        context.AddMessage(message);

        _logger.LogInformation(
            "SolutionBuilderAgent completato. Soluzione scritta in: {OutputPath} ({FileCount} file C#)",
            outputDir, files.Count);

        return message;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sanitizza il nome del progetto rimuovendo caratteri non validi per un nome di cartella.
    /// </summary>
    private static string SanitizeProjectName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return "GeneratedApp";

        var sanitized = new string(
            rawName
                .Replace(" ", "")
                .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')
                .ToArray());

        return string.IsNullOrEmpty(sanitized) ? "GeneratedApp" : sanitized;
    }

    /// <summary>
    /// Costruisce il testo di riepilogo dell'artefatto "solution".
    /// </summary>
    private static string BuildSolutionSummary(
        string projectName,
        string outputDir,
        IReadOnlyList<(string FilePath, string Content)> files,
        bool hadCsproj)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Soluzione generata: {projectName}");
        sb.AppendLine();
        sb.AppendLine($"**Percorso output**: `{outputDir}`");
        sb.AppendLine($"**File C# generati**: {files.Count}");
        sb.AppendLine();
        sb.AppendLine("## File creati:");
        foreach (var (filePath, _) in files)
            sb.AppendLine($"- `{filePath}`");
        if (!hadCsproj)
            sb.AppendLine($"- `{projectName}.csproj` (generato automaticamente)");
        sb.AppendLine("- `README.md` (generato automaticamente)");
        return sb.ToString();
    }

    /// <summary>
    /// Costruisce il contenuto del README.md per il progetto generato.
    /// </summary>
    private static string BuildReadme(
        string projectName,
        IReadOnlyList<(string FilePath, string Content)> files,
        AgentContext context,
        bool hadCsproj)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {projectName}");
        sb.AppendLine();
        sb.AppendLine("Progetto generato automaticamente dal framework **BMAD Agent**.");
        sb.AppendLine();
        sb.AppendLine($"**Data generazione**: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC  ");
        sb.AppendLine($"**ProjectId**: `{context.ProjectId}`");
        sb.AppendLine();
        sb.AppendLine("## Struttura del progetto");
        sb.AppendLine();
        foreach (var (filePath, _) in files)
            sb.AppendLine($"- `{filePath}`");
        if (!hadCsproj)
            sb.AppendLine($"- `{projectName}.csproj`");
        sb.AppendLine();
        sb.AppendLine("## Come avviare il progetto");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine($"cd {projectName}");
        sb.AppendLine("dotnet restore");
        sb.AppendLine("dotnet build");
        sb.AppendLine("dotnet run");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Requisiti originali");
        sb.AppendLine();
        sb.AppendLine(context.Requirements);
        return sb.ToString();
    }
}
