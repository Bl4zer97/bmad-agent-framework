using System.Text;
using System.Text.RegularExpressions;

namespace BmadAgentFramework.Agents.SolutionExporter;

/// <summary>
/// Servizio deterministico per il parsing del Markdown prodotto dal DeveloperAgent
/// e la scrittura su disco di una .NET solution strutturata.
/// Non effettua chiamate AI — zero costi aggiuntivi.
/// </summary>
public static class SolutionExporterService
{
    /// <summary>
    /// Estrae tutti i blocchi di codice C# da un documento Markdown.
    /// Cerca blocchi delimitati da ```csharp o ```cs.
    /// Per ogni blocco, usa il commento "// File: NomeFile.cs" nella prima riga
    /// come nome del file; altrimenti assegna un nome generico.
    /// </summary>
    /// <param name="markdownContent">Contenuto Markdown dell'artefatto "code"</param>
    /// <returns>Lista di tuple (NomeFile, ContenutoFile)</returns>
    public static List<(string FileName, string Content)> ExtractCodeBlocks(string markdownContent)
    {
        var results = new List<(string FileName, string Content)>();
        var pattern = new Regex(
            @"```(?:csharp|cs)\s*\n(.*?)```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var matches = pattern.Matches(markdownContent);
        int genericCounter = 1;

        foreach (Match match in matches)
        {
            var blockContent = match.Groups[1].Value.TrimEnd();

            if (string.IsNullOrWhiteSpace(blockContent))
                continue;

            // Cerca un commento "// File: NomeFile.cs" nella prima riga del blocco
            var firstLine = blockContent.Split('\n')[0].Trim();
            string fileName;

            var fileCommentMatch = Regex.Match(firstLine, @"^//\s*File:\s*(.+\.cs)\s*$");
            if (fileCommentMatch.Success)
            {
                fileName = fileCommentMatch.Groups[1].Value.Trim();
                // Rimuove la prima riga con il commento dal contenuto del file
                var newlineIndex = blockContent.IndexOf('\n');
                blockContent = newlineIndex >= 0
                    ? blockContent[(newlineIndex + 1)..].TrimStart('\r', '\n')
                    : string.Empty;
            }
            else
            {
                fileName = $"GeneratedCode{genericCounter++}.cs";
            }

            results.Add((fileName, blockContent));
        }

        return results;
    }

    /// <summary>
    /// Scrive la solution .NET su disco nella cartella di output specificata.
    /// Crea la struttura: {outputBasePath}/{projectName}-solution/
    /// con file .sln, src/{projectName}/{projectName}.csproj e tutti i .cs estratti.
    /// </summary>
    /// <param name="outputBasePath">Percorso base della cartella di output</param>
    /// <param name="projectName">Nome del progetto (usato per cartelle e file)</param>
    /// <param name="files">Lista di file C# da scrivere (NomeFile, Contenuto)</param>
    /// <returns>Percorso assoluto della cartella solution generata</returns>
    public static string WriteSolutionToDisk(
        string outputBasePath,
        string projectName,
        List<(string FileName, string Content)> files)
    {
        var safeName = SanitizeProjectName(projectName);
        var solutionDir = Path.Combine(outputBasePath, $"{safeName}-solution");
        var srcDir = Path.Combine(solutionDir, "src", safeName);

        Directory.CreateDirectory(srcDir);

        // Scrive ogni file C# nella cartella src/{ProjectName}/
        foreach (var (fileName, content) in files)
        {
            var filePath = Path.Combine(srcDir, fileName);
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }

        // Genera e scrive il file .csproj
        var csprojContent = GenerateCsproj(safeName);
        var csprojPath = Path.Combine(srcDir, $"{safeName}.csproj");
        File.WriteAllText(csprojPath, csprojContent, Encoding.UTF8);

        // Genera e scrive il file .sln
        var csprojRelativePath = Path.Combine("src", safeName, $"{safeName}.csproj");
        var slnContent = GenerateSln(safeName, csprojRelativePath);
        var slnPath = Path.Combine(solutionDir, $"{safeName}.sln");
        File.WriteAllText(slnPath, slnContent, Encoding.UTF8);

        // Genera il README.md
        var readmeContent = GenerateReadme(safeName);
        var readmePath = Path.Combine(solutionDir, "README.md");
        File.WriteAllText(readmePath, readmeContent, Encoding.UTF8);

        return solutionDir;
    }

    /// <summary>
    /// Genera il contenuto di un file .csproj minimale per .NET 8.
    /// </summary>
    /// <param name="projectName">Nome del progetto (usato per AssemblyName e RootNamespace)</param>
    /// <returns>Contenuto XML del file .csproj</returns>
    public static string GenerateCsproj(string projectName) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net8.0</TargetFramework>
            <AssemblyName>{projectName}</AssemblyName>
            <RootNamespace>{projectName}</RootNamespace>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
        </Project>
        """;

    /// <summary>
    /// Genera il contenuto di un file .sln minimale compatibile con Visual Studio.
    /// </summary>
    /// <param name="projectName">Nome del progetto</param>
    /// <param name="csprojRelativePath">Percorso relativo del .csproj rispetto alla cartella solution</param>
    /// <returns>Contenuto del file .sln</returns>
    public static string GenerateSln(string projectName, string csprojRelativePath)
    {
        // GUID fisso per il tipo progetto C# (.NET Core / SDK-style)
        const string projectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        var projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var solutionGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

        // Normalizza il separatore di percorso per Windows (Visual Studio vuole backslash nel .sln)
        var slnCsprojPath = csprojRelativePath.Replace('/', '\\');

        return $"""

            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{projectTypeGuid}") = "{projectName}", "{slnCsprojPath}", "{projectGuid}"
            EndProject
            Global
            	GlobalSection(SolutionConfigurationPlatforms) = preSolution
            		Debug|Any CPU = Debug|Any CPU
            		Release|Any CPU = Release|Any CPU
            	EndGlobalSection
            	GlobalSection(ProjectConfigurationPlatforms) = postSolution
            		{projectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
            		{projectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU
            		{projectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU
            		{projectGuid}.Release|Any CPU.Build.0 = Release|Any CPU
            	EndGlobalSection
            	GlobalSection(SolutionProperties) = preSolution
            		HideSolutionNode = FALSE
            	EndGlobalSection
            	GlobalSection(ExtensibilityGlobals) = postSolution
            		SolutionGuid = {solutionGuid}
            	EndGlobalSection
            EndGlobal
            """;
    }

    /// <summary>
    /// Genera il contenuto del README.md con informazioni sul progetto generato.
    /// </summary>
    private static string GenerateReadme(string projectName) =>
        $"""
        # {projectName}

        Progetto generato automaticamente dal **BMAD Agent Framework**.

        ## Struttura

        ```
        {projectName}-solution/
        ├── {projectName}.sln
        ├── src/
        │   └── {projectName}/
        │       ├── {projectName}.csproj
        │       └── *.cs
        └── README.md
        ```

        ## Come avviare

        ```bash
        dotnet run --project src/{projectName}/{projectName}.csproj
        ```

        ## Artefatti BMAD

        - **Requirements**: vedi l'artefatto `requirements` nel contesto di progetto
        - **Architecture**: vedi l'artefatto `architecture` nel contesto di progetto
        """;

    /// <summary>
    /// Sanitizza il nome del progetto per l'utilizzo come nome di cartella e file.
    /// Rimuove caratteri non validi e sostituisce gli spazi con underscore.
    /// </summary>
    private static string SanitizeProjectName(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            return "BmadProject";

        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(Path.GetInvalidPathChars())
            .Distinct()
            .ToArray();

        var sanitized = string.Concat(projectName
            .Replace(' ', '_')
            .Select(c => invalidChars.Contains(c) ? '_' : c));

        return string.IsNullOrWhiteSpace(sanitized) ? "BmadProject" : sanitized;
    }
}
