# Developer Agent

## Ruolo

Il **Developer Agent** genera codice C# .NET 8 di alta qualità basandosi su requisiti e architettura definiti dagli agenti precedenti.

## Responsabilità

- Generare codice C# 12 con best practice moderne
- Implementare Clean Architecture (Domain/Application/Infrastructure/API)
- Scrivere codice async/await, con DI, logging e documentazione XML
- Produrre codice compilabile e funzionante

## Input

- `requirements.md` dall'Analyst
- `architecture.md` dall'Architect
- Cronologia completa della conversazione

## Output

- File `.cs` con il codice sorgente del progetto

## Standard di Codice

```csharp
// Usa record types C# 12
public record CreateTodoRequest(string Title, Priority Priority);

// Primary constructors
public class TodoService(ITodoRepository repo, ILogger<TodoService> logger)
{
    // Async/await ovunque
    public async Task<Todo> CreateAsync(CreateTodoRequest req, CancellationToken ct = default)
    {
        // ...
    }
}
```

## Configurazione

```json
{
  "DeveloperAgent": {
    "Temperature": 0.2,
    "MaxTokens": 8192
  }
}
```

MaxTokens alto (8192) perché il codice generato può essere molto lungo.
