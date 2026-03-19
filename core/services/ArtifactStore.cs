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

    public ArtifactStore(ILogger<ArtifactStore> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Salva un artefatto nello store.
    /// Se esiste già un artefatto dello stesso tipo, incrementa la versione.
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

        return sb.ToString();
    }
}
