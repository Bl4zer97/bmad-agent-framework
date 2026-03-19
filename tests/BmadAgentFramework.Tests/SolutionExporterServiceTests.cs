using BmadAgentFramework.Agents.SolutionExporter;
using FluentAssertions;
using Xunit;

namespace BmadAgentFramework.Tests.Unit;

/// <summary>
/// Test unitari per <see cref="SolutionExporterService"/>.
/// Verifica il parsing del Markdown, della struttura dell'architettura
/// e la scrittura multi-progetto su disco.
/// </summary>
public class SolutionExporterServiceTests
{
    // ========================================================================
    // TEST EXTRACTCODEBLOCKS
    // ========================================================================

    [Fact]
    public void ExtractCodeBlocks_ShouldExtractFile_WhenMarkdownHeadingPresent()
    {
        // Arrange
        var markdown = """
            ### src/MyApp.Domain/Entities/TodoItem.cs
            ```csharp
            namespace MyApp.Domain.Entities;
            public record TodoItem(int Id, string Title);
            ```
            """;

        // Act
        var blocks = SolutionExporterService.ExtractCodeBlocks(markdown);

        // Assert
        blocks.Should().HaveCount(1);
        blocks[0].FileName.Should().Be("src/MyApp.Domain/Entities/TodoItem.cs");
        blocks[0].Content.Should().Contain("TodoItem");
    }

    [Fact]
    public void ExtractCodeBlocks_ShouldExtractFile_WhenFileCommentPresent()
    {
        // Arrange
        var markdown = """
            Ecco il codice:
            ```csharp
            // File: src/MyApp.Domain/Entities/Order.cs
            namespace MyApp.Domain.Entities;
            public record Order(int Id);
            ```
            """;

        // Act
        var blocks = SolutionExporterService.ExtractCodeBlocks(markdown);

        // Assert
        blocks.Should().HaveCount(1);
        blocks[0].FileName.Should().Be("src/MyApp.Domain/Entities/Order.cs");
        blocks[0].Content.Should().Contain("namespace MyApp.Domain.Entities");
        blocks[0].Content.Should().NotContain("// File:", "il commento file deve essere rimosso dal contenuto");
    }

    [Fact]
    public void ExtractCodeBlocks_ShouldAssignGenericName_WhenNeitherHeadingNorCommentPresent()
    {
        // Arrange
        var markdown = """
            Codice senza identificatore:
            ```csharp
            public class SomeClass { }
            ```
            """;

        // Act
        var blocks = SolutionExporterService.ExtractCodeBlocks(markdown);

        // Assert
        blocks.Should().HaveCount(1);
        blocks[0].FileName.Should().Be("GeneratedCode1.cs");
    }

    [Fact]
    public void ExtractCodeBlocks_ShouldPreferMarkdownHeading_OverFileComment()
    {
        // Arrange - heading ha precedenza sul commento
        var markdown = """
            ### src/MyApp.Domain/Entities/Priority.cs
            ```csharp
            // File: src/OtherPath/Priority.cs
            namespace MyApp.Domain.Entities;
            public enum Priority { Low, Medium, High }
            ```
            """;

        // Act
        var blocks = SolutionExporterService.ExtractCodeBlocks(markdown);

        // Assert
        blocks.Should().HaveCount(1);
        blocks[0].FileName.Should().Be("src/MyApp.Domain/Entities/Priority.cs",
            "il heading Markdown ha precedenza sul commento // File:");
    }

    [Fact]
    public void ExtractCodeBlocks_ShouldExtractMultipleBlocks()
    {
        // Arrange
        var markdown = """
            ### src/MyApp.Domain/Entities/Todo.cs
            ```csharp
            namespace MyApp.Domain.Entities;
            public record Todo(int Id, string Title);
            ```

            ### src/MyApp.Application/Services/TodoService.cs
            ```csharp
            namespace MyApp.Application.Services;
            public class TodoService { }
            ```

            ### tests/MyApp.Tests/Unit/TodoTests.cs
            ```csharp
            namespace MyApp.Tests.Unit;
            public class TodoTests { }
            ```
            """;

        // Act
        var blocks = SolutionExporterService.ExtractCodeBlocks(markdown);

        // Assert
        blocks.Should().HaveCount(3);
        blocks.Should().Contain(b => b.FileName == "src/MyApp.Domain/Entities/Todo.cs");
        blocks.Should().Contain(b => b.FileName == "src/MyApp.Application/Services/TodoService.cs");
        blocks.Should().Contain(b => b.FileName == "tests/MyApp.Tests/Unit/TodoTests.cs");
    }

    [Fact]
    public void ExtractCodeBlocks_ShouldSkipEmptyBlocks()
    {
        // Arrange
        var markdown = """
            ### src/Empty.cs
            ```csharp
            ```
            """;

        // Act
        var blocks = SolutionExporterService.ExtractCodeBlocks(markdown);

        // Assert
        blocks.Should().BeEmpty("i blocchi vuoti devono essere ignorati");
    }

    [Fact]
    public void ExtractCodeBlocks_ShouldHandleEmptyInput()
    {
        // Act
        var blocks = SolutionExporterService.ExtractCodeBlocks(string.Empty);

        // Assert
        blocks.Should().BeEmpty();
    }

    // ========================================================================
    // TEST PARSESOLUTIONSTRUCTURE
    // ========================================================================

    [Fact]
    public void ParseSolutionStructure_ShouldParseCorrectly_WhenBlockPresent()
    {
        // Arrange
        var architecture = """
            ## 9. Struttura del Progetto .NET

            ```solution-structure
            SOLUTION: MyTodoApp
            PROJECTS:
            - Name: MyTodoApp.Domain | SDK: Microsoft.NET.Sdk | References: (nessuno)
              Folders: Entities/, ValueObjects/
            - Name: MyTodoApp.Application | SDK: Microsoft.NET.Sdk | References: MyTodoApp.Domain
              Folders: Services/, DTOs/
            - Name: MyTodoApp.API | SDK: Microsoft.NET.Sdk.Web | References: MyTodoApp.Application
              Folders: Controllers/
            - Name: MyTodoApp.Tests | SDK: Microsoft.NET.Sdk | References: MyTodoApp.Domain, MyTodoApp.Application
              Folders: Unit/, Integration/
            ```
            """;

        // Act
        var (solutionName, projects) = SolutionExporterService.ParseSolutionStructure(architecture);

        // Assert
        solutionName.Should().Be("MyTodoApp");
        projects.Should().HaveCount(4);

        var domain = projects.Should().ContainSingle(p => p.Name == "MyTodoApp.Domain").Subject;
        domain.Sdk.Should().Be("Microsoft.NET.Sdk");
        domain.References.Should().BeEmpty("Domain non ha riferimenti");
        domain.Folders.Should().Contain("Entities");

        var app = projects.Should().ContainSingle(p => p.Name == "MyTodoApp.Application").Subject;
        app.References.Should().ContainSingle().Which.Should().Be("MyTodoApp.Domain");

        var api = projects.Should().ContainSingle(p => p.Name == "MyTodoApp.API").Subject;
        api.Sdk.Should().Be("Microsoft.NET.Sdk.Web");

        var tests = projects.Should().ContainSingle(p => p.Name == "MyTodoApp.Tests").Subject;
        tests.References.Should().HaveCount(2);
        tests.References.Should().Contain("MyTodoApp.Domain");
        tests.References.Should().Contain("MyTodoApp.Application");
    }

    [Fact]
    public void ParseSolutionStructure_ShouldReturnEmpty_WhenBlockNotPresent()
    {
        // Arrange
        var architecture = "## 9. Struttura\n\nCartelle consigliate: Domain, Application";

        // Act
        var (solutionName, projects) = SolutionExporterService.ParseSolutionStructure(architecture);

        // Assert
        solutionName.Should().BeEmpty();
        projects.Should().BeEmpty();
    }

    // ========================================================================
    // TEST GENERATECSPROJFORPROJECT
    // ========================================================================

    [Fact]
    public void GenerateCsprojForProject_ShouldIncludeCorrectSdk()
    {
        // Arrange
        var project = new SolutionProjectInfo("MyApp.API", "Microsoft.NET.Sdk.Web", [], []);

        // Act
        var csproj = SolutionExporterService.GenerateCsprojForProject(project, false);

        // Assert
        csproj.Should().Contain("Microsoft.NET.Sdk.Web");
        csproj.Should().Contain("net8.0");
    }

    [Fact]
    public void GenerateCsprojForProject_ShouldIncludeProjectReferences_ForSourceProject()
    {
        // Arrange
        var project = new SolutionProjectInfo(
            "MyApp.Application",
            "Microsoft.NET.Sdk",
            ["MyApp.Domain"],
            []);

        // Act
        var csproj = SolutionExporterService.GenerateCsprojForProject(project, false);

        // Assert
        csproj.Should().Contain("ProjectReference");
        csproj.Should().Contain("../MyApp.Domain/MyApp.Domain.csproj",
            "i sorgenti src/ usano percorsi relativi ../");
    }

    [Fact]
    public void GenerateCsprojForProject_ShouldUseDeepRelativePath_ForTestProject()
    {
        // Arrange
        var project = new SolutionProjectInfo(
            "MyApp.Tests",
            "Microsoft.NET.Sdk",
            ["MyApp.Domain", "MyApp.Application"],
            []);

        // Act
        var csproj = SolutionExporterService.GenerateCsprojForProject(project, isTest: true);

        // Assert
        csproj.Should().Contain("IsPackable");
        csproj.Should().Contain("IsTestProject");
        csproj.Should().Contain("../../src/MyApp.Domain/MyApp.Domain.csproj",
            "i test in tests/ usano percorsi ../../src/ per referenziare i sorgenti");
        csproj.Should().Contain("../../src/MyApp.Application/MyApp.Application.csproj");
    }

    [Fact]
    public void GenerateCsprojForProject_ShouldNotIncludeItemGroup_WhenNoReferences()
    {
        // Arrange
        var project = new SolutionProjectInfo("MyApp.Domain", "Microsoft.NET.Sdk", [], []);

        // Act
        var csproj = SolutionExporterService.GenerateCsprojForProject(project, false);

        // Assert
        csproj.Should().NotContain("ItemGroup");
        csproj.Should().NotContain("ProjectReference");
    }

    // ========================================================================
    // TEST GENERATEMULTIPROJECTSLN
    // ========================================================================

    [Fact]
    public void GenerateMultiProjectSln_ShouldIncludeAllProjects()
    {
        // Arrange
        var projects = new List<(SolutionProjectInfo Project, string CsprojRelativePath)>
        {
            (new SolutionProjectInfo("MyApp.Domain", "Microsoft.NET.Sdk", [], []),
                "src/MyApp.Domain/MyApp.Domain.csproj"),
            (new SolutionProjectInfo("MyApp.API", "Microsoft.NET.Sdk.Web", [], []),
                "src/MyApp.API/MyApp.API.csproj"),
        };

        // Act
        var sln = SolutionExporterService.GenerateMultiProjectSln("MyApp", projects);

        // Assert
        sln.Should().Contain("MyApp.Domain");
        sln.Should().Contain("MyApp.API");
        sln.Should().Contain("src\\MyApp.Domain\\MyApp.Domain.csproj",
            "i path nel .sln usano backslash Windows");
        sln.Should().Contain("Microsoft Visual Studio Solution File");
        sln.Should().Contain("SolutionGuid");
    }

    // ========================================================================
    // TEST WRITESOLUTIONTODISK — INTEGRAZIONE
    // ========================================================================

    [Fact]
    public void WriteSolutionToDisk_ShouldCreateMultiProjectStructure_WithArchitecture()
    {
        // Arrange
        var outputDir = Path.Combine(Path.GetTempPath(), $"bmad-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var architecture = """
                ## 9. Struttura del Progetto .NET

                ```solution-structure
                SOLUTION: TodoApp
                PROJECTS:
                - Name: TodoApp.Domain | SDK: Microsoft.NET.Sdk | References: (nessuno)
                  Folders: Entities/
                - Name: TodoApp.API | SDK: Microsoft.NET.Sdk.Web | References: TodoApp.Domain
                  Folders: Controllers/
                - Name: TodoApp.Tests | SDK: Microsoft.NET.Sdk | References: TodoApp.Domain
                  Folders: Unit/
                ```
                """;

            var files = new List<(string FileName, string Content)>
            {
                ("src/TodoApp.Domain/Entities/Todo.cs", "namespace TodoApp.Domain.Entities;\npublic record Todo(int Id);"),
                ("src/TodoApp.API/Controllers/TodoController.cs", "namespace TodoApp.API.Controllers;\npublic class TodoController { }"),
                ("tests/TodoApp.Tests/Unit/TodoTests.cs", "namespace TodoApp.Tests.Unit;\npublic class TodoTests { }"),
            };

            // Act
            var solutionDir = SolutionExporterService.WriteSolutionToDisk(
                outputDir, "TodoApp", files, architecture);

            // Assert: struttura cartelle
            Directory.Exists(Path.Combine(solutionDir, "src", "TodoApp.Domain")).Should().BeTrue();
            Directory.Exists(Path.Combine(solutionDir, "src", "TodoApp.API")).Should().BeTrue();
            Directory.Exists(Path.Combine(solutionDir, "tests", "TodoApp.Tests")).Should().BeTrue();

            // Assert: file C# scritti nelle posizioni corrette
            File.Exists(Path.Combine(solutionDir, "src", "TodoApp.Domain", "Entities", "Todo.cs"))
                .Should().BeTrue();
            File.Exists(Path.Combine(solutionDir, "src", "TodoApp.API", "Controllers", "TodoController.cs"))
                .Should().BeTrue();
            File.Exists(Path.Combine(solutionDir, "tests", "TodoApp.Tests", "Unit", "TodoTests.cs"))
                .Should().BeTrue();

            // Assert: .csproj generati
            File.Exists(Path.Combine(solutionDir, "src", "TodoApp.Domain", "TodoApp.Domain.csproj"))
                .Should().BeTrue();
            File.Exists(Path.Combine(solutionDir, "src", "TodoApp.API", "TodoApp.API.csproj"))
                .Should().BeTrue();
            File.Exists(Path.Combine(solutionDir, "tests", "TodoApp.Tests", "TodoApp.Tests.csproj"))
                .Should().BeTrue();

            // Assert: .sln generato con tutti i progetti
            var slnPath = Path.Combine(solutionDir, "TodoApp.sln");
            File.Exists(slnPath).Should().BeTrue();
            var slnContent = File.ReadAllText(slnPath);
            slnContent.Should().Contain("TodoApp.Domain");
            slnContent.Should().Contain("TodoApp.API");
            slnContent.Should().Contain("TodoApp.Tests");

            // Assert: ProjectReference corretti nel .csproj dell'API
            var apiCsproj = File.ReadAllText(
                Path.Combine(solutionDir, "src", "TodoApp.API", "TodoApp.API.csproj"));
            apiCsproj.Should().Contain("../TodoApp.Domain/TodoApp.Domain.csproj");

            // Assert: ProjectReference corretti nel .csproj dei test
            var testsCsproj = File.ReadAllText(
                Path.Combine(solutionDir, "tests", "TodoApp.Tests", "TodoApp.Tests.csproj"));
            testsCsproj.Should().Contain("../../src/TodoApp.Domain/TodoApp.Domain.csproj");
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public void WriteSolutionToDisk_ShouldInferProjects_WithoutArchitecture()
    {
        // Arrange
        var outputDir = Path.Combine(Path.GetTempPath(), $"bmad-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var files = new List<(string FileName, string Content)>
            {
                ("src/MyProject.Core/Models/Item.cs", "namespace MyProject.Core.Models;\npublic record Item;"),
                ("tests/MyProject.Tests/ItemTests.cs", "namespace MyProject.Tests;\npublic class ItemTests { }"),
            };

            // Act
            var solutionDir = SolutionExporterService.WriteSolutionToDisk(
                outputDir, "MyProject", files, architectureContent: null);

            // Assert: cartelle create
            Directory.Exists(Path.Combine(solutionDir, "src", "MyProject.Core")).Should().BeTrue();
            Directory.Exists(Path.Combine(solutionDir, "tests", "MyProject.Tests")).Should().BeTrue();

            // Assert: .csproj generati per ogni progetto inferito
            File.Exists(Path.Combine(solutionDir, "src", "MyProject.Core", "MyProject.Core.csproj"))
                .Should().BeTrue();
            File.Exists(Path.Combine(solutionDir, "tests", "MyProject.Tests", "MyProject.Tests.csproj"))
                .Should().BeTrue();

            // Assert: .sln con tutti i progetti
            var slnFiles = Directory.GetFiles(solutionDir, "*.sln");
            slnFiles.Should().HaveCount(1);
            var slnContent = File.ReadAllText(slnFiles[0]);
            slnContent.Should().Contain("MyProject.Core");
            slnContent.Should().Contain("MyProject.Tests");
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public void WriteSolutionToDisk_ShouldHandleFilesWithoutPathPrefix()
    {
        // Arrange
        var outputDir = Path.Combine(Path.GetTempPath(), $"bmad-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var files = new List<(string FileName, string Content)>
            {
                ("Program.cs", "Console.WriteLine(\"Hello\");"),
                ("MyClass.cs", "public class MyClass { }"),
            };

            // Act — senza architettura, file senza path prefix
            var solutionDir = SolutionExporterService.WriteSolutionToDisk(
                outputDir, "SimpleApp", files, architectureContent: null);

            // Assert: i file finiscono nel progetto di default src/SimpleApp/
            File.Exists(Path.Combine(solutionDir, "src", "SimpleApp", "Program.cs"))
                .Should().BeTrue();
            File.Exists(Path.Combine(solutionDir, "src", "SimpleApp", "MyClass.cs"))
                .Should().BeTrue();
            File.Exists(Path.Combine(solutionDir, "src", "SimpleApp", "SimpleApp.csproj"))
                .Should().BeTrue();
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }
}
