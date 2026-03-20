using BmadAgentFramework.Agents.Orchestrator;
using BmadAgentFramework.Core.Abstractions;
using BmadAgentFramework.Core.Models;
using BmadAgentFramework.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BmadAgentFramework.Tests.Unit;

/// <summary>
/// Test unitari per l'OrchestratorAgent e il WorkflowEngine.
/// Usa mock degli agenti per testare il flusso di orchestrazione
/// senza invocare Azure OpenAI (test veloci e isolati).
/// </summary>
public class OrchestratorTests
{
    // ========================================================================
    // TEST WORKFLOWENGINE
    // ========================================================================

    [Fact]
    public async Task WorkflowEngine_ShouldExecuteAllAgentsInSequence()
    {
        // Arrange
        var artifactStore = new ArtifactStore(Mock.Of<ILogger<ArtifactStore>>());
        var memoryService = new MemoryService(Mock.Of<ILogger<MemoryService>>());
        var engine = new WorkflowEngine(
            artifactStore,
            memoryService,
            Mock.Of<ILogger<WorkflowEngine>>());

        // Crea mock degli agenti che simulano un'esecuzione di successo
        var executionOrder = new List<string>();
        var agents = CreateMockAgents(executionOrder);

        var state = new WorkflowState
        {
            ProjectId = "test-project",
            Context = new AgentContext { Requirements = "Test workflow" }
        };

        // Act
        var result = await engine.ExecuteWorkflowAsync(state, agents);

        // Assert
        result.CurrentPhase.Should().Be(WorkflowPhase.Completed,
            "tutte le fasi devono essere completate");
        result.IsCompleted.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();

        // Verifica che gli agenti siano stati eseguiti nell'ordine corretto
        executionOrder.Should().ContainInOrder(
            "Analysis", "Architecture", "TechResearch", "Development", "QualityAssurance", "DevOps");
    }

    [Fact]
    public async Task WorkflowEngine_ShouldFailWorkflow_WhenAgentReturnsError()
    {
        // Arrange
        var artifactStore = new ArtifactStore(Mock.Of<ILogger<ArtifactStore>>());
        var memoryService = new MemoryService(Mock.Of<ILogger<MemoryService>>());
        var engine = new WorkflowEngine(
            artifactStore,
            memoryService,
            Mock.Of<ILogger<WorkflowEngine>>());

        // Agente che ritorna un errore
        var failingAgent = new Mock<IAgent>();
        failingAgent.Setup(a => a.Name).Returns("FailingAgent");
        failingAgent.Setup(a => a.Role).Returns("TestRole");
        failingAgent.Setup(a => a.CanHandleAsync(It.IsAny<AgentContext>()))
            .ReturnsAsync(true);
        failingAgent.Setup(a => a.ProcessAsync(It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AgentMessage.CreateError(
                "FailingAgent", AgentRole.Analyst, "Errore simulato", WorkflowPhase.Analysis));

        var state = new WorkflowState
        {
            Context = new AgentContext { Requirements = "Test" }
        };

        // Act
        var result = await engine.ExecuteWorkflowAsync(state, new[] { failingAgent.Object });

        // Assert
        result.CurrentPhase.Should().Be(WorkflowPhase.Failed);
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task WorkflowEngine_ShouldSkipPhase_WhenNoAgentCanHandle()
    {
        // Arrange
        var artifactStore = new ArtifactStore(Mock.Of<ILogger<ArtifactStore>>());
        var memoryService = new MemoryService(Mock.Of<ILogger<MemoryService>>());
        var engine = new WorkflowEngine(
            artifactStore,
            memoryService,
            Mock.Of<ILogger<WorkflowEngine>>());

        // Nessun agente disponibile
        var state = new WorkflowState
        {
            Context = new AgentContext { Requirements = "Test" }
        };

        // Act - passa lista vuota di agenti
        var result = await engine.ExecuteWorkflowAsync(state, Array.Empty<IAgent>());

        // Assert
        result.CurrentPhase.Should().Be(WorkflowPhase.Completed,
            "tutte le fasi vengono saltate se non ci sono agenti");
    }

    [Fact]
    public async Task WorkflowEngine_ShouldResumeFromCorrectPhase()
    {
        // Arrange
        var artifactStore = new ArtifactStore(Mock.Of<ILogger<ArtifactStore>>());
        var memoryService = new MemoryService(Mock.Of<ILogger<MemoryService>>());
        var engine = new WorkflowEngine(
            artifactStore,
            memoryService,
            Mock.Of<ILogger<WorkflowEngine>>());

        var executionOrder = new List<string>();
        var agents = CreateMockAgents(executionOrder);

        // Stato con fase già avanzata (resume dal Development)
        var state = new WorkflowState
        {
            CurrentPhase = WorkflowPhase.Development,
            Context = new AgentContext
            {
                Requirements = "Test",
                CurrentPhase = WorkflowPhase.Development
            }
        };

        // Pre-popola artefatti necessari per Development
        state.Context.SaveArtifact(ProjectArtifact.CreateRequirements("Req", "AnalystAgent"));
        state.Context.SaveArtifact(ProjectArtifact.CreateArchitecture("Arch", "ArchitectAgent"));

        // Act
        var result = await engine.ExecuteWorkflowAsync(state, agents);

        // Assert
        result.CurrentPhase.Should().Be(WorkflowPhase.Completed);

        // Solo le fasi da Development in poi devono essere eseguite
        executionOrder.Should().NotContain("Analysis",
            "la fase Analysis è già completata");
        executionOrder.Should().NotContain("Architecture",
            "la fase Architecture è già completata");
        executionOrder.Should().NotContain("TechResearch",
            "la fase TechResearch è già completata");
        executionOrder.Should().Contain("Development");
    }

    // ========================================================================
    // TEST ORCHESTRATORAGENT
    // ========================================================================

    [Fact]
    public void OrchestratorAgent_RegisterAgent_ShouldAcceptMultipleAgents()
    {
        // Arrange
        var orchestrator = new OrchestratorAgent(
            new ArtifactStore(Mock.Of<ILogger<ArtifactStore>>()),
            new MemoryService(Mock.Of<ILogger<MemoryService>>()),
            Mock.Of<ILogger<OrchestratorAgent>>(),
            new WorkflowEngine(
                new ArtifactStore(Mock.Of<ILogger<ArtifactStore>>()),
                new MemoryService(Mock.Of<ILogger<MemoryService>>()),
                Mock.Of<ILogger<WorkflowEngine>>()));

        var agent1 = Mock.Of<IAgent>(a => a.Name == "Agent1" && a.Role == "Role1");
        var agent2 = Mock.Of<IAgent>(a => a.Name == "Agent2" && a.Role == "Role2");

        // Act & Assert - non deve lanciare eccezioni
        var act = () =>
        {
            orchestrator.RegisterAgent(agent1);
            orchestrator.RegisterAgent(agent2);
        };

        act.Should().NotThrow("la registrazione degli agenti non deve fallire");
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    /// <summary>
    /// Crea una lista di mock agenti che simulano un'esecuzione di successo.
    /// Ogni agente è configurato per gestire la sua fase specifica.
    /// </summary>
    private static IReadOnlyList<IAgent> CreateMockAgents(List<string> executionOrder)
    {
        var phases = new[]
        {
            (Phase: WorkflowPhase.Analysis, Role: AgentRole.Analyst, ArtifactType: "requirements"),
            (Phase: WorkflowPhase.Architecture, Role: AgentRole.Architect, ArtifactType: "architecture"),
            (Phase: WorkflowPhase.TechResearch, Role: AgentRole.TechResearch, ArtifactType: "tech-reference"),
            (Phase: WorkflowPhase.Development, Role: AgentRole.Developer, ArtifactType: "code"),
            (Phase: WorkflowPhase.QualityAssurance, Role: AgentRole.QA, ArtifactType: "tests"),
            (Phase: WorkflowPhase.DevOps, Role: AgentRole.DevOps, ArtifactType: "pipeline")
        };

        return phases.Select(p =>
        {
            var mock = new Mock<IAgent>();
            mock.Setup(a => a.Name).Returns(p.Phase.ToString());
            mock.Setup(a => a.Role).Returns(p.Role.ToString());

            // Ogni agente gestisce solo la sua fase
            mock.Setup(a => a.CanHandleAsync(It.Is<AgentContext>(
                    ctx => ctx.CurrentPhase == p.Phase)))
                .ReturnsAsync(true);
            mock.Setup(a => a.CanHandleAsync(It.Is<AgentContext>(
                    ctx => ctx.CurrentPhase != p.Phase)))
                .ReturnsAsync(false);

            // Simula l'esecuzione dell'agente
            mock.Setup(a => a.ProcessAsync(
                    It.IsAny<AgentContext>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((AgentContext ctx, CancellationToken _) =>
                {
                    // Registra l'ordine di esecuzione
                    executionOrder.Add(p.Phase.ToString());

                    // Aggiunge un artefatto simulato al contesto
                    ctx.SaveArtifact(new ProjectArtifact
                    {
                        Name = $"{p.ArtifactType}.md",
                        ArtifactType = p.ArtifactType,
                        ProducedBy = p.Phase.ToString(),
                        Content = $"Contenuto simulato per {p.ArtifactType}"
                    });

                    return new AgentMessage
                    {
                        AgentName = p.Phase.ToString(),
                        Role = p.Role,
                        Content = $"Completato: {p.Phase}",
                        Phase = p.Phase
                    };
                });

            return mock.Object;
        }).ToList();
    }
}
