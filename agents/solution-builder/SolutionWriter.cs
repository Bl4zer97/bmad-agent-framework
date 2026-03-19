using System.Text;
using System.Text.RegularExpressions;

namespace BmadAgentFramework.Agents.SolutionBuilder;

/// <summary>
/// Helper statico per il parsing dei blocchi di codice e la scrittura dei file su disco.
/// Estrae i file C# dal formato markdown prodotto dal DeveloperAgent e li
/// materializza come file effettivi nella cartella di output.
/// </summary>
public static class SolutionWriter
{
    /// <summary>
    /// Analizza il contenuto markdown dell'agente Developer ed estrae le coppie
    /// (percorso file, contenuto codice) dai blocchi ```csharp.
    ///
    /// Formati di intestazione file supportati:
    /// - ### src/Domain/Entities/TodoItem.cs
    /// - ### `src/Domain/Entities/TodoItem.cs`
    /// - **src/Domain/Entities/TodoItem.cs**
    /// </summary>
    public static IReadOnlyList<(string FilePath, string Content)> ParseCodeBlocks(string markdownContent)
    {
        var result = new List<(string FilePath, string Content)>();
        var lines = markdownContent.Split('\n');
        string? currentFilePath = null;
        var codeLines = new List<string>();
        bool inCodeBlock = false;

        foreach (var line in lines)
        {
            if (!inCodeBlock)
            {
                // Detect the start of a C# code block
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("```csharp", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("```cs", StringComparison.OrdinalIgnoreCase))
                {
                    inCodeBlock = true;
                    codeLines.Clear();
                }
                else
                {
                    // Try to detect a heading that contains a file path
                    var filePath = TryExtractFilePath(line);
                    if (filePath is not null)
                        currentFilePath = filePath;
                }
            }
            else
            {
                var trimmed = line.TrimStart();
                // Detect end of code block (``` alone, not ```csharp)
                if (trimmed.StartsWith("```")
                    && !trimmed.StartsWith("```csharp", StringComparison.OrdinalIgnoreCase)
                    && !trimmed.StartsWith("```cs", StringComparison.OrdinalIgnoreCase))
                {
                    inCodeBlock = false;
                    if (codeLines.Count > 0)
                    {
                        var path = currentFilePath ?? DeriveFilePathFromCode(codeLines);
                        result.Add((NormalizePath(path), string.Join("\n", codeLines).Trim()));
                        currentFilePath = null;
                    }
                    codeLines.Clear();
                }
                else
                {
                    codeLines.Add(line);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Scrive i file estratti nella directory di output indicata,
    /// ricreando la struttura di sottocartelle specificata nei path.
    /// </summary>
    public static async Task WriteFilesAsync(
        string outputDirectory,
        IReadOnlyList<(string FilePath, string Content)> files,
        CancellationToken ct = default)
    {
        foreach (var (filePath, content) in files)
        {
            ct.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(outputDirectory, filePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(fullPath, content, ct);
        }
    }

    /// <summary>
    /// Genera il contenuto di un file .csproj minimale per il progetto.
    /// Include i package NuGet più comuni per un'applicazione .NET 8 con Clean Architecture.
    /// </summary>
    public static string GenerateProjectFile(string projectName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk.Web\">");
        sb.AppendLine();
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <TargetFramework>net8.0</TargetFramework>");
        sb.AppendLine($"    <AssemblyName>{projectName}</AssemblyName>");
        sb.AppendLine($"    <RootNamespace>{projectName}</RootNamespace>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        sb.AppendLine("    <LangVersion>12</LangVersion>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine();
        sb.AppendLine("  <ItemGroup>");
        sb.AppendLine("    <PackageReference Include=\"Microsoft.EntityFrameworkCore.SqlServer\" Version=\"8.0.0\" />");
        sb.AppendLine("    <PackageReference Include=\"Microsoft.EntityFrameworkCore.Tools\" Version=\"8.0.0\">");
        sb.AppendLine("      <PrivateAssets>all</PrivateAssets>");
        sb.AppendLine("      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>");
        sb.AppendLine("    </PackageReference>");
        sb.AppendLine("    <PackageReference Include=\"Swashbuckle.AspNetCore\" Version=\"6.8.1\" />");
        sb.AppendLine("    <PackageReference Include=\"FluentValidation.AspNetCore\" Version=\"11.3.0\" />");
        sb.AppendLine("    <PackageReference Include=\"MediatR\" Version=\"12.4.1\" />");
        sb.AppendLine("    <PackageReference Include=\"Serilog.AspNetCore\" Version=\"8.0.3\" />");
        sb.AppendLine("  </ItemGroup>");
        sb.AppendLine();
        sb.AppendLine("</Project>");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Prova a estrarre un percorso file da una riga di testo markdown.
    /// </summary>
    private static string? TryExtractFilePath(string line)
    {
        // Heading with optional backtick: ### `src/Domain/Entities/TodoItem.cs`
        // or without backtick:            ### src/Domain/Entities/TodoItem.cs
        var headingMatch = Regex.Match(
            line,
            @"^#{1,4}\s+`?([^`\s]+\.(?:cs|csproj|json|yaml|yml|md))`?\s*$");
        if (headingMatch.Success)
            return headingMatch.Groups[1].Value;

        // Bold markdown: **src/Domain/Entities/TodoItem.cs**
        var boldMatch = Regex.Match(
            line,
            @"^\*\*([^*\s]+\.(?:cs|csproj|json|yaml|yml|md))\*\*\s*$");
        if (boldMatch.Success)
            return boldMatch.Groups[1].Value;

        return null;
    }

    /// <summary>
    /// Deduce il nome del file dal contenuto del codice cercando la prima dichiarazione
    /// di tipo pubblico (class, interface, record, enum, struct).
    /// </summary>
    private static string DeriveFilePathFromCode(IList<string> codeLines)
    {
        foreach (var line in codeLines)
        {
            var match = Regex.Match(
                line,
                @"public\s+(?:partial\s+)?(?:class|interface|record|enum|struct)\s+(\w+)");
            if (match.Success)
                return $"{match.Groups[1].Value}.cs";
        }
        return $"GeneratedCode_{Guid.NewGuid():N}.cs";
    }

    /// <summary>
    /// Normalizza il separatore di percorso in base al sistema operativo corrente.
    /// </summary>
    private static string NormalizePath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
}
