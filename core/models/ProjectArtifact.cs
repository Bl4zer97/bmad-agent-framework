namespace BmadAgentFramework.Core.Models;

/// <summary>
/// Tipo di artefatto prodotto da un agente
/// </summary>
public enum ArtifactType
{
    /// <summary>Documento dei requisiti (prodotto dall'Analyst)</summary>
    Requirements,

    /// <summary>Documento di architettura (prodotto dall'Architect)</summary>
    Architecture,

    /// <summary>Codice sorgente generato (prodotto dal Developer)</summary>
    SourceCode,

    /// <summary>Suite di test (prodotta dal QA)</summary>
    TestSuite,

    /// <summary>Pipeline CI/CD e IaC (prodotta dal DevOps)</summary>
    Pipeline,

    /// <summary>Documentazione generica</summary>
    Documentation,

    /// <summary>Configurazione del progetto</summary>
    Configuration,

    /// <summary>Soluzione materializzata su disco (prodotta dal SolutionBuilderAgent)</summary>
    Solution
}

/// <summary>
/// Artefatto prodotto da un agente durante il workflow BMAD.
/// Gli artefatti sono i "deliverable" del processo: documenti, codice, test, pipeline.
/// Vengono salvati nell'ArtifactStore e passati agli agenti successivi come contesto.
/// </summary>
public class ProjectArtifact
{
    /// <summary>Identificativo univoco dell'artefatto</summary>
    public string ArtifactId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Nome dell'artefatto (es. "requirements.md", "TodoApp.cs")</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Tipo dell'artefatto (stringa per flessibilità):
    /// "requirements", "architecture", "code", "tests", "pipeline"
    /// </summary>
    public string ArtifactType { get; init; } = string.Empty;

    /// <summary>Nome dell'agente che ha prodotto l'artefatto</summary>
    public string ProducedBy { get; init; } = string.Empty;

    /// <summary>Timestamp di creazione</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Contenuto dell'artefatto.
    /// Può essere Markdown, codice C#, JSON, YAML, Bicep, ecc.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Formato del contenuto (markdown, csharp, json, yaml, bicep)</summary>
    public string ContentFormat { get; init; } = "markdown";

    /// <summary>Versione dell'artefatto (per gestire revisioni)</summary>
    public int Version { get; set; } = 1;

    /// <summary>Metadati aggiuntivi (es. numero di classi generate, test cases, ecc.)</summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Crea un artefatto di tipo Requirements dall'output dell'Analyst
    /// </summary>
    public static ProjectArtifact CreateRequirements(string content, string producedBy) =>
        new()
        {
            Name = "requirements.md",
            ArtifactType = "requirements",
            ProducedBy = producedBy,
            Content = content,
            ContentFormat = "markdown"
        };

    /// <summary>
    /// Crea un artefatto di tipo Architecture dall'output dell'Architect
    /// </summary>
    public static ProjectArtifact CreateArchitecture(string content, string producedBy) =>
        new()
        {
            Name = "architecture.md",
            ArtifactType = "architecture",
            ProducedBy = producedBy,
            Content = content,
            ContentFormat = "markdown"
        };

    /// <summary>
    /// Crea un artefatto di tipo SourceCode dal Developer
    /// </summary>
    public static ProjectArtifact CreateSourceCode(string content, string fileName, string producedBy) =>
        new()
        {
            Name = fileName,
            ArtifactType = "code",
            ProducedBy = producedBy,
            Content = content,
            ContentFormat = "csharp"
        };

    /// <summary>
    /// Crea un artefatto di tipo Solution dal SolutionBuilderAgent.
    /// Contiene il riepilogo dei file materializzati su disco.
    /// </summary>
    public static ProjectArtifact CreateSolution(string summary, string outputPath, string producedBy) =>
        new()
        {
            Name = "solution-summary.md",
            ArtifactType = "solution",
            ProducedBy = producedBy,
            Content = summary,
            ContentFormat = "markdown",
            Metadata = new Dictionary<string, string>
            {
                ["outputPath"] = outputPath
            }
        };
}
