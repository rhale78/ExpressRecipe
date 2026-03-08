# GitHub Copilot Instructions for ExpressRecipe

## Project Overview

**ExpressRecipe** is a local-first, cloud-capable dietary management platform built with .NET 10, Aspire, and microservices architecture. It helps individuals with dietary restrictions manage food choices, meal planning, inventory, and shopping.

**Key Architecture Principles:**
- **Local-first design** - All user data stored locally (SQLite) by default, cloud sync is optional
- **Microservices** - 15+ specialized services with bounded contexts
- **.NET Aspire orchestration** - Service discovery, configuration, and observability
- **ADO.NET data access** - Direct SQL for performance, no Entity Framework
- **Multi-platform** - Blazor Web, WinUI 3, .NET MAUI, PWA

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 |
| Orchestration | .NET Aspire |
| Data Access | ADO.NET (custom `SqlHelper` base class) |
| Cloud DB | SQL Server |
| Local DB | SQLite |
| Cache | Redis |
| Messaging | RabbitMQ / Azure Service Bus |
| Web UI | Blazor (Auto rendering mode) |
| Desktop | WinUI 3 |
| Mobile | .NET MAUI |
| Auth | OAuth 2.0 / OpenID Connect |
| Logging | Serilog |
| Metrics | OpenTelemetry |
| AI | Ollama (llama2, mistral, codellama) |

## Project Structure

```
src/
├── ExpressRecipe.AppHost/           # Aspire orchestration entry point
├── ExpressRecipe.ServiceDefaults/   # Shared Aspire defaults (logging, health, telemetry)
├── ExpressRecipe.Shared/            # Shared models/DTOs/messages
├── ExpressRecipe.Data.Common/       # ADO.NET SqlHelper base class
├── Messaging/                       # RabbitMQ and saga infrastructure
├── Services/                        # Microservices (25+ services)
│   ├── ExpressRecipe.AuthService/
│   ├── ExpressRecipe.ProductService/
│   ├── ExpressRecipe.RecipeService/
│   ├── ExpressRecipe.IngredientService/
│   ├── ExpressRecipe.InventoryService/
│   ├── ExpressRecipe.ShoppingService/
│   ├── ExpressRecipe.MealPlanningService/
│   ├── ExpressRecipe.PriceService/
│   ├── ExpressRecipe.GroceryStoreLocationService/
│   ├── ExpressRecipe.NotificationService/
│   ├── ExpressRecipe.AIService/
│   ├── ExpressRecipe.VisionService/
│   ├── ExpressRecipe.SafeForkService/
│   ├── ExpressRecipe.ProfileService/
│   ├── ExpressRecipe.PreferencesService/
│   └── ... (see src/Services/ for full list)
└── Frontends/
    ├── ExpressRecipe.BlazorWeb/     # Blazor web app
    ├── ExpressRecipe.Windows/       # WinUI 3 desktop
    └── ExpressRecipe.MAUI/          # Android/iOS mobile
```

## Building and Testing

```bash
# Restore and build the solution
dotnet restore
dotnet build

# Run all tests
dotnet test

# Run with Aspire (orchestrates all services)
cd src/ExpressRecipe.AppHost
dotnet run

# Run a single service
cd src/Services/ExpressRecipe.ProductService
dotnet run
```

> **Note:** The AppHost `Program.cs` is a minimal diagnostic stub. The full Aspire wiring is in `Program..backup.cs`. Do not modify `Program.cs` without understanding this distinction.

## Coding Standards

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase | `ProductService` |
| Interfaces | `I` + PascalCase | `IProductService` |
| Methods | PascalCase | `GetProductByIdAsync` |
| Parameters | camelCase | `productId` |
| Private fields | `_camelCase` | `_httpClient` |
| Constants | PascalCase | `MaxRetryAttempts` |
| DB tables | PascalCase singular | `Product` |
| DB columns | PascalCase | `ProductId` |

### File Organization

- One class per file; file name matches class name
- Namespace matches folder structure
- `Program.cs` for service entry points

### API Design

```
GET    /api/products          List products
GET    /api/products/{id}     Get by ID
POST   /api/products          Create
PUT    /api/products/{id}     Update
DELETE /api/products/{id}     Delete
GET    /api/products/search   Search
```

Always use DTOs for API contracts — never expose domain models directly.

## Critical Rules

### Data Access

1. **NEVER use Entity Framework** — use ADO.NET with the custom `SqlHelper` base class
2. **Always use parameterized queries** to prevent SQL injection via `CreateParameter(name, value)`
3. **Always filter soft-deleted records** with `WHERE IsDeleted = 0`
4. **Always use transactions** for multi-statement operations
5. **Deadlock handling is automatic** — `SqlHelper` has built-in retry with exponential backoff

**SqlHelper repository example:**

```csharp
public class ProductRepository : SqlHelper
{
    public ProductRepository(string connectionString) : base(connectionString) { }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Name, Brand, UPC
            FROM Product
            WHERE Id = @Id AND IsDeleted = 0";

        var products = await ExecuteReaderAsync(
            sql,
            reader => new Product
            {
                Id = GetGuid(reader, "Id"),
                Name = GetString(reader, "Name"),
                Brand = GetString(reader, "Brand"),
                UPC = GetString(reader, "UPC")
            },
            CreateParameter("@Id", id)
        );

        return products.FirstOrDefault();
    }
}
```

**SqlHelper safe-read helpers:** `GetGuid`, `GetGuidNullable`, `GetString`, `GetInt32`, `GetIntNullable`, `GetBoolean`, `GetBool`, `GetDateTime`, `GetDateTimeNullable`, `GetDecimal`, `GetDecimalNullable`, `CreateParameter`.

### Async/Await

- **Always use async/await** for I/O operations; suffix async methods with `Async`
- **NEVER use `.Result` or `.Wait()`** — causes deadlocks
- Use `ConfigureAwait(false)` in library code

### Architecture Patterns

Each microservice follows **Controller → Service → Repository**:

```csharp
// ✅ DO: separate concerns
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var products = await _service.GetAllAsync();
        return Ok(products);
    }
}

// ❌ DON'T: database access directly in controllers
// ❌ DON'T: var result = _service.GetAsync().Result; // deadlock risk
// ❌ DON'T: expose domain models in API responses
```

### Microservice Structure

All services MUST have:
- `Controllers/` — API endpoints
- `Services/` — Business logic
- `Program.cs` — Service registration and startup

For data-backed services (services that own a database) you MUST also include:
- `Data/` — Repositories and migrations (`Data/Migrations/001_Initial.sql`, …)
- `Models/` — Domain models
- `Contracts/` — DTOs (Requests/Responses)
- `Events/` — Message bus event definitions (where the service publishes or consumes messages)

Services without their own data store (e.g., pure AI or integration services) typically still use `Models/`, `Contracts/`, and `Events/`, but these folders are optional and should be added when they provide clarity.
**Standard `Program.cs` pattern:**

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();  // Aspire: logging, health checks, telemetry

builder.Services.AddSingleton<IProductRepository>(sp =>
    new ProductRepository(builder.Configuration.GetConnectionString("ProductDb")));
builder.Services.AddScoped<IProductService, ProductService>();
builder.AddRedisClient("cache");
builder.AddRabbitMQClient("messaging");
builder.Services.AddControllers();

var app = builder.Build();
await app.RunDatabaseManagementAsync("ProductService", "productdb"); // migrations
app.MapDefaultEndpoints();  // Aspire health checks
app.MapControllers();
app.Run();
```

### Database Schema

All tables MUST include these base columns:

```sql
Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
CreatedBy   UNIQUEIDENTIFIER NULL,
UpdatedAt   DATETIME2        NULL,
UpdatedBy   UNIQUEIDENTIFIER NULL,
IsDeleted   BIT              NOT NULL DEFAULT 0,
DeletedAt   DATETIME2        NULL,
RowVersion  ROWVERSION
```

Use soft deletes (`IsDeleted = 1`) — never hard delete rows.

### Messaging

- Prices, products, recipes, and ingredients services use messaging (RabbitMQ) by default.
- Keep messaging enabled; use runtime REST fallback instead of disabling at startup.
- Both sync REST path and async channel path (`System.Threading.Channels`) exist in each major service.

### Error Handling & Security

- Use structured logging with Serilog; include relevant IDs in log context
- Return appropriate HTTP status codes (200, 201, 400, 404, 500)
- Never expose internal error details to clients; use global exception-handling middleware
- **NEVER commit secrets** — use User Secrets, environment variables, or Azure Key Vault
- Always validate input with Data Annotations; sanitize output to prevent XSS

### Solution Structure

- The active solution file is `ExpressRecipe.sln` — no `.slnx` file exists.
- When reorganizing solution structure, preserve and validate this file.

---

**For detailed guidance, see `CLAUDE.md` in the repository root and `AGENTS.md` for Aspire-specific agent instructions.**
