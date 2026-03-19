using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using BmadAgentFramework.Agents.Analyst;
using BmadAgentFramework.Agents.Orchestrator;
using BmadAgentFramework.Agents.SolutionBuilder;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BmadAgentFramework.Tests.Unit;

/// <summary>
/// Test unitari per gli agenti del framework BMAD.
/// Usa Moq per isolare le dipendenze (AzureOpenAIService, stores) e
/// testare solo la logica degli agenti.
/// </summary>
public class AgentTests
{
    // ========================================================================
    // TEST AGENTCONTEXT
    // ========================================================================

    [Fact]
    public void AgentContext_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var context = new AgentContext
        {
            Requirements = "Crea una REST API"
        };

        // Assert
        context.ProjectId.Should().NotBeNullOrEmpty("il ProjectId deve essere generato automaticamente");
        context.Requirements.Should().Be("Crea una REST API");
        context.CurrentPhase.Should().Be(WorkflowPhase.Analysis, "la fase iniziale è Analysis");
        context.Artifacts.Should().BeEmpty("nessun artefatto all'inizio");
        context.ConversationHistory.Should().BeEmpty("nessun messaggio all'inizio");
    }

    [Fact]
    public void AgentContext_ShouldSaveAndRetrieveArtifact()
    {
        // Arrange
        var context = new AgentContext();
        var artifact = new ProjectArtifact
        {
            Name = "requirements.md",
            ArtifactType = "requirements",
            Content = "# Requisiti\nFunzionalità 1",
            ProducedBy = "AnalystAgent"
        };

        // Act
        context.SaveArtifact(artifact);

        // Assert
        context.Artifacts.Should().HaveCount(1);
        context.GetArtifact("requirements").Should().NotBeNull();
        context.GetArtifact("requirements")!.Content.Should().Contain("Funzionalità 1");
        context.GetArtifact("nonexistent").Should().BeNull("tipo inesistente ritorna null");
    }

    [Fact]
    public void AgentContext_ShouldAddMessageToHistory()
    {
        // Arrange
        var context = new AgentContext();
        var message = new AgentMessage
        {
            AgentName = "AnalystAgent",
            Role = AgentRole.Analyst,
            Content = "Analisi completata",
            Phase = WorkflowPhase.Analysis
        };

        // Act
        context.AddMessage(message);

        // Assert
        context.ConversationHistory.Should().HaveCount(1);
        context.ConversationHistory[0].AgentName.Should().Be("AnalystAgent");
    }

    // ========================================================================
    // TEST WORKFLOWSTATE
    // ========================================================================

    [Fact]
    public void WorkflowState_ShouldAdvanceThroughAllPhases()
    {
        // Arrange
        var state = new WorkflowState();
        state.Context.CurrentPhase = WorkflowPhase.Analysis;

        // Act & Assert - verifica la progressione delle fasi
        state.Context.CurrentPhase.Should().Be(WorkflowPhase.Analysis);

        state.AdvanceToNextPhase();
        state.CurrentPhase.Should().Be(WorkflowPhase.Architecture);

        state.AdvanceToNextPhase();
        state.CurrentPhase.Should().Be(WorkflowPhase.Development);

        state.AdvanceToNextPhase();
        state.CurrentPhase.Should().Be(WorkflowPhase.SolutionBuilding,
            "dopo lo sviluppo il SolutionBuilder deve materializzare il codice");

        state.AdvanceToNextPhase();
        state.CurrentPhase.Should().Be(WorkflowPhase.QualityAssurance);

        state.AdvanceToNextPhase();
        state.CurrentPhase.Should().Be(WorkflowPhase.DevOps);

        state.AdvanceToNextPhase();
        state.CurrentPhase.Should().Be(WorkflowPhase.Completed);
        state.IsCompleted.Should().BeTrue();
        state.CompletedAt.Should().NotBeNull("deve essere impostato al completamento");
    }

    [Fact]
    public void WorkflowState_IsCompleted_ShouldReturnTrue_When_Failed()
    {
        // Arrange
        var state = new WorkflowState { CurrentPhase = WorkflowPhase.Failed };

        // Act & Assert
        state.IsCompleted.Should().BeTrue("Failed è considerato completato (con errore)");
        state.IsWaitingForApproval.Should().BeFalse();
    }

    // ========================================================================
    // TEST PROJECTARTIFACT
    // ========================================================================

    [Fact]
    public void ProjectArtifact_CreateRequirements_ShouldSetCorrectProperties()
    {
        // Arrange
        const string content = "# PRD\n## Requisiti";
        const string producer = "AnalystAgent";

        // Act
        var artifact = ProjectArtifact.CreateRequirements(content, producer);

        // Assert
        artifact.Name.Should().Be("requirements.md");
        artifact.ArtifactType.Should().Be("requirements");
        artifact.ProducedBy.Should().Be("AnalystAgent");
        artifact.Content.Should().Be(content);
        artifact.ContentFormat.Should().Be("markdown");
        artifact.Version.Should().Be(1, "versione iniziale è 1");
        artifact.ArtifactId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProjectArtifact_CreateSourceCode_ShouldSetCorrectProperties()
    {
        // Arrange
        const string code = "public class TodoItem { }";
        const string fileName = "TodoItem.cs";
        const string producer = "DeveloperAgent";

        // Act
        var artifact = ProjectArtifact.CreateSourceCode(code, fileName, producer);

        // Assert
        artifact.Name.Should().Be(fileName);
        artifact.ArtifactType.Should().Be("code");
        artifact.ContentFormat.Should().Be("csharp");
        artifact.ProducedBy.Should().Be(producer);
    }

    // ========================================================================
    // TEST AGENTMESSAGE
    // ========================================================================

    [Fact]
    public void AgentMessage_CreateError_ShouldSetIsErrorTrue()
    {
        // Act
        var errorMessage = AgentMessage.CreateError(
            "AnalystAgent",
            AgentRole.Analyst,
            "Errore durante l'analisi",
            WorkflowPhase.Analysis);

        // Assert
        errorMessage.IsError.Should().BeTrue();
        errorMessage.Content.Should().Be("Errore durante l'analisi");
        errorMessage.AgentName.Should().Be("AnalystAgent");
        errorMessage.MessageId.Should().NotBeNullOrEmpty();
        errorMessage.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ========================================================================
    // TEST REQUIREMENTSPARSER
    // ========================================================================

    [Fact]
    public void RequirementsParser_BuildAnalysisPrompt_ShouldContainRequirements()
    {
        // Arrange
        const string requirements = "Crea una Todo App REST API";

        // Act
        var prompt = RequirementsParser.BuildAnalysisPrompt(requirements);

        // Assert
        prompt.Should().Contain(requirements, "il prompt deve includere i requisiti originali");
        prompt.Should().Contain("PRD", "il prompt deve richiedere un PRD");
        prompt.Should().Contain("Requisiti Funzionali", "deve includere la sezione RF");
    }

    [Theory]
    [InlineData("# Todo App REST API\n## Funzionalità", "Todo App REST API")]
    [InlineData("Titolo: Sistema di Gestione Ordini\n", "Sistema di Gestione Ordini")]
    [InlineData("## Solo sottotitoli\nNessun titolo principale", "Progetto BMAD")]
    public void RequirementsParser_ExtractProjectName_ShouldExtractCorrectly(
        string analysisOutput, string expectedName)
    {
        // Act
        var name = RequirementsParser.ExtractProjectName(analysisOutput);

        // Assert
        name.Should().Be(expectedName);
    }

    // ========================================================================
    // TEST MEMORYSERVICE
    // ========================================================================

    [Fact]
    public async Task MemoryService_ShouldSaveAndRetrieveMessages()
    {
        // Arrange
        var logger = Mock.Of<ILogger<MemoryService>>();
        var service = new MemoryService(logger);
        var projectId = Guid.NewGuid().ToString();
        var message = new AgentMessage
        {
            AgentName = "AnalystAgent",
            Role = AgentRole.Analyst,
            Content = "Test message",
            Phase = WorkflowPhase.Analysis
        };

        // Act
        await service.SaveConversationAsync(projectId, message);
        var history = await service.GetConversationHistoryAsync(projectId);

        // Assert
        history.Should().HaveCount(1);
        history[0].Content.Should().Be("Test message");
        history[0].AgentName.Should().Be("AnalystAgent");
    }

    [Fact]
    public async Task MemoryService_GetHistory_ShouldReturnEmptyForUnknownProject()
    {
        // Arrange
        var logger = Mock.Of<ILogger<MemoryService>>();
        var service = new MemoryService(logger);

        // Act
        var history = await service.GetConversationHistoryAsync("unknown-project");

        // Assert
        history.Should().BeEmpty("progetto sconosciuto non ha storia");
    }

    [Fact]
    public async Task MemoryService_ShouldClearProjectData()
    {
        // Arrange
        var logger = Mock.Of<ILogger<MemoryService>>();
        var service = new MemoryService(logger);
        var projectId = "test-project";

        await service.SaveConversationAsync(projectId, new AgentMessage
        {
            AgentName = "Test", Content = "data", Phase = WorkflowPhase.Analysis
        });

        // Act
        await service.ClearProjectDataAsync(projectId);
        var history = await service.GetConversationHistoryAsync(projectId);

        // Assert
        history.Should().BeEmpty("i dati devono essere stati eliminati");
    }

    // ========================================================================
    // TEST ARTIFACTSTORE
    // ========================================================================

    [Fact]
    public async Task ArtifactStore_ShouldSaveAndRetrieveArtifact()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ArtifactStore>>();
        var store = new ArtifactStore(logger);
        var projectId = Guid.NewGuid().ToString();
        var artifact = ProjectArtifact.CreateRequirements("# Requisiti", "AnalystAgent");

        // Act
        await store.SaveArtifactAsync(projectId, artifact);
        var retrieved = await store.GetArtifactAsync(projectId, "requirements");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Content.Should().Be("# Requisiti");
        retrieved.ProducedBy.Should().Be("AnalystAgent");
    }

    [Fact]
    public async Task ArtifactStore_ShouldIncrementVersionOnUpdate()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ArtifactStore>>();
        var store = new ArtifactStore(logger);
        var projectId = "test";

        var v1 = ProjectArtifact.CreateRequirements("Versione 1", "AnalystAgent");
        var v2 = ProjectArtifact.CreateRequirements("Versione 2 aggiornata", "AnalystAgent");

        // Act
        await store.SaveArtifactAsync(projectId, v1);
        await store.SaveArtifactAsync(projectId, v2);
        var retrieved = await store.GetArtifactAsync(projectId, "requirements");

        // Assert
        retrieved!.Version.Should().Be(2, "la versione deve essere incrementata");
        retrieved.Content.Should().Be("Versione 2 aggiornata");
    }

    [Fact]
    public async Task ArtifactStore_ExportProject_ShouldIncludeAllArtifacts()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ArtifactStore>>();
        var store = new ArtifactStore(logger);
        var projectId = "export-test";

        await store.SaveArtifactAsync(projectId, ProjectArtifact.CreateRequirements("# PRD", "AnalystAgent"));
        await store.SaveArtifactAsync(projectId, ProjectArtifact.CreateArchitecture("# Arch", "ArchitectAgent"));

        // Act
        var export = await store.ExportProjectAsync(projectId);

        // Assert
        export.Should().Contain("BMAD Framework");
        export.Should().Contain("requirements.md");
        export.Should().Contain("architecture.md");
        export.Should().Contain("# PRD");
        export.Should().Contain("# Arch");
    }

    // ========================================================================
    // TEST PROJECTARTIFACT - SOLUTION
    // ========================================================================

    [Fact]
    public void ProjectArtifact_CreateSolution_ShouldSetCorrectProperties()
    {
        // Arrange
        const string summary = "# Soluzione generata";
        const string outputPath = "/tmp/output/MyApp";
        const string producer = "SolutionBuilderAgent";

        // Act
        var artifact = ProjectArtifact.CreateSolution(summary, outputPath, producer);

        // Assert
        artifact.Name.Should().Be("solution-summary.md");
        artifact.ArtifactType.Should().Be("solution");
        artifact.ProducedBy.Should().Be(producer);
        artifact.Content.Should().Be(summary);
        artifact.ContentFormat.Should().Be("markdown");
        artifact.Metadata["outputPath"].Should().Be(outputPath);
        artifact.ArtifactId.Should().NotBeNullOrEmpty();
    }

    // ========================================================================
    // TEST SOLUTIONWRITER
    // ========================================================================

    [Fact]
    public void SolutionWriter_ParseCodeBlocks_ShouldExtractFileWithHeadingPath()
    {
        // Arrange
        const string markdown = """
            ### src/Domain/Entities/TodoItem.cs
            ```csharp
            public class TodoItem { }
            ```
            """;

        // Act
        var files = SolutionWriter.ParseCodeBlocks(markdown);

        // Assert
        files.Should().HaveCount(1);
        files[0].FilePath.Should().Be("src/Domain/Entities/TodoItem.cs");
        files[0].Content.Should().Contain("public class TodoItem");
    }

    [Fact]
    public void SolutionWriter_ParseCodeBlocks_ShouldExtractMultipleFiles()
    {
        // Arrange
        const string markdown = """
            ### src/Domain/Entities/TodoItem.cs
            ```csharp
            public class TodoItem { }
            ```

            ### src/Application/Services/TodoService.cs
            ```csharp
            public class TodoService { }
            ```
            """;

        // Act
        var files = SolutionWriter.ParseCodeBlocks(markdown);

        // Assert
        files.Should().HaveCount(2);
        files[0].FilePath.Should().Be("src/Domain/Entities/TodoItem.cs");
        files[1].FilePath.Should().Be("src/Application/Services/TodoService.cs");
    }

    [Fact]
    public void SolutionWriter_ParseCodeBlocks_ShouldDerivePathFromClassWhenNoHeading()
    {
        // Arrange
        const string markdown = """
            ```csharp
            public class MyController { }
            ```
            """;

        // Act
        var files = SolutionWriter.ParseCodeBlocks(markdown);

        // Assert
        files.Should().HaveCount(1);
        files[0].FilePath.Should().Be("MyController.cs",
            "il path deve essere derivato dal nome della classe quando non c'è un'intestazione");
    }

    [Fact]
    public void SolutionWriter_ParseCodeBlocks_ShouldSupportBacktickHeading()
    {
        // Arrange - formato con backtick: ### `src/MyFile.cs`
        const string markdown = """
            ### `src/MyFile.cs`
            ```csharp
            public class MyFile { }
            ```
            """;

        // Act
        var files = SolutionWriter.ParseCodeBlocks(markdown);

        // Assert
        files.Should().HaveCount(1);
        files[0].FilePath.Should().Be("src/MyFile.cs");
    }

    [Fact]
    public void SolutionWriter_ParseCodeBlocks_ShouldReturnEmptyForMarkdownWithNoCodeBlocks()
    {
        // Arrange
        const string markdown = "# Solo testo\nNessun blocco di codice qui.";

        // Act
        var files = SolutionWriter.ParseCodeBlocks(markdown);

        // Assert
        files.Should().BeEmpty("nessun blocco ```csharp presente");
    }

    [Fact]
    public async Task SolutionWriter_WriteFilesAsync_ShouldCreateFilesOnDisk()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"bmad-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var files = new List<(string FilePath, string Content)>
        {
            ("src/Domain/Entities/TodoItem.cs", "public class TodoItem { }"),
            ("src/Application/Services/TodoService.cs", "public class TodoService { }")
        };

        try
        {
            // Act
            await SolutionWriter.WriteFilesAsync(tempDir, files);

            // Assert
            File.Exists(Path.Combine(tempDir, "src", "Domain", "Entities", "TodoItem.cs"))
                .Should().BeTrue("il file deve essere creato sul disco");
            File.Exists(Path.Combine(tempDir, "src", "Application", "Services", "TodoService.cs"))
                .Should().BeTrue("anche il secondo file deve essere creato");

            var content = await File.ReadAllTextAsync(
                Path.Combine(tempDir, "src", "Domain", "Entities", "TodoItem.cs"));
            content.Should().Be("public class TodoItem { }");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SolutionWriter_GenerateProjectFile_ShouldContainProjectName()
    {
        // Act
        var csproj = SolutionWriter.GenerateProjectFile("MyTodoApp");

        // Assert
        csproj.Should().Contain("MyTodoApp");
        csproj.Should().Contain("net8.0");
        csproj.Should().Contain("Microsoft.NET.Sdk.Web");
    }

    // ========================================================================
    // TEST SOLUTIONBUILDERAGENT
    // ========================================================================

    [Fact]
    public async Task SolutionBuilderAgent_CanHandle_ShouldReturnTrue_WhenPhaseIsCorrectAndCodeArtifactExists()
    {
        // Arrange
        var agent = CreateSolutionBuilderAgent();
        var context = new AgentContext
        {
            CurrentPhase = WorkflowPhase.SolutionBuilding
        };
        context.SaveArtifact(ProjectArtifact.CreateSourceCode(
            "### MyClass.cs\n```csharp\npublic class MyClass { }\n```",
            "MyClass.cs",
            "DeveloperAgent"));

        // Act
        var canHandle = await agent.CanHandleAsync(context);

        // Assert
        canHandle.Should().BeTrue();
    }

    [Fact]
    public async Task SolutionBuilderAgent_CanHandle_ShouldReturnFalse_WhenWrongPhase()
    {
        // Arrange
        var agent = CreateSolutionBuilderAgent();
        var context = new AgentContext
        {
            CurrentPhase = WorkflowPhase.Development
        };

        // Act
        var canHandle = await agent.CanHandleAsync(context);

        // Assert
        canHandle.Should().BeFalse("la fase corrente non è SolutionBuilding");
    }

    [Fact]
    public async Task SolutionBuilderAgent_CanHandle_ShouldReturnFalse_WhenNoCodeArtifact()
    {
        // Arrange
        var agent = CreateSolutionBuilderAgent();
        var context = new AgentContext
        {
            CurrentPhase = WorkflowPhase.SolutionBuilding
            // nessun artefatto "code"
        };

        // Act
        var canHandle = await agent.CanHandleAsync(context);

        // Assert
        canHandle.Should().BeFalse("manca l'artefatto 'code'");
    }

    [Fact]
    public async Task SolutionBuilderAgent_ProcessAsync_ShouldWriteFilesToDiskAndSaveArtifact()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"bmad-agent-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var agent = CreateSolutionBuilderAgent(tempDir);

            var context = new AgentContext
            {
                CurrentPhase = WorkflowPhase.SolutionBuilding,
                ProjectName = "TodoApp",
                Requirements = "Crea una Todo REST API"
            };
            context.SaveArtifact(ProjectArtifact.CreateSourceCode(
                "### src/Domain/TodoItem.cs\n```csharp\npublic class TodoItem { }\n```",
                "TodoApp.cs",
                "DeveloperAgent"));

            // Act
            var message = await agent.ProcessAsync(context);

            // Assert
            message.IsError.Should().BeFalse();
            message.AgentName.Should().Be("SolutionBuilderAgent");
            message.Phase.Should().Be(WorkflowPhase.SolutionBuilding);

            // Verifica che i file siano stati scritti su disco
            var outputProjectDir = Path.Combine(tempDir, "TodoApp");
            Directory.Exists(outputProjectDir).Should().BeTrue("la cartella del progetto deve esistere");

            File.Exists(Path.Combine(outputProjectDir, "src", "Domain", "TodoItem.cs"))
                .Should().BeTrue("il file C# deve essere scritto su disco");
            File.Exists(Path.Combine(outputProjectDir, "TodoApp.csproj"))
                .Should().BeTrue("deve essere generato il file .csproj");
            File.Exists(Path.Combine(outputProjectDir, "README.md"))
                .Should().BeTrue("deve essere generato il README.md");

            // Verifica che l'artefatto "solution" sia stato salvato nel contesto
            context.GetArtifact("solution").Should().NotBeNull();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// Crea una istanza di <see cref="SolutionBuilderAgent"/> con dipendenze mock,
    /// usando una directory di output temporanea.
    /// </summary>
    private static SolutionBuilderAgent CreateSolutionBuilderAgent(string? outputBasePath = null)
    {
        var logger = Mock.Of<ILogger<SolutionBuilderAgent>>();
        var artifactStore = new ArtifactStore(Mock.Of<ILogger<ArtifactStore>>());
        var memoryService = new MemoryService(Mock.Of<ILogger<MemoryService>>());
        var frameworkOptions = new BmadAgentFramework.Core.Configuration.FrameworkOptions
        {
            OutputPath = outputBasePath ?? Path.Combine(Path.GetTempPath(), $"bmad-default-{Guid.NewGuid():N}")
        };
        var options = Options.Create(frameworkOptions);

        return new SolutionBuilderAgent(artifactStore, memoryService, options, logger);
    }
}
