using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Configuration;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BmadAgentFramework.Agents.SolutionExporter;

/// <summary>
/// Agente esportatore della solution del framework BMAD.
///
/// Responsabilità:
/// - Leggere l'artefatto "code" prodotto dal DeveloperAgent (Markdown con blocchi C#)
/// - Leggere l'artefatto "architecture" prodotto dall'ArchitectAgent (struttura multi-progetto)
/// - Estrarre i blocchi ```csharp dal Markdown rispettando i path definiti dal Developer
/// - Parsare il blocco `solution-structure` dall'architettura per la struttura multi-progetto
/// - Scrivere su disco una .NET solution strutturata con .sln, .csproj multipli e file .cs organizzati
/// - Salvare un artefatto "solution" con il percorso della cartella generata
///
/// Caratteristiche:
/// - Agente DETERMINISTICO: zero chiamate Azure OpenAI → zero costi aggiuntivi
/// - L'Architect è SENIOR sulla struttura: il SolutionExporter ESEGUE, non decide
/// - Si aggancia alla fase QualityAssurance (attivata dopo il DeveloperAgent)
///
/// Output prodotto: artefatto "solution" con il path della cartella su disco
/// Input necessari: artefatto "code" nel contesto; artefatto "architecture" opzionale (migliora la struttura)
/// </summary>
public class SolutionExporterAgent : IAgent
{
    /// <inheritdoc />
    public string Name => "SolutionExporterAgent";

    /// <inheritdoc />
    public string Role => "Solution Exporter";

    private readonly IArtifactStore _artifactStore;
    private readonly IMemoryService _memoryService;
    private readonly FrameworkOptions _options;
    private readonly ILogger<SolutionExporterAgent> _logger;

    /// <summary>
    /// Inizializza una nuova istanza di <see cref="SolutionExporterAgent"/>.
    /// Non richiede AzureOpenAIService: non effettua chiamate AI.
    /// </summary>
    public SolutionExporterAgent(
        IArtifactStore artifactStore,
        IMemoryService memoryService,
        IOptions<FrameworkOptions> options,
        ILogger<SolutionExporterAgent> logger)
    {
        _artifactStore = artifactStore;
        _memoryService = memoryService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Si attiva nella fase QualityAssurance se l'artefatto "code" è disponibile nel contesto.
    /// Questo permette di agganciare la fase normalmente saltata (nessun QAAgent registrato)
    /// senza modificare il WorkflowEngine.
    /// </summary>
    public Task<bool> CanHandleAsync(AgentContext context)
    {
        var canHandle = context.CurrentPhase == WorkflowPhase.QualityAssurance
            && context.GetArtifact("code") != null;
        return Task.FromResult(canHandle);
    }

    /// <summary>
    /// Esporta il codice C# contenuto nell'artefatto "code" in una .NET solution su disco.
    ///
    /// Flusso:
    /// 1. Legge l'artefatto "code" dal contesto
    /// 2. Legge l'artefatto "architecture" dal contesto (opzionale, per la struttura multi-progetto)
    /// 3. Estrae i blocchi ```csharp dal Markdown tramite SolutionExporterService
    /// 4. Scrive la solution su disco (cartella output/{ProjectName}-solution/) rispettando l'architettura
    /// 5. Salva un artefatto "solution" con il percorso generato
    /// 6. Restituisce un AgentMessage con il summary dell'operazione
    /// </summary>
    public async Task<AgentMessage> ProcessAsync(AgentContext context, CancellationToken ct = default)
    {
        var codeArtifact = context.GetArtifact("code")
            ?? throw new InvalidOperationException("Manca artefatto 'code'");

        // Legge l'artefatto "architecture" (opzionale) per la struttura multi-progetto
        var architectureArtifact = context.GetArtifact("architecture");
        var architectureContent = architectureArtifact?.Content;

        if (architectureContent != null)
        {
            _logger.LogInformation(
                "SolutionExporterAgent: artefatto 'architecture' trovato, " +
                "verrà usato per la struttura multi-progetto");
        }
        else
        {
            _logger.LogWarning(
                "SolutionExporterAgent: artefatto 'architecture' non trovato. " +
                "La struttura verrà inferita dai path dei file generati.");
        }

        // Estrae i blocchi C# dal Markdown
        var codeBlocks = SolutionExporterService.ExtractCodeBlocks(codeArtifact.Content);

        // Fallback: se non ci sono blocchi csharp ben formattati, scrive tutto come file unico
        if (codeBlocks.Count == 0)
        {
            _logger.LogWarning(
                "SolutionExporterAgent: nessun blocco ```csharp trovato nell'artefatto code. " +
                "Scrivo l'intero contenuto come GeneratedCode.cs");
            codeBlocks.Add(("GeneratedCode.cs", codeArtifact.Content));
        }

        _logger.LogInformation(
            "SolutionExporterAgent: estratti {Count} file C# dall'artefatto code",
            codeBlocks.Count);

        // Log dei file estratti per visibilità
        foreach (var (fileName, _) in codeBlocks)
        {
            _logger.LogDebug("SolutionExporterAgent: file estratto → {FileName}", fileName);
        }

        // Determina il percorso di output (stesso CWD usato dal Program.cs esistente)
        var outputBasePath = Path.Combine(Directory.GetCurrentDirectory(), "output");
        Directory.CreateDirectory(outputBasePath);

        var projectName = string.IsNullOrWhiteSpace(context.ProjectName)
            ? "BmadProject"
            : context.ProjectName;

        // Scrive la solution su disco usando anche l'architettura per la struttura multi-progetto
        var solutionPath = SolutionExporterService.WriteSolutionToDisk(
            outputBasePath,
            projectName,
            codeBlocks,
            architectureContent);

        _logger.LogInformation(
            "SolutionExporterAgent: solution scritta in {SolutionPath}",
            solutionPath);

        // Salva un artefatto "solution" con il percorso della cartella
        var artifact = new ProjectArtifact
        {
            Name = $"{projectName}.sln",
            ArtifactType = "solution",
            ProducedBy = Name,
            Content = solutionPath,
            ContentFormat = "path"
        };
        await _artifactStore.SaveArtifactAsync(context.ProjectId, artifact, ct);
        context.SaveArtifact(artifact);

        var fileList = string.Join(", ", codeBlocks.Select(b => b.FileName));
        var summary =
            $"Solution esportata in: {solutionPath}\n" +
            $"File C# scritti ({codeBlocks.Count}): {fileList}";

        var message = new AgentMessage
        {
            AgentName = Name,
            Role = AgentRole.QA,
            Content = summary,
            Phase = WorkflowPhase.QualityAssurance,
            Metadata = new Dictionary<string, string>
            {
                ["solutionPath"] = solutionPath,
                ["fileCount"] = codeBlocks.Count.ToString()
            }
        };

        await _memoryService.SaveConversationAsync(context.ProjectId, message, ct);
        context.AddMessage(message);

        return message;
    }
}
