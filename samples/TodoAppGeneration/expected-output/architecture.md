# Documento di Architettura Tecnica
## Progetto: Todo App REST API

**Generato da**: BMAD Architect Agent  
**Data**: 2024-01-15

---

## 1. Overview Architetturale

**Pattern**: Clean Architecture con CQRS (Command Query Responsibility Segregation)

```
┌─────────────────────────────────────────────────────────────┐
│                     CLIENT (Postman / Web App)               │
└─────────────────────────┬───────────────────────────────────┘
                          │ HTTPS
┌─────────────────────────▼───────────────────────────────────┐
│              Azure App Service (ASP.NET Core 8)              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Presentation Layer                       │   │
│  │  TodoController  │  AuthMiddleware  │  Swagger/OAS   │   │
│  └──────────────────┬───────────────────────────────────┘   │
│  ┌──────────────────▼───────────────────────────────────┐   │
│  │              Application Layer                        │   │
│  │  CreateTodoCommand  │  GetTodosQuery  │  Handlers    │   │
│  └──────────────────┬───────────────────────────────────┘   │
│  ┌──────────────────▼───────────────────────────────────┐   │
│  │              Domain Layer                             │   │
│  │  TodoItem  │  User  │  Priority  │  TodoStatus      │   │
│  └──────────────────┬───────────────────────────────────┘   │
│  ┌──────────────────▼───────────────────────────────────┐   │
│  │              Infrastructure Layer                     │   │
│  │  TodoRepository  │  AppDbContext  │  EmailService    │   │
│  └──────────────────┬───────────────────────────────────┘   │
└─────────────────────┼───────────────────────────────────────┘
                      │
        ┌─────────────┴─────────────┐
        │                           │
┌───────▼───────┐         ┌─────────▼──────────┐
│  Azure SQL DB  │         │  Azure AD (Entra)   │
│  (EF Core 8)  │         │  Authentication     │
└───────────────┘         └────────────────────┘
```

---

## 2. Servizi Azure

| Servizio | Tier | Scopo | Costo/Mese |
|----------|------|-------|------------|
| Azure App Service | B2 (staging) / P1v3 (prod) | Hosting API | ~$15 / ~$140 |
| Azure SQL Database | Basic (staging) / S2 (prod) | Persistenza dati | ~$5 / ~$150 |
| Azure AD | Free tier | Autenticazione | $0 |
| Application Insights | Pay-per-use | Monitoring | ~$5 |
| Azure Key Vault | Standard | Gestione segreti | ~$5 |

---

## 3. Struttura del Progetto .NET

```
TodoApp/
├── src/
│   ├── TodoApp.Domain/          ← Entità e business logic
│   │   ├── Entities/
│   │   │   ├── TodoItem.cs
│   │   │   └── User.cs
│   │   └── Enums/
│   │       ├── Priority.cs
│   │       └── TodoStatus.cs
│   ├── TodoApp.Application/     ← Use cases CQRS
│   │   ├── Commands/
│   │   ├── Queries/
│   │   └── Interfaces/
│   ├── TodoApp.Infrastructure/  ← EF Core, Azure services
│   │   ├── Persistence/
│   │   └── Services/
│   └── TodoApp.API/             ← Controllers, middleware
│       ├── Controllers/
│       └── Program.cs
└── tests/
    ├── TodoApp.UnitTests/
    └── TodoApp.IntegrationTests/
```
