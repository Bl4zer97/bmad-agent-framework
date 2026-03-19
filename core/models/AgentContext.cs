namespace BmadAgentFramework.Core.Models;

/// <summary>
/// Fase corrente del workflow BMAD.
/// Gli agenti vengono eseguiti in sequenza secondo questa enumerazione.
/// </summary>
public enum WorkflowPhase
{
    /// <summary>Fase iniziale - raccolta requisiti</summary>
    Analysis = 0,

    /// <summary>Fase di progettazione architetturale</summary>
    Architecture = 1,

    /// <summary>Fase di sviluppo del codice</summary>
    Development = 2,

    /// <summary>Fase di testing e quality assurance</summary>
    QualityAssurance = 3,

    /// <summary>Fase di deployment e infrastruttura</summary>
    DevOps = 4,

    /// <summary>Workflow completato con successo</summary>
    Completed = 5,

    /// <summary>Workflow in attesa di approvazione umana (human-in-the-loop)</summary>
    PendingApproval = 6,

    /// <summary>Workflow fallito con errori</summary>
    Failed = 7,

    /// <summary>Fase di materializzazione della soluzione: crea i file effettivi nella cartella output</summary>
    SolutionBuilding = 8
}

/// <summary>
/// Contesto condiviso tra tutti gli agenti del framework BMAD.
/// Viene passato da un agente all'altro, accumulando artefatti e informazioni.
/// È il "canale di comunicazione" principale tra gli agenti.
/// </summary>
public class AgentContext
{
    /// <summary>Identificativo univoco del progetto</summary>
    public string ProjectId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Timestamp di creazione del contesto</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Requisiti originali del progetto forniti dall'utente.
    /// Esempio: "Crea una REST API per gestire una lista di task in .NET 8 con Azure"
    /// </summary>
    public string Requirements { get; set; } = string.Empty;

    /// <summary>Fase corrente del workflow</summary>
    public WorkflowPhase CurrentPhase { get; set; } = WorkflowPhase.Analysis;

    /// <summary>
    /// Artefatti prodotti dagli agenti durante il workflow.
    /// Key = nome dell'artefatto (es. "requirements", "architecture", "code")
    /// Value = contenuto dell'artefatto
    /// </summary>
    public Dictionary<string, ProjectArtifact> Artifacts { get; init; } = new();

    /// <summary>
    /// Cronologia completa delle conversazioni con tutti gli agenti.
    /// Permette agli agenti successivi di avere il contesto completo.
    /// </summary>
    public List<AgentMessage> ConversationHistory { get; init; } = new();

    /// <summary>
    /// Metadati aggiuntivi del progetto (es. linguaggio target, framework, cloud provider).
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Nome del progetto estratto dai requisiti
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Stack tecnologico identificato dall'Analyst Agent
    /// </summary>
    public string TechnologyStack { get; set; } = string.Empty;

    /// <summary>
    /// Aggiunge un messaggio alla cronologia e aggiorna il contesto
    /// </summary>
    public void AddMessage(AgentMessage message)
    {
        ConversationHistory.Add(message);
    }

    /// <summary>
    /// Salva un artefatto prodotto da un agente
    /// </summary>
    public void SaveArtifact(ProjectArtifact artifact)
    {
        Artifacts[artifact.ArtifactType] = artifact;
    }

    /// <summary>
    /// Recupera un artefatto per tipo (es. "requirements", "architecture")
    /// </summary>
    public ProjectArtifact? GetArtifact(string artifactType)
    {
        return Artifacts.TryGetValue(artifactType, out var artifact) ? artifact : null;
    }
}
