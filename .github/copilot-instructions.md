# GitHub Copilot Instructions for ExpressRecipe

## Project Overview

**ExpressRecipe** is a local-first, cloud-capable dietary management platform built with .NET 10, Aspire, and microservices architecture. The application helps individuals with dietary restrictions (medical, religious, health-related, or personal) manage food choices, meal planning, inventory, and shopping.

**Key Architecture Principles:**
- **Local-first design** - All user data stored locally (SQLite) by default, cloud sync is optional
- **Microservices** - 15 specialized services with bounded contexts
- **.NET Aspire orchestration** - Service discovery, configuration, and observability
- **ADO.NET data access** - Direct SQL for performance, no Entity Framework
- **Multi-platform** - Blazor Web, WinUI 3, .NET MAUI, PWA

## Technology Stack

- **Framework**: .NET 10
- **Orchestration**: .NET Aspire
- **Data Access**: ADO.NET (custom `SqlHelper` base class)
- **Databases**: SQL Server (cloud), SQLite (local)
- **Cache**: Redis
- **Messaging**: RabbitMQ/Azure Service Bus
- **Web UI**: Blazor (Auto rendering mode)
- **Desktop**: WinUI 3
- **Mobile**: .NET MAUI
- **Auth**: OAuth 2.0 / OpenID Connect
- **Logging**: Serilog with structured logging
- **Metrics**: OpenTelemetry
- **AI**: Ollama (llama2, mistral, codellama)

## Project Structure

```
src/
├── ExpressRecipe.AppHost/          # Aspire orchestration
├── ExpressRecipe.ServiceDefaults/   # Shared defaults
├── ExpressRecipe.Shared/            # Shared models/DTOs
├── ExpressRecipe.Data.Common/       # ADO.NET helpers
├── Services/                        # Microservices
│   ├── ExpressRecipe.AuthService/
│   ├── ExpressRecipe.ProductService/
│   ├── ExpressRecipe.RecipeService/
│   ├── ExpressRecipe.InventoryService/
│   ├── ExpressRecipe.ShoppingService/
│   ├── ExpressRecipe.MealPlanningService/
│   ├── ExpressRecipe.AIService/
│   ├── ExpressRecipe.NotificationService/
│   ├── ExpressRecipe.AnalyticsService/
│   ├── ExpressRecipe.CommunityService/
│   ├── ExpressRecipe.PriceService/
│   └── ... (15 services total)
└── Frontends/
    ├── ExpressRecipe.BlazorWeb/
    ├── ExpressRecipe.Windows/
    └── ExpressRecipe.MAUI/
```

## Coding Standards

### Naming Conventions

- **Classes**: PascalCase (`ProductService`, `InventoryRepository`)
- **Interfaces**: IPascalCase (`IProductService`, `IRepository<T>`)
- **Methods**: PascalCase (`GetProductByIdAsync`)
- **Parameters**: camelCase (`productId`, `userName`)
- **Private fields**: `_camelCase` (`_httpClient`, `_connectionString`)
- **Constants**: PascalCase (`MaxRetryAttempts`)
- **Database tables**: PascalCase, singular (`Product`, `User`)
- **Database columns**: PascalCase (`ProductId`, `CreatedAt`)

### File Organization

- One class per file
- Namespace matches folder structure
- File name matches primary class name
- `Program.cs` for service entry points
- `*.Tests.cs` for test files

### API Design

**RESTful conventions:**
```
GET    /api/products          - List products
GET    /api/products/{id}     - Get product by ID
POST   /api/products          - Create product
PUT    /api/products/{id}     - Update product
DELETE /api/products/{id}     - Delete product
GET    /api/products/search   - Search products
```

**Always use DTOs for API contracts, never expose domain models directly.**

## Critical Rules

### Data Access

1. **NEVER use Entity Framework** - Use ADO.NET with the custom `SqlHelper` base class
2. **Always use the `SqlHelper` base class** for data access - provides `ExecuteScalarAsync`, `ExecuteNonQueryAsync`, `ExecuteReaderAsync`, `ExecuteTransactionAsync`
3. **Always use parameterized queries** to prevent SQL injection
4. **Always filter soft-deleted records** with `WHERE IsDeleted = 0`
5. **Always use transactions** for multi-statement operations

Example:
```csharp
public class ProductRepository : SqlHelper
{
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
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Brand = reader.GetString(2),
                UPC = reader.GetString(3)
            },
            new SqlParameter("@Id", id)
        );

        return products.FirstOrDefault();
    }
}
```

### Architecture Patterns

1. **Repository Pattern** - All data access through repositories
2. **Service Layer** - Business logic in service classes
3. **DTOs for API boundaries** - Request/Response objects separate from domain models
4. **Dependency Injection** - Use built-in DI container
5. **CQRS** - Separate command and query models for complex operations

### Microservice Structure

Each microservice MUST have:
- `Controllers/` - API endpoints
- `Services/` - Business logic
- `Data/` - Repositories and data access
- `Models/` - Domain models
- `Contracts/` - DTOs (Request/Response)
- `Events/` - Message bus event definitions
- `Program.cs` - Service registration and startup

### Database Schema Rules

All tables MUST include these base columns:
```sql
Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
CreatedBy UNIQUEIDENTIFIER NULL,
UpdatedAt DATETIME2 NULL,
UpdatedBy UNIQUEIDENTIFIER NULL,
IsDeleted BIT NOT NULL DEFAULT 0,
DeletedAt DATETIME2 NULL,
RowVersion ROWVERSION
```

### Async/Await

1. **Always use async/await** for I/O operations
2. **NEVER use `.Result` or `.Wait()`** - causes deadlocks
3. **Always suffix async methods with `Async`**
4. **Always use `ConfigureAwait(false)`** in library code

### Error Handling

1. **Use structured logging** with Serilog
2. **Log exceptions with context** - include relevant IDs and parameters
3. **Return appropriate HTTP status codes** (200, 201, 400, 404, 500)
4. **Never expose internal error details** to clients
5. **Use middleware for global exception handling**

### Testing

1. **Unit tests** - 80% of test coverage (business logic)
2. **Integration tests** - 15% (API + DB)
3. **E2E tests** - 5% (critical workflows)
4. **Use xUnit** for all tests
5. **Follow AAA pattern** (Arrange, Act, Assert)
6. **Mock external dependencies** in unit tests

### Synchronization

1. **Every create/update/delete** must enqueue for sync (if applicable)
2. **Think offline-first** - operations must work without network
3. **Handle conflicts gracefully** - last-write-wins or custom resolution
4. **Use sync queue pattern** for reliable cloud sync

## Common Pitfalls to Avoid

### ❌ DON'T

```csharp
// DON'T mix concerns - no database access in controllers
public class ProductsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        using var conn = new SqlConnection(_connectionString);
        // Direct SQL in controller - BAD
    }
}

// DON'T use blocking calls
var result = _service.GetAsync().Result; // Deadlock risk!

// DON'T expose domain models
[HttpPost]
public async Task<Product> Create(Product product) // Domain model exposed
```

### ✅ DO

```csharp
// DO separate concerns - Controller → Service → Repository
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

// DO use proper async
var result = await _service.GetAsync();

// DO use DTOs for APIs
[HttpPost]
public async Task<ProductResponse> Create(CreateProductRequest request)
```

## Development Workflow

### Building and Testing

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run all tests
dotnet test

# Run specific service
cd src/Services/ExpressRecipe.ProductService
dotnet run

# Run with Aspire (all services)
cd src/ExpressRecipe.AppHost
dotnet run
```

### Docker Development

```bash
# Start all services
docker compose up -d

# View logs
docker compose logs -f

# Stop services
docker compose down
```

### Migrations

- SQL scripts in `Data/Migrations/` folder
- Numbered sequentially: `001_Initial.sql`, `002_AddIndexes.sql`
- Run on startup in development
- Use DbUp or FluentMigrator for production

## Additional Resources

- **Planning Docs**: See `/docs` folder for comprehensive architecture documentation
- **CLAUDE.md**: Detailed AI assistant guide with code examples and patterns
- **README.md**: Project overview and getting started guide

## Aspire Service Registration

Each service should register with Aspire defaults:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Aspire defaults (logging, health checks, telemetry)
builder.AddServiceDefaults();

// Database
builder.Services.AddSingleton<IProductRepository>(sp =>
    new ProductRepository(builder.Configuration.GetConnectionString("ProductDb")));

// Business logic
builder.Services.AddScoped<IProductService, ProductService>();

// Caching
builder.AddRedisClient("cache");

// Message bus
builder.AddRabbitMQClient("messaging");

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

app.MapDefaultEndpoints(); // Aspire health checks
app.MapControllers();
app.Run();
```

## Security

1. **NEVER commit secrets** - use User Secrets, environment variables, or Azure Key Vault
2. **Always validate input** - use Data Annotations and FluentValidation
3. **Always sanitize output** - prevent XSS attacks
4. **Use authentication middleware** - verify JWT tokens
5. **Follow OWASP guidelines** for secure coding

## AI Integration (Ollama)

- **AIService** handles all AI operations
- Use `llama2` for general recommendations
- Use `mistral` for complex reasoning
- Use `codellama` for technical queries
- Always provide context and examples in prompts
- Handle timeout gracefully (AI operations can be slow)

---

**For more detailed guidance, see `CLAUDE.md` in the repository root.**
