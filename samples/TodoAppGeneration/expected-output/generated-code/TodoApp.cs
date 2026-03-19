// ============================================================================
// TodoApp.cs - Codice generato dal BMAD Developer Agent
// Esempio di output per la Todo App REST API
// Stack: C# 12, .NET 8, Clean Architecture, EF Core, Azure AD
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

// ============================================================================
// DOMAIN LAYER - Entità del dominio
// ============================================================================

namespace TodoApp.Domain.Entities;

/// <summary>
/// Entità principale: rappresenta un task nella todo list.
/// Usa record per immutabilità e C# 12 primary constructors.
/// </summary>
public class TodoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTimeOffset? DueDate { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string? AssignedToId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Complete() => IsCompleted = true;
    public void Assign(string userId) => AssignedToId = userId;
}

public enum Priority { Low = 0, Medium = 1, High = 2, Critical = 3 }
public enum TodoStatus { Active, Completed, Overdue }

// ============================================================================
// APPLICATION LAYER - DTOs e interfacce
// ============================================================================

namespace TodoApp.Application.DTOs;

public record CreateTodoRequest(
    string Title,
    string? Description,
    Priority Priority,
    DateTimeOffset? DueDate);

public record UpdateTodoRequest(
    string? Title,
    string? Description,
    Priority? Priority,
    DateTimeOffset? DueDate);

public record TodoResponse(
    int Id,
    string Title,
    string? Description,
    bool IsCompleted,
    Priority Priority,
    DateTimeOffset? DueDate,
    string OwnerId,
    DateTimeOffset CreatedAt);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

// ============================================================================
// INFRASTRUCTURE LAYER - Persistenza con EF Core
// ============================================================================

namespace TodoApp.Infrastructure.Persistence;

/// <summary>
/// DbContext configurato per Azure SQL Database.
/// Usa EF Core 8 con tutti i best practices.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configurazione entità TodoItem
        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(2000);
            entity.Property(t => t.OwnerId).HasMaxLength(450).IsRequired();
            entity.HasIndex(t => t.OwnerId);  // Index per query per utente
            entity.HasIndex(t => new { t.OwnerId, t.IsCompleted });  // Index composto
        });
    }
}

// ============================================================================
// API LAYER - Controller REST
// ============================================================================

namespace TodoApp.API.Controllers;

/// <summary>
/// Controller REST per la gestione dei todo items.
/// Richiede autenticazione Azure AD per tutti gli endpoint.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TodosController(AppDbContext db, ILogger<TodosController> logger) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Utente non autenticato");

    /// <summary>
    /// GET /api/todos - Lista i task dell'utente corrente con paginazione e filtri
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TodoResponse>), 200)]
    public async Task<ActionResult<PagedResult<TodoResponse>>> GetTodos(
        [FromQuery] bool? isCompleted = null,
        [FromQuery] Priority? priority = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = db.TodoItems
            .Where(t => t.OwnerId == CurrentUserId);

        // Applica filtri opzionali
        if (isCompleted.HasValue)
            query = query.Where(t => t.IsCompleted == isCompleted.Value);

        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TodoResponse(
                t.Id, t.Title, t.Description, t.IsCompleted,
                t.Priority, t.DueDate, t.OwnerId, t.CreatedAt))
            .ToListAsync(ct);

        logger.LogInformation(
            "Utente {UserId} ha recuperato {Count} task (pagina {Page})",
            CurrentUserId, items.Count, page);

        return Ok(new PagedResult<TodoResponse>(items, totalCount, page, pageSize));
    }

    /// <summary>
    /// POST /api/todos - Crea un nuovo task
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TodoResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<TodoResponse>> CreateTodo(
        CreateTodoRequest request,
        CancellationToken ct = default)
    {
        var todo = new TodoItem
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            DueDate = request.DueDate,
            OwnerId = CurrentUserId
        };

        db.TodoItems.Add(todo);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Task {TodoId} creato da utente {UserId}: {Title}",
            todo.Id, CurrentUserId, todo.Title);

        var response = new TodoResponse(
            todo.Id, todo.Title, todo.Description, todo.IsCompleted,
            todo.Priority, todo.DueDate, todo.OwnerId, todo.CreatedAt);

        return CreatedAtAction(nameof(GetTodo), new { id = todo.Id }, response);
    }

    /// <summary>
    /// GET /api/todos/{id} - Recupera un task specifico
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TodoResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<TodoResponse>> GetTodo(int id, CancellationToken ct = default)
    {
        var todo = await db.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == CurrentUserId, ct);

        if (todo == null)
            return NotFound($"Task {id} non trovato");

        return Ok(new TodoResponse(
            todo.Id, todo.Title, todo.Description, todo.IsCompleted,
            todo.Priority, todo.DueDate, todo.OwnerId, todo.CreatedAt));
    }

    /// <summary>
    /// PUT /api/todos/{id}/complete - Segna un task come completato
    /// </summary>
    [HttpPut("{id:int}/complete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CompleteTodo(int id, CancellationToken ct = default)
    {
        var todo = await db.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == CurrentUserId, ct);

        if (todo == null)
            return NotFound();

        todo.Complete();
        todo.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// DELETE /api/todos/{id} - Elimina un task
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteTodo(int id, CancellationToken ct = default)
    {
        var todo = await db.TodoItems
            .FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == CurrentUserId, ct);

        if (todo == null)
            return NotFound();

        db.TodoItems.Remove(todo);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Task {TodoId} eliminato da utente {UserId}",
            id, CurrentUserId);

        return NoContent();
    }
}

// ============================================================================
// PROGRAM.CS - Entry point con configurazione DI
// ============================================================================

namespace TodoApp.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Database - Azure SQL con EF Core
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure(3)));

        // Autenticazione Azure AD
        builder.Services.AddAuthentication()
            .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

        builder.Services.AddAuthorization();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Todo App API", Version = "v1" });
        });

        // Application Insights
        builder.Services.AddApplicationInsightsTelemetry(
            builder.Configuration["ApplicationInsights:ConnectionString"]);

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
