# Developer Agent

## Ruolo

Il **Developer Agent** genera l'intera struttura della Solution .NET 8 e il codice C# di alta qualità basandosi su requisiti e architettura definiti dagli agenti precedenti. È responsabile sia dello scaffolding della solution (`.sln`, `.csproj`, cartelle, project reference) sia dell'implementazione del codice sorgente per ogni layer.

## Responsabilità

- Generare la struttura completa della Solution .NET (`.sln`, `.csproj`, cartelle)
- Configurare i project reference tra i layer (Domain ← Application ← Infrastructure/API)
- Generare codice C# 12 con best practice moderne
- Implementare Clean Architecture (Domain/Application/Infrastructure/API)
- Scrivere codice async/await, con DI, logging e documentazione XML
- Produrre codice compilabile e funzionante

## Input

- `requirements.md` dall'Analyst
- `architecture.md` dall'Architect
- Cronologia completa della conversazione

## Output

- `<SolutionName>.sln`: Visual Studio Solution con tutti i progetti registrati
- `src/<SolutionName>.Domain/<SolutionName>.Domain.csproj`
- `src/<SolutionName>.Application/<SolutionName>.Application.csproj`
- `src/<SolutionName>.Infrastructure/<SolutionName>.Infrastructure.csproj`
- `src/<SolutionName>.API/<SolutionName>.API.csproj`
- `tests/<SolutionName>.Tests/<SolutionName>.Tests.csproj`
- File `.cs` con il codice sorgente per ogni layer

## Template .csproj

### Domain (nessuna dipendenza)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

### Application (referenzia Domain)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\<SolutionName>.Domain\<SolutionName>.Domain.csproj" />
  </ItemGroup>
</Project>
```

### Infrastructure (referenzia Application)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\<SolutionName>.Application\<SolutionName>.Application.csproj" />
  </ItemGroup>
</Project>
```

### API (referenzia Application e Infrastructure)

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\<SolutionName>.Application\<SolutionName>.Application.csproj" />
    <ProjectReference Include="..\<SolutionName>.Infrastructure\<SolutionName>.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

### Tests (referenzia Domain e Application)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\<SolutionName>.Domain\<SolutionName>.Domain.csproj" />
    <ProjectReference Include="..\..\src\<SolutionName>.Application\<SolutionName>.Application.csproj" />
  </ItemGroup>
</Project>
```

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
    "MaxTokens": 16384
  }
}
```

MaxTokens alto (16384) perché la generazione dell'intera solution (struttura + codice) richiede più token.
