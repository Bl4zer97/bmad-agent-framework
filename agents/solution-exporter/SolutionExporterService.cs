using System.Text;
using System.Text.RegularExpressions;

namespace BmadAgentFramework.Agents.SolutionExporter;

/// <summary>
/// Modello che rappresenta un progetto .NET all'interno della solution,
/// come definito dall'Architect Agent nel blocco <c>solution-structure</c>.
/// </summary>
/// <param name="Name">Nome del progetto (es. "MyApp.Domain")</param>
/// <param name="Sdk">SDK MSBuild (es. "Microsoft.NET.Sdk" o "Microsoft.NET.Sdk.Web")</param>
/// <param name="References">Nomi dei progetti referenziati</param>
/// <param name="Folders">Cartelle suggerite dall'Architect per organizzare i file</param>
public record SolutionProjectInfo(
    string Name,
    string Sdk,
    string[] References,
    string[] Folders)
{
    /// <summary>
    /// Pacchetti NuGet richiesti dal progetto (tuple Nome/Versione).
    /// Definiti dall'Architect nella riga <c>NuGetPackages:</c> del blocco solution-structure,
    /// oppure inferiti automaticamente dai namespace <c>using</c> presenti nel codice.
    /// </summary>
    public (string PackageName, string Version)[] NuGetPackages { get; init; } = [];
};

/// <summary>
/// Servizio deterministico per il parsing del Markdown prodotto dal DeveloperAgent
/// e la scrittura su disco di una .NET solution strutturata multi-progetto.
/// Non effettua chiamate AI — zero costi aggiuntivi.
///
/// Principio: l'Architect è SENIOR sulla struttura. Il SolutionExporter ESEGUE, non decide.
/// Se è disponibile un blocco <c>solution-structure</c> nell'architettura, viene usato
/// come fonte di verità. Altrimenti si inferisce la struttura dai path dei file generati.
/// </summary>
public static class SolutionExporterService
{
    /// <summary>
    /// Estrae tutti i blocchi di codice da un documento Markdown.
    /// Cerca blocchi C# (```csharp, ```cs), JSON (```json) e XML (```xml).
    ///
    /// Strategia per determinare il path del file (in ordine di priorità):
    /// 1. Heading Markdown <c>### path/to/File.cs</c> nella riga precedente al blocco
    /// 2. Commento <c>// File: path/File.cs</c> nella prima riga del blocco (fallback, solo C#)
    /// 3. Nome generico <c>GeneratedCodeN.cs</c> se nessuno dei precedenti è trovato
    ///
    /// Il path restituito può essere un path relativo completo (es. <c>src/MyApp.Domain/Entities/Todo.cs</c>).
    /// </summary>
    /// <param name="markdownContent">Contenuto Markdown dell'artefatto "code"</param>
    /// <returns>Lista di tuple (RelativePath, Content)</returns>
    public static List<(string FileName, string Content)> ExtractCodeBlocks(string markdownContent)
    {
        var results = new List<(string FileName, string Content)>();
        var blockPattern = new Regex(
            @"```(?<lang>csharp|cs|json|xml)\s*\n(?<content>.*?)```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var matches = blockPattern.Matches(markdownContent);
        int genericCounter = 1;

        foreach (Match match in matches)
        {
            var lang = match.Groups["lang"].Value.ToLowerInvariant();
            var blockContent = match.Groups["content"].Value.TrimEnd();

            if (string.IsNullOrWhiteSpace(blockContent))
                continue;

            string fileName;

            // 1. Cerca un heading Markdown "### path/to/File.ext" immediatamente prima del blocco
            var textBefore = markdownContent[..match.Index];
            var lastNonEmptyLine = textBefore
                .TrimEnd()
                .Split('\n')
                .LastOrDefault()
                ?.Trim() ?? string.Empty;

            // Accetta .cs, .json, .xml nel heading
            var headingMatch = Regex.Match(lastNonEmptyLine, @"^#{1,6}\s+(.+\.(?:cs|json|xml))\s*$");
            if (headingMatch.Success)
            {
                fileName = headingMatch.Groups[1].Value.Trim();
            }
            else if (lang is "csharp" or "cs")
            {
                // 2. Fallback: cerca "// File: path/File.cs" nella prima riga del blocco (solo C#)
                var firstLine = blockContent.Split('\n')[0].Trim();
                var fileCommentMatch = Regex.Match(firstLine, @"^//\s*File:\s*(.+\.cs)\s*$");

                if (fileCommentMatch.Success)
                {
                    fileName = fileCommentMatch.Groups[1].Value.Trim();
                    // Rimuove la riga con il commento dal contenuto del file
                    var newlineIndex = blockContent.IndexOf('\n');
                    blockContent = newlineIndex >= 0
                        ? blockContent[(newlineIndex + 1)..].TrimStart('\r', '\n')
                        : string.Empty;

                    if (string.IsNullOrWhiteSpace(blockContent))
                        continue;
                }
                else
                {
                    // 3. Nome generico
                    fileName = $"GeneratedCode{genericCounter++}.cs";
                }
            }
            else
            {
                // Blocco JSON/XML senza heading: ignora (non ha senso senza path)
                continue;
            }

            results.Add((fileName, blockContent));
        }

        return results;
    }

    /// <summary>
    /// Parsare il blocco <c>solution-structure</c> dal documento di architettura.
    /// Il blocco deve essere in formato:
    /// <code>
    /// ```solution-structure
    /// SOLUTION: NomeSolution
    /// PROJECTS:
    /// - Name: MyApp.Domain | SDK: Microsoft.NET.Sdk | References: (nessuno)
    ///   Folders: Entities/, ValueObjects/
    /// - Name: MyApp.API | SDK: Microsoft.NET.Sdk.Web | References: MyApp.Application
    ///   Folders: Controllers/, Middleware/
    /// ```
    /// </code>
    /// </summary>
    /// <param name="architectureContent">Contenuto del documento di architettura</param>
    /// <returns>Tuple con nome solution e lista di progetti; lista vuota se il blocco non è trovato</returns>
    public static (string SolutionName, List<SolutionProjectInfo> Projects) ParseSolutionStructure(
        string architectureContent)
    {
        var blockMatch = Regex.Match(
            architectureContent,
            @"```solution-structure\s*\n(.*?)```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (!blockMatch.Success)
            return (string.Empty, []);

        var block = blockMatch.Groups[1].Value;

        // Estrae il nome della solution
        var solutionNameMatch = Regex.Match(block, @"^SOLUTION:\s*(.+)$", RegexOptions.Multiline);
        var solutionName = solutionNameMatch.Success
            ? solutionNameMatch.Groups[1].Value.Trim()
            : string.Empty;

        // Pattern per ogni riga progetto
        var projectLinePattern = new Regex(
            @"^\s*-\s*Name:\s*(?<name>[^|]+)\|\s*SDK:\s*(?<sdk>[^|]+)\|\s*References:\s*(?<refs>.+)$",
            RegexOptions.Multiline);

        var projects = new List<SolutionProjectInfo>();
        var lines = block.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var projectMatch = projectLinePattern.Match(lines[i]);
            if (!projectMatch.Success)
                continue;

            var name = projectMatch.Groups["name"].Value.Trim();
            var sdk = projectMatch.Groups["sdk"].Value.Trim();
            var refsText = projectMatch.Groups["refs"].Value.Trim();

            var refs = refsText.Equals("(nessuno)", StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<string>()
                : refsText.Split(',')
                    .Select(r => r.Trim())
                    .Where(r => r.Length > 0)
                    .ToArray();

            // Cerca la riga Folders subito dopo
            string[] folders = [];
            (string PackageName, string Version)[] nugetPackages = [];
            int offset = 1;

            if (i + offset < lines.Length)
            {
                var foldersMatch = Regex.Match(
                    lines[i + offset],
                    @"^\s+Folders:\s*(.+)$");
                if (foldersMatch.Success)
                {
                    folders = foldersMatch.Groups[1].Value
                        .Split(',')
                        .Select(f => f.Trim().TrimEnd('/'))
                        .Where(f => f.Length > 0)
                        .ToArray();
                    offset++;
                }
            }

            // Cerca la riga NuGetPackages subito dopo Folders (o dopo la riga progetto)
            if (i + offset < lines.Length)
            {
                var nugetMatch = Regex.Match(
                    lines[i + offset],
                    @"^\s+NuGetPackages:\s*(.+)$");
                if (nugetMatch.Success)
                {
                    nugetPackages = nugetMatch.Groups[1].Value
                        .Split(',')
                        .Select(p =>
                        {
                            var parts = p.Trim().Split('/');
                            return parts.Length == 2
                                ? (PackageName: parts[0].Trim(), Version: parts[1].Trim())
                                : (PackageName: parts[0].Trim(), Version: string.Empty);
                        })
                        .Where(p => p.PackageName.Length > 0)
                        .ToArray();
                }
            }

            projects.Add(new SolutionProjectInfo(name, sdk, refs, folders)
            {
                NuGetPackages = nugetPackages
            });
        }

        return (solutionName, projects);
    }

    /// <summary>
    /// Scrive la solution .NET su disco nella cartella di output specificata.
    ///
    /// Quando è disponibile l'architettura con un blocco <c>solution-structure</c>:
    /// - Crea i .csproj per ogni progetto con i <c>ProjectReference</c> corretti
    /// - Organizza i file nelle cartelle corrette
    /// - Genera un .sln con tutti i progetti registrati
    ///
    /// Fallback (senza architettura):
    /// - Inferisce i progetti dai path dei file generati
    /// - Genera un .sln con i progetti inferiti
    /// </summary>
    /// <param name="outputBasePath">Percorso base della cartella di output</param>
    /// <param name="projectName">Nome del progetto (usato per cartelle e file)</param>
    /// <param name="files">Lista di file C# con path relativo e contenuto</param>
    /// <param name="architectureContent">Contenuto del documento di architettura (opzionale)</param>
    /// <returns>Percorso assoluto della cartella solution generata</returns>
    public static string WriteSolutionToDisk(
        string outputBasePath,
        string projectName,
        List<(string FileName, string Content)> files,
        string? architectureContent = null)
    {
        var safeName = SanitizeProjectName(projectName);
        var solutionDir = Path.Combine(outputBasePath, $"{safeName}-solution");
        Directory.CreateDirectory(solutionDir);

        // Step 1: Parsare la struttura dall'architettura (se disponibile)
        List<SolutionProjectInfo> parsedProjects = [];
        var parsedSolutionName = safeName;

        if (!string.IsNullOrWhiteSpace(architectureContent))
        {
            var (sName, projects) = ParseSolutionStructure(architectureContent);
            if (projects.Count > 0)
            {
                parsedProjects = projects;
                if (!string.IsNullOrEmpty(sName))
                    parsedSolutionName = SanitizeProjectName(sName);
            }
        }

        // Step 2: Scrivere tutti i file C# ai loro path relativi
        // I file senza path prefix vanno nella directory di default: src/{safeName}/
        var defaultProjectDir = Path.Combine(solutionDir, "src", safeName);

        foreach (var (fileName, content) in files)
        {
            var normalized = fileName.Replace('\\', '/');
            string targetPath;

            if (normalized.Contains('/'))
            {
                // Ha un path relativo (es. src/MyApp.Domain/Entities/Todo.cs)
                targetPath = Path.Combine(
                    solutionDir,
                    normalized.Replace('/', Path.DirectorySeparatorChar));
            }
            else
            {
                // Nessun path prefix: va nel progetto di default
                targetPath = Path.Combine(defaultProjectDir, normalized);
            }

            var dir = Path.GetDirectoryName(targetPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            File.WriteAllText(targetPath, content, Encoding.UTF8);
        }

        // Step 3: Creare i .csproj e raccogliere i progetti per il .sln
        List<(SolutionProjectInfo Project, string CsprojRelativePath)> projectsForSln;

        if (parsedProjects.Count > 0)
        {
            // Applica NuGet inference come fallback sui progetti che non hanno NuGet definiti
            var allCode = string.Join("\n", files.Select(f => f.Content));
            parsedProjects = parsedProjects
                .Select(p => p.NuGetPackages.Length == 0
                    ? p with { NuGetPackages = InferNuGetPackagesFromCode(allCode) }
                    : p)
                .ToList();

            projectsForSln = CreateProjectsFromStructure(solutionDir, parsedProjects);
        }
        else
        {
            // Senza architettura: inferisce NuGet dal codice e li applica a tutti i progetti
            var allCode = string.Join("\n", files.Select(f => f.Content));
            var inferredNugets = InferNuGetPackagesFromCode(allCode);
            projectsForSln = InferAndCreateProjects(solutionDir, files, safeName, inferredNugets);
        }

        // Step 4: Generare il file .sln con tutti i progetti
        var slnContent = GenerateMultiProjectSln(parsedSolutionName, projectsForSln);
        var slnPath = Path.Combine(solutionDir, $"{parsedSolutionName}.sln");
        File.WriteAllText(slnPath, slnContent, Encoding.UTF8);

        // Step 5: Generare il README.md
        var projectNames = projectsForSln.Select(p => p.Project.Name).ToList();
        var readmeContent = GenerateReadme(safeName, projectNames, parsedSolutionName);
        File.WriteAllText(Path.Combine(solutionDir, "README.md"), readmeContent, Encoding.UTF8);

        return solutionDir;
    }

    /// <summary>
    /// Crea i .csproj per ogni progetto definito nella struttura parsata dell'Architect.
    /// Crea anche le sottocartelle definite dall'Architect con un file <c>.gitkeep</c>
    /// nelle cartelle che non contengono file .cs generati.
    /// </summary>
    private static List<(SolutionProjectInfo Project, string CsprojRelativePath)> CreateProjectsFromStructure(
        string solutionDir,
        List<SolutionProjectInfo> projects)
    {
        var result = new List<(SolutionProjectInfo Project, string CsprojRelativePath)>();

        foreach (var project in projects)
        {
            var isTest = IsTestProject(project.Name);
            var baseDir = isTest ? "tests" : "src";
            var projectRelDir = $"{baseDir}/{project.Name}";
            var projectFullDir = Path.Combine(
                solutionDir,
                projectRelDir.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(projectFullDir);

            // Crea le sottocartelle definite dall'Architect, con .gitkeep nelle cartelle vuote
            foreach (var folder in project.Folders)
            {
                var subFolderPath = Path.Combine(projectFullDir, folder);
                Directory.CreateDirectory(subFolderPath);

                var gitkeepPath = Path.Combine(subFolderPath, ".gitkeep");
                if (!File.Exists(gitkeepPath) && !Directory.EnumerateFiles(subFolderPath).Any())
                    File.WriteAllText(gitkeepPath, string.Empty, Encoding.UTF8);
            }

            var csprojContent = GenerateCsprojForProject(project, isTest);
            var csprojFileName = $"{project.Name}.csproj";
            File.WriteAllText(
                Path.Combine(projectFullDir, csprojFileName),
                csprojContent,
                Encoding.UTF8);

            result.Add((project, $"{projectRelDir}/{csprojFileName}"));
        }

        return result;
    }

    /// <summary>
    /// Inferisce i progetti dalla struttura dei path dei file generati
    /// (fallback quando non è disponibile un blocco <c>solution-structure</c>).
    /// </summary>
    private static List<(SolutionProjectInfo Project, string CsprojRelativePath)> InferAndCreateProjects(
        string solutionDir,
        List<(string FileName, string Content)> files,
        string defaultProjectName,
        (string PackageName, string Version)[]? inferredNugets = null)
    {
        var result = new List<(SolutionProjectInfo Project, string CsprojRelativePath)>();
        var projectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasUnpathedFiles = false;

        foreach (var (fileName, _) in files)
        {
            var normalized = fileName.Replace('\\', '/');
            var parts = normalized.Split('/');

            if (parts.Length >= 3 && (parts[0] == "src" || parts[0] == "tests"))
                projectPaths.Add($"{parts[0]}/{parts[1]}");
            else if (!normalized.Contains('/'))
                hasUnpathedFiles = true;
        }

        // I file senza path prefix vanno nel progetto di default
        if (hasUnpathedFiles || projectPaths.Count == 0)
            projectPaths.Add($"src/{defaultProjectName}");

        foreach (var projectPath in projectPaths.OrderBy(p => p))
        {
            var parts = projectPath.Split('/');
            var isTest = parts[0] == "tests";
            var projName = parts[1];

            var projectFullDir = Path.Combine(
                solutionDir,
                projectPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(projectFullDir);

            var project = new SolutionProjectInfo(projName, "Microsoft.NET.Sdk", [], [])
            {
                NuGetPackages = inferredNugets ?? []
            };
            var csprojContent = GenerateCsprojForProject(project, isTest);
            var csprojFileName = $"{projName}.csproj";
            File.WriteAllText(
                Path.Combine(projectFullDir, csprojFileName),
                csprojContent,
                Encoding.UTF8);

            result.Add((project, $"{projectPath}/{csprojFileName}"));
        }

        return result;
    }

    /// <summary>
    /// Genera il contenuto di un file .csproj per un progetto della solution.
    /// Include i <c>ProjectReference</c> corretti in base alla posizione del progetto
    /// (src/ per i sorgenti, tests/ per i test).
    /// </summary>
    /// <param name="project">Informazioni sul progetto (nome, SDK, riferimenti)</param>
    /// <param name="isTest">True se il progetto è un progetto di test</param>
    /// <returns>Contenuto XML del file .csproj</returns>
    public static string GenerateCsprojForProject(SolutionProjectInfo project, bool isTest)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""<Project Sdk="{project.Sdk}">""");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <TargetFramework>net8.0</TargetFramework>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");

        if (isTest)
        {
            sb.AppendLine("    <IsPackable>false</IsPackable>");
            sb.AppendLine("    <IsTestProject>true</IsTestProject>");
        }

        sb.AppendLine("  </PropertyGroup>");

        if (project.References.Length > 0)
        {
            sb.AppendLine("  <ItemGroup>");
            foreach (var reference in project.References)
            {
                // I progetti di test sono in tests/, i sorgenti in src/
                // Quindi i riferimenti da tests/ a src/ richiedono ../../src/
                var relPath = isTest
                    ? $"../../src/{reference}/{reference}.csproj"
                    : $"../{reference}/{reference}.csproj";
                sb.AppendLine($"""    <ProjectReference Include="{relPath}" />""");
            }

            sb.AppendLine("  </ItemGroup>");
        }

        // Aggiunge i PackageReference NuGet definiti dall'Architect o inferiti dal codice
        var nugets = project.NuGetPackages ?? [];
        if (nugets.Length > 0)
        {
            sb.AppendLine("  <ItemGroup>");
            foreach (var (packageName, version) in nugets)
            {
                if (string.IsNullOrWhiteSpace(version))
                    sb.AppendLine($"""    <PackageReference Include="{packageName}" />""");
                else
                    sb.AppendLine($"""    <PackageReference Include="{packageName}" Version="{version}" />""");
            }

            sb.AppendLine("  </ItemGroup>");
        }

        sb.Append("</Project>");
        return sb.ToString();
    }

    /// <summary>
    /// Mappa statica da prefisso namespace a pacchetto NuGet e versione.
    /// Usata da <see cref="InferNuGetPackagesFromCode"/> per determinare i pacchetti
    /// necessari analizzando le direttive <c>using</c> nel codice C# generato.
    /// </summary>
    private static readonly Dictionary<string, (string PackageName, string Version)> NamespaceToNuGetMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MediatR"]                                                         = ("MediatR", "12.4.0"),
            ["FluentValidation.AspNetCore"]                                     = ("FluentValidation.AspNetCore", "11.3.0"),
            ["FluentValidation"]                                                = ("FluentValidation", "11.9.2"),
            ["Microsoft.EntityFrameworkCore.SqlServer"]                         = ("Microsoft.EntityFrameworkCore.SqlServer", "8.0.11"),
            ["Microsoft.EntityFrameworkCore.Design"]                            = ("Microsoft.EntityFrameworkCore.Design", "8.0.11"),
            ["Microsoft.EntityFrameworkCore"]                                   = ("Microsoft.EntityFrameworkCore", "8.0.11"),
            ["Azure.Identity"]                                                  = ("Azure.Identity", "1.13.1"),
            ["Azure.Security.KeyVault"]                                         = ("Azure.Security.KeyVault.Secrets", "4.6.0"),
            ["Azure.Messaging.ServiceBus"]                                      = ("Azure.Messaging.ServiceBus", "7.18.2"),
            ["Azure.Storage.Blobs"]                                             = ("Azure.Storage.Blobs", "12.22.2"),
            ["Azure.AI.Projects"]                                               = ("Azure.AI.Projects", "1.0.0-beta.6"),
            ["Telegram.Bot"]                                                    = ("Telegram.Bot", "21.3.1"),
            ["Swashbuckle"]                                                     = ("Swashbuckle.AspNetCore", "6.9.0"),
            ["Polly"]                                                           = ("Polly", "8.5.0"),
            ["Serilog"]                                                         = ("Serilog.AspNetCore", "8.0.3"),
            ["AutoMapper"]                                                      = ("AutoMapper", "13.0.1"),
            ["Mapster"]                                                         = ("Mapster", "7.4.0"),
            ["Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore"]     = ("Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore", "1.3.2"),
            ["Microsoft.Azure.Functions.Worker.Extensions.Http"]                = ("Microsoft.Azure.Functions.Worker.Extensions.Http", "3.2.0"),
            ["Microsoft.Azure.Functions.Worker.Sdk"]                            = ("Microsoft.Azure.Functions.Worker.Sdk", "1.18.1"),
            ["Microsoft.Azure.Functions.Worker"]                                = ("Microsoft.Azure.Functions.Worker", "1.23.0"),
            ["Xunit"]                                                           = ("xunit", "2.9.2"),
            ["xunit"]                                                           = ("xunit", "2.9.2"),
            ["Microsoft.NET.Test.Sdk"]                                          = ("Microsoft.NET.Test.Sdk", "17.12.0"),
            ["FluentAssertions"]                                                = ("FluentAssertions", "6.12.2"),
            ["Moq"]                                                             = ("Moq", "4.20.72"),
            ["NSubstitute"]                                                     = ("NSubstitute", "5.3.0"),
        };

    /// <summary>
    /// Analizza le direttive <c>using</c> nel codice C# e mappa i namespace noti
    /// ai corrispondenti pacchetti NuGet.
    /// Usato come fallback deterministico quando l'Architect non ha specificato i NuGet
    /// nel blocco <c>solution-structure</c>.
    /// </summary>
    /// <param name="csharpCode">Contenuto del codice C# (uno o più file)</param>
    /// <returns>Array di tuple (PackageName, Version) per i pacchetti rilevati</returns>
    public static (string PackageName, string Version)[] InferNuGetPackagesFromCode(string csharpCode)
    {
        if (string.IsNullOrWhiteSpace(csharpCode))
            return [];

        // Estrae tutti i namespace dalle direttive using
        var usingPattern = new Regex(@"^\s*using\s+([\w.]+)\s*;", RegexOptions.Multiline);
        var usedNamespaces = usingPattern.Matches(csharpCode)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, (string PackageName, string Version)>(StringComparer.OrdinalIgnoreCase);

        foreach (var usedNs in usedNamespaces)
        {
            // Cerca prima il match esatto, poi per prefisso (più lungo vince)
            // Ordine: le chiavi più specifiche (più lunghe) hanno precedenza
            foreach (var (nsPrefix, package) in NamespaceToNuGetMap
                .OrderByDescending(kv => kv.Key.Length))
            {
                if (usedNs.StartsWith(nsPrefix, StringComparison.OrdinalIgnoreCase)
                    && !result.ContainsKey(package.PackageName))
                {
                    result[package.PackageName] = package;
                    break;
                }
            }
        }

        return [.. result.Values];
    }

    /// <summary>
    /// Genera il contenuto di un file .sln compatibile con Visual Studio
    /// che registra tutti i progetti della solution.
    /// </summary>
    /// <param name="solutionName">Nome della solution</param>
    /// <param name="projects">Lista di progetti con il loro path relativo del .csproj</param>
    /// <returns>Contenuto del file .sln</returns>
    public static string GenerateMultiProjectSln(
        string solutionName,
        List<(SolutionProjectInfo Project, string CsprojRelativePath)> projects)
    {
        // GUID fisso per il tipo progetto C# (.NET Core / SDK-style)
        const string projectTypeGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        var solutionGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

        // Assegna un GUID univoco a ogni progetto
        var projectEntries = projects.Select(p => (
            Guid: Guid.NewGuid().ToString("B").ToUpperInvariant(),
            p.Project.Name,
            SlnPath: p.CsprojRelativePath.Replace('/', '\\')
        )).ToList();

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio Version 17");
        sb.AppendLine("VisualStudioVersion = 17.0.31903.59");
        sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

        foreach (var (guid, name, slnPath) in projectEntries)
        {
            sb.AppendLine($"Project(\"{projectTypeGuid}\") = \"{name}\", \"{slnPath}\", \"{guid}\"");
            sb.AppendLine("EndProject");
        }

        sb.AppendLine("Global");
        sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

        foreach (var (guid, _, _) in projectEntries)
        {
            sb.AppendLine($"\t\t{guid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine($"\t\t{guid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine($"\t\t{guid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine($"\t\t{guid}.Release|Any CPU.Build.0 = Release|Any CPU");
        }

        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
        sb.AppendLine("\t\tHideSolutionNode = FALSE");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(ExtensibilityGlobals) = postSolution");
        sb.AppendLine($"\t\tSolutionGuid = {solutionGuid}");
        sb.AppendLine("\tEndGlobalSection");
        sb.Append("EndGlobal");

        return sb.ToString();
    }

    /// <summary>
    /// Determina se un progetto è un progetto di test in base al nome.
    /// Un progetto è considerato di test se il nome termina con "Tests" o "Test".
    /// </summary>
    private static bool IsTestProject(string projectName) =>
        projectName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
        projectName.EndsWith("Test", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Genera il contenuto di un file .csproj minimale per .NET 8.
    /// Mantenuto per compatibilità; preferire <see cref="GenerateCsprojForProject"/> per i nuovi progetti.
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
    /// Genera il contenuto di un file .sln minimale con un singolo progetto.
    /// Mantenuto per compatibilità; preferire <see cref="GenerateMultiProjectSln"/> per le nuove solution.
    /// </summary>
    /// <param name="projectName">Nome del progetto</param>
    /// <param name="csprojRelativePath">Percorso relativo del .csproj rispetto alla cartella solution</param>
    /// <returns>Contenuto del file .sln</returns>
    public static string GenerateSln(string projectName, string csprojRelativePath)
    {
        var project = new SolutionProjectInfo(projectName, "Microsoft.NET.Sdk", [], []);
        return GenerateMultiProjectSln(projectName, [(project, csprojRelativePath)]);
    }

    /// <summary>
    /// Genera il contenuto del README.md con informazioni sul progetto generato.
    /// </summary>
    private static string GenerateReadme(
        string projectName,
        List<string> projectNames,
        string solutionName)
    {
        var projectList = projectNames.Count > 0
            ? string.Join("\n", projectNames.Select(n => $"│   ├── {n}/"))
            : $"│   └── {projectName}/";

        return $"""
        # {solutionName}

        Progetto generato automaticamente dal **BMAD Agent Framework**.

        ## Struttura

        ```
        {solutionName}-solution/
        ├── {solutionName}.sln
        ├── src/
        {projectList}
        └── README.md
        ```

        ## Come buildare

        ```bash
        dotnet build {solutionName}.sln
        ```

        ## Artefatti BMAD

        - **Requirements**: vedi l'artefatto `requirements` nel contesto di progetto
        - **Architecture**: vedi l'artefatto `architecture` nel contesto di progetto
        - **Code**: vedi l'artefatto `code` nel contesto di progetto
        """;
    }

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
