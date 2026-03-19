using BmadAgentFramework.Core.Models;
using Microsoft.Extensions.Logging;

namespace BmadAgentFramework.Core.Services;

/// <summary>
/// Interfaccia per lo store degli artefatti prodotti dagli agenti.
/// </summary>
public interface IArtifactStore
{
    Task SaveArtifactAsync(string projectId, ProjectArtifact artifact, CancellationToken ct = default);
    Task<ProjectArtifact?> GetArtifactAsync(string projectId, string artifactType, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectArtifact>> GetAllArtifactsAsync(string projectId, CancellationToken ct = default);
    Task<string> ExportProjectAsync(string projectId, CancellationToken ct = default);
}

/// <summary>
/// Store degli artefatti prodotti dagli agenti BMAD durante il workflow.
/// Implementazione in-memory per sviluppo/demo.
///
/// Gli artefatti sono i "deliverable" del processo BMAD:
/// - requirements.md (dall'Analyst)
/// - architecture.md (dall'Architect)
/// - codice .cs (dal Developer)
/// - test suite (dal QA)
/// - pipeline YAML + Bicep (dal DevOps)
///
/// In produzione: sostituire con AzureBlobArtifactStore che usa Azure Blob Storage
/// per la persistenza e la condivisione tra le istanze.
/// </summary>
public class ArtifactStore : IArtifactStore
{
    // Struttura: projectId → (artifactType → artifact)
    private readonly Dictionary<string, Dictionary<string, ProjectArtifact>> _store = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<ArtifactStore> _logger;

    // Counter per-progetto usato come prefisso numerico nel nome del file su disco
    private readonly Dictionary<string, int> _diskCounters = new();

    public ArtifactStore(ILogger<ArtifactStore> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Salva un artefatto nello store.
    /// Se esiste già un artefatto dello stesso tipo, incrementa la versione.
    /// Persiste automaticamente l'artefatto su disco in output/{projectId}/.
    /// </summary>
    public async Task SaveArtifactAsync(
        string projectId,
        ProjectArtifact artifact,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_store.ContainsKey(projectId))
                _store[projectId] = new Dictionary<string, ProjectArtifact>();

            // Incrementa versione se esiste già
            if (_store[projectId].TryGetValue(artifact.ArtifactType, out var existing))
            {
                artifact.Version = existing.Version + 1;
                _logger.LogInformation(
                    "Artefatto {Type} aggiornato a v{Version} per progetto {ProjectId}",
                    artifact.ArtifactType, artifact.Version, projectId);
            }
            else
            {
                _logger.LogInformation(
                    "Nuovo artefatto {Type} salvato per progetto {ProjectId} da {Agent}",
                    artifact.ArtifactType, projectId, artifact.ProducedBy);
            }

            _store[projectId][artifact.ArtifactType] = artifact;

            // Incrementa il counter per-progetto e persiste su disco
            var counter = _diskCounters.GetValueOrDefault(projectId, 0) + 1;
            _diskCounters[projectId] = counter;
            await PersistArtifactToDiskAsync(projectId, counter, artifact);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Recupera un artefatto specifico per tipo.
    /// </summary>
    public async Task<ProjectArtifact?> GetArtifactAsync(
        string projectId,
        string artifactType,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _store.TryGetValue(projectId, out var artifacts)
                && artifacts.TryGetValue(artifactType, out var artifact)
                ? artifact
                : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Recupera tutti gli artefatti di un progetto.
    /// </summary>
    public async Task<IReadOnlyList<ProjectArtifact>> GetAllArtifactsAsync(
        string projectId,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _store.TryGetValue(projectId, out var artifacts)
                ? artifacts.Values.ToList().AsReadOnly()
                : Array.Empty<ProjectArtifact>();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Esporta tutti gli artefatti del progetto in formato Markdown strutturato.
    /// Utile per la revisione finale da parte del team.
    /// </summary>
    public async Task<string> ExportProjectAsync(
        string projectId,
        CancellationToken ct = default)
    {
        var artifacts = await GetAllArtifactsAsync(projectId, ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# BMAD Framework - Output Progetto");
        sb.AppendLine($"**ProjectId**: {projectId}");
        sb.AppendLine($"**Generato il**: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var artifact in artifacts.OrderBy(a => a.CreatedAt))
        {
            sb.AppendLine($"## {artifact.Name}");
            sb.AppendLine($"*Prodotto da: {artifact.ProducedBy} | Formato: {artifact.ContentFormat} | v{artifact.Version}*");
            sb.AppendLine();
            sb.AppendLine(artifact.Content);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        var safeProjectId = SanitizeForFileName(projectId);
        sb.AppendLine("## File su disco");
        sb.AppendLine();
        sb.AppendLine($"Gli artefatti sono stati salvati in: `output/{safeProjectId}/`");

        return sb.ToString();
    }

    /// <summary>
    /// Persiste un artefatto su disco nella cartella output/{projectId}/ con nome
    /// {counter:D2}-{ProducedBy}-{ArtifactType}.md. Gli errori I/O sono loggati come
    /// warning e non interrompono il workflow.
    /// </summary>
    private async Task PersistArtifactToDiskAsync(string projectId, int counter, ProjectArtifact artifact)
    {
        try
        {
            var safeProjectId = SanitizeForFileName(projectId);
            var safeProducedBy = SanitizeForFileName(artifact.ProducedBy);
            var safeArtifactType = SanitizeForFileName(artifact.ArtifactType);

            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output", safeProjectId);
            Directory.CreateDirectory(outputDir);

            var fileName = $"{counter:D2}-{safeProducedBy}-{safeArtifactType}.md";
            var filePath = Path.Combine(outputDir, fileName);

            string fileContent;
            if (string.Equals(artifact.ContentFormat, "path", StringComparison.OrdinalIgnoreCase))
            {
                fileContent = $"""
                    # {artifact.ProducedBy} — {artifact.ArtifactType}

                    La .NET solution è stata esportata in:

                    ```
                    {artifact.Content}
                    ```
                    """;
            }
            else
            {
                fileContent = artifact.Content;
            }

            await File.WriteAllTextAsync(filePath, fileContent, System.Text.Encoding.UTF8);

            _logger.LogInformation(
                "Artefatto {Type} persistito su disco: {FilePath}",
                artifact.ArtifactType, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Impossibile persistere l'artefatto {Type} su disco per progetto {ProjectId}",
                artifact.ArtifactType, projectId);
        }
    }

    /// <summary>
    /// Sanitizza una stringa per l'uso come componente di un nome file,
    /// sostituendo spazi con underscore e rimuovendo caratteri non validi.
    /// </summary>
    private static readonly char[] InvalidFileNameChars =
        Path.GetInvalidFileNameChars()
            .Concat(Path.GetInvalidPathChars())
            .Distinct()
            .ToArray();

    private static string SanitizeForFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var sanitized = string.Concat(value
            .Replace(' ', '_')
            .Select(c => InvalidFileNameChars.Contains(c) ? '_' : c));

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
