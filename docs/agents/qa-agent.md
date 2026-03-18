# QA Agent

## Ruolo

Il **QA Agent** analizza il codice prodotto dal Developer e genera una suite di test completa usando xUnit, FluentAssertions e Moq.

## Responsabilità

- Generare unit test (xUnit + FluentAssertions + Moq)
- Generare integration test (WebApplicationFactory)
- Identificare rischi di qualità e potenziali bug
- Produrre un test plan documentato

## Input

- Codice generato dal Developer
- Requisiti originali dall'Analyst

## Output

- `*Tests.cs`: suite di test completa
- Test plan con lista dei casi di test e coverage estimate

## Standard di Test

```csharp
// Naming convention: Should_[Expected]_When_[Condition]
[Fact]
public async Task Should_ReturnNotFound_When_TodoDoesNotExist()
{
    // Arrange
    var repo = new Mock<ITodoRepository>();
    repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((TodoItem?)null);

    // Act
    var result = await _controller.GetTodo(999);

    // Assert
    result.Should().BeOfType<NotFoundResult>();
}
```

## Configurazione

```json
{
  "QAAgent": {
    "Temperature": 0.2,
    "MaxTokens": 8192
  }
}
```
