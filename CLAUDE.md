# CLAUDE.md - AI Assistant Guide for ExpressRecipe

**Last Updated:** 2025-11-19
**Project Status:** Planning Complete - Ready for Development

---

## Project Overview

**ExpressRecipe** is a local-first, cloud-capable dietary management platform built with .NET 10, Aspire, and microservices architecture. The application helps individuals with dietary restrictions (medical, religious, health-related, or personal) manage food choices, meal planning, inventory, and shopping.

**Key Differentiators:**
- Local-first architecture (works offline)
- Real-time barcode scanning with allergen alerts
- AI-powered recommendations and pattern detection
- Community-driven product database
- Multi-platform (Web, Windows, Android, PWA)

---

## Architecture Principles

### 1. Local-First Design
- All user data stored locally (SQLite) by default
- Cloud sync is optional enhancement
- Full offline functionality
- User owns their data

### 2. Microservices Architecture
- 15 specialized services (Auth, Products, Recipes, Inventory, Shopping, etc.)
- Bounded contexts per domain
- Independent deployment
- Event-driven communication

### 3. .NET Aspire Orchestration
- AppHost manages all services
- Service discovery and configuration
- Built-in observability
- Development environment coordination

### 4. ADO.NET Data Access
- No Entity Framework (performance reasons)
- Custom `SqlHelper` base class
- Direct SQL with full control
- Explicit and debuggable

### 5. Multi-Platform Frontends
- **Blazor** (Web) - Auto rendering mode (Server → WASM)
- **WinUI 3** (Windows) - Native desktop app
- **.NET MAUI** (Android/iOS) - Mobile apps
- **PWA** - Progressive web app fallback

---

## Project Structure

```
ExpressRecipe/
├── old/                          # Previous code generation project (archived)
├── docs/                         # Comprehensive planning documents
│   ├── 00-PROJECT-OVERVIEW.md
│   ├── 01-ARCHITECTURE.md
│   ├── 02-MICROSERVICES.md
│   ├── 03-DATA-MODELS.md
│   ├── 04-LOCAL-FIRST-SYNC.md
│   ├── 05-FRONTEND-ARCHITECTURE.md
│   └── 06-IMPLEMENTATION-ROADMAP.md
├── src/                          # Source code (to be created)
│   ├── ExpressRecipe.AppHost/           # Aspire orchestration
│   ├── ExpressRecipe.ServiceDefaults/   # Shared defaults
│   ├── ExpressRecipe.Shared/            # Shared models/DTOs
│   ├── ExpressRecipe.Data.Common/       # ADO.NET helpers
│   ├── Services/                        # Microservices
│   │   ├── ExpressRecipe.AuthService/
│   │   ├── ExpressRecipe.UserService/
│   │   ├── ExpressRecipe.ProductService/
│   │   ├── ExpressRecipe.RecipeService/
│   │   ├── ExpressRecipe.InventoryService/
│   │   ├── ExpressRecipe.ShoppingService/
│   │   ├── ExpressRecipe.MealPlanningService/
│   │   ├── ExpressRecipe.PriceService/
│   │   ├── ExpressRecipe.ScannerService/
│   │   ├── ExpressRecipe.RecallService/
│   │   ├── ExpressRecipe.NotificationService/
│   │   ├── ExpressRecipe.CommunityService/
│   │   ├── ExpressRecipe.SyncService/
│   │   ├── ExpressRecipe.SearchService/
│   │   └── ExpressRecipe.AnalyticsService/
│   ├── Frontends/
│   │   ├── ExpressRecipe.BlazorWeb/     # Web app
│   │   ├── ExpressRecipe.Windows/       # WinUI 3 app
│   │   └── ExpressRecipe.MAUI/          # Mobile apps
│   └── Tests/
│       ├── ExpressRecipe.Tests.Unit/
│       ├── ExpressRecipe.Tests.Integration/
│       └── ExpressRecipe.Tests.E2E/
├── README.md
└── CLAUDE.md
```

---

## Technology Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| **Framework** | .NET 10 | Latest features, performance |
| **Orchestration** | .NET Aspire | Service coordination, dev environment |
| **Data Access** | ADO.NET | Maximum performance and control |
| **Cloud DB** | SQL Server | Scalable relational database |
| **Local DB** | SQLite | Offline storage |
| **Cache** | Redis | Fast in-memory cache |
| **Messaging** | RabbitMQ/ASB | Async communication |
| **Web UI** | Blazor | Interactive web application |
| **Desktop** | WinUI 3 | Native Windows app |
| **Mobile** | .NET MAUI | Android/iOS apps |
| **Auth** | Duende/Custom | OAuth 2.0 / OpenID Connect |
| **Logging** | Serilog | Structured logging |
| **Metrics** | OpenTelemetry | Observability |
| **Hosting** | Azure Container Apps | Serverless containers |
| **CI/CD** | GitHub Actions | Automation |

---

## Key Design Patterns

### ADO.NET Base Helper
```csharp
public abstract class SqlHelper
{
    protected string ConnectionString { get; }

    protected async Task<T> ExecuteScalarAsync<T>(
        string sql, params SqlParameter[] parameters);

    protected async Task<int> ExecuteNonQueryAsync(
        string sql, params SqlParameter[] parameters);

    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<SqlDataReader, T> mapper,
        params SqlParameter[] parameters);

    protected async Task<T> ExecuteTransactionAsync<T>(
        Func<SqlConnection, SqlTransaction, Task<T>> operation);
}
```

### Repository Pattern
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
                Id = reader.GetGuid("Id"),
                Name = reader.GetString("Name"),
                Brand = reader.GetString("Brand"),
                UPC = reader.GetString("UPC")
            },
            new SqlParameter("@Id", id)
        );

        return products.FirstOrDefault();
    }
}
```

### CQRS Pattern (for complex queries)
```csharp
// Command (write)
public record CreateProductCommand(string Name, string Brand);

// Query (read)
public record GetProductsByBrandQuery(string Brand);

// Handler
public class CreateProductHandler
{
    public async Task<Guid> Handle(CreateProductCommand command)
    {
        // Insert and return ID
    }
}
```

### Event Sourcing (for audit-critical data)
```csharp
// Events
public record ProductCreatedEvent(Guid ProductId, string Name);
public record ProductUpdatedEvent(Guid ProductId, string Name);

// Aggregate
public class ProductAggregate
{
    private List<object> _events = new();

    public void Create(string name)
    {
        _events.Add(new ProductCreatedEvent(Guid.NewGuid(), name));
    }
}
```

---

## Coding Conventions

### Naming Standards
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

### API Endpoint Conventions
```
GET    /api/products          - List products
GET    /api/products/{id}     - Get product by ID
POST   /api/products          - Create product
PUT    /api/products/{id}     - Update product
DELETE /api/products/{id}     - Delete product
GET    /api/products/search   - Search products
```

### DTOs vs Domain Models
- **Domain Models**: Internal business objects
- **DTOs**: API contracts (request/response)
- Never expose domain models directly in APIs
- Use mapping libraries (AutoMapper) or manual mapping

---

## Microservice Structure

### Standard Service Layout
```
ExpressRecipe.ProductService/
├── Controllers/              # API endpoints
│   └── ProductsController.cs
├── Services/                 # Business logic
│   ├── ProductService.cs
│   └── ProductValidator.cs
├── Data/                     # Data access
│   ├── ProductRepository.cs
│   ├── SqlHelper.cs
│   └── Migrations/
│       └── 001_CreateProductTable.sql
├── Models/                   # Domain models
│   ├── Product.cs
│   └── Ingredient.cs
├── Contracts/                # DTOs
│   ├── Requests/
│   │   └── CreateProductRequest.cs
│   └── Responses/
│       └── ProductResponse.cs
├── Events/                   # Message bus events
│   └── ProductCreatedEvent.cs
├── Configuration/
│   └── ProductServiceOptions.cs
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
├── Program.cs
└── appsettings.json
```

### Service Registration (Program.cs)
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

---

## Database Guidelines

### Migration Strategy
- SQL scripts in `Migrations/` folder
- Numbered sequentially: `001_Initial.sql`, `002_AddIndexes.sql`
- Run migrations on startup in development
- Use DbUp or FluentMigrator for production

### Schema Conventions
```sql
-- Base entity pattern (all tables)
Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
CreatedBy UNIQUEIDENTIFIER NULL,
UpdatedAt DATETIME2 NULL,
UpdatedBy UNIQUEIDENTIFIER NULL,
IsDeleted BIT NOT NULL DEFAULT 0,
DeletedAt DATETIME2 NULL,
RowVersion ROWVERSION

-- Indexes
CREATE INDEX IX_{TableName}_{ColumnName} ON {TableName}({ColumnName});

-- Foreign keys
CONSTRAINT FK_{ParentTable}_{ChildTable}_{Column}
    FOREIGN KEY ({Column}) REFERENCES {ParentTable}(Id)

-- Check constraints
CONSTRAINT CK_{TableName}_{Rule}
    CHECK ({Condition})
```

### Soft Deletes
```sql
-- Mark as deleted (don't actually delete)
UPDATE Product
SET IsDeleted = 1, DeletedAt = GETUTCDATE()
WHERE Id = @Id;

-- Always filter in queries
SELECT * FROM Product
WHERE IsDeleted = 0;
```

---

## Testing Strategy

### Test Pyramid
```
      /\        E2E Tests (5%)
     /  \       - Full workflows
    /────\      Integration Tests (15%)
   /      \     - API + DB
  /────────\    Unit Tests (80%)
 /          \   - Business logic
```

### Unit Tests (xUnit)
```csharp
public class ProductServiceTests
{
    [Fact]
    public async Task CreateProduct_WithValidData_ReturnsProductId()
    {
        // Arrange
        var mockRepo = new Mock<IProductRepository>();
        var service = new ProductService(mockRepo.Object);

        // Act
        var id = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Test Product",
            Brand = "Test Brand"
        });

        // Assert
        Assert.NotEqual(Guid.Empty, id);
        mockRepo.Verify(r => r.InsertAsync(It.IsAny<Product>()), Times.Once);
    }
}
```

### Integration Tests
```csharp
public class ProductApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ProductApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProduct_ExistingId_ReturnsProduct()
    {
        // Arrange
        var productId = await CreateTestProductAsync();

        // Act
        var response = await _client.GetAsync($"/api/products/{productId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);
    }
}
```

---

## Sync Implementation

### Sync Queue Pattern
```csharp
public class SyncQueue
{
    public async Task EnqueueAsync(string entityType, Guid entityId, object data)
    {
        await _localDb.ExecuteAsync(@"
            INSERT INTO SyncQueue (EntityType, EntityId, Data, CreatedAt)
            VALUES (@Type, @Id, @Data, @Now)",
            new { Type = entityType, Id = entityId, Data = JsonSerializer.Serialize(data), Now = DateTime.UtcNow });
    }

    public async Task ProcessQueueAsync()
    {
        var pending = await _localDb.QueryAsync<SyncQueueItem>(
            "SELECT * FROM SyncQueue WHERE IsSynced = 0 LIMIT 100");

        var response = await _httpClient.PostAsJsonAsync("/sync/push", pending);

        foreach (var id in response.AcceptedIds)
        {
            await _localDb.ExecuteAsync(
                "UPDATE SyncQueue SET IsSynced = 1 WHERE EntityId = ?", id);
        }
    }
}
```

---

## AI Assistant Workflows

### When Adding a New Feature

1. **Review Planning Docs**
   - Check if feature is in roadmap
   - Identify affected services
   - Review data model requirements

2. **Design First**
   - Sketch API endpoints
   - Define DTOs
   - Plan database changes
   - Consider sync implications

3. **Implement Backend**
   - Database migration
   - Repository methods
   - Service logic
   - Controller endpoints
   - Event publishing (if needed)

4. **Implement Frontend**
   - ViewModels
   - UI components
   - State management
   - API integration

5. **Add Tests**
   - Unit tests for business logic
   - Integration tests for APIs
   - E2E test for critical paths

6. **Document**
   - Update CLAUDE.md if architectural change
   - Add XML comments to public APIs
   - Update README if user-facing

### When Fixing Bugs

1. **Reproduce**
   - Add failing test first (TDD)
   - Understand root cause
   - Check if it affects multiple services

2. **Fix**
   - Minimal change to fix issue
   - Ensure test passes
   - Check for similar issues elsewhere

3. **Verify**
   - All tests pass
   - Manual testing if UI-related
   - Performance impact minimal

### When Refactoring

1. **Safety First**
   - Ensure good test coverage
   - Make small, incremental changes
   - Keep tests green

2. **Patterns**
   - Extract interfaces for testability
   - Use dependency injection
   - Follow SOLID principles

3. **Performance**
   - Measure before and after
   - Profile if performance-critical
   - Cache appropriately

---

## Common Pitfalls

### 1. Don't Mix Concerns
❌ **Bad**: Controller with database access
```csharp
public class ProductsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        using var conn = new SqlConnection(_connectionString);
        // Direct SQL in controller - BAD
    }
}
```

✅ **Good**: Controller → Service → Repository
```csharp
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
```

### 2. Don't Forget Async/Await
❌ **Bad**: Blocking calls
```csharp
var result = _service.GetAsync().Result; // Deadlock risk!
```

✅ **Good**: Proper async
```csharp
var result = await _service.GetAsync();
```

### 3. Don't Expose Domain Models
❌ **Bad**: Domain model in API
```csharp
[HttpPost]
public async Task<Product> Create(Product product) // Domain model exposed
```

✅ **Good**: Use DTOs
```csharp
[HttpPost]
public async Task<ProductResponse> Create(CreateProductRequest request)
```

### 4. Don't Ignore Sync
- Every create/update/delete must be queued for sync (if applicable)
- Think about offline scenarios
- Handle conflicts gracefully

### 5. Don't Skip Validation
```csharp
public class CreateProductRequest
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; }

    [Required]
    public string Brand { get; set; }
}
```

---

## Development Workflow

### Daily Development
```bash
# Start Aspire (all services)
cd src/ExpressRecipe.AppHost
dotnet run

# Run specific service
cd src/Services/ExpressRecipe.ProductService
dotnet run

# Run tests
dotnet test

# Watch tests
dotnet watch test
```

### Git Workflow
```bash
# Feature branch
git checkout -b feature/add-barcode-scanner

# Commit often
git add .
git commit -m "Add barcode scanning endpoint"

# Push and create PR
git push -u origin feature/add-barcode-scanner
```

### Commit Message Format
```
<type>: <short description>

<optional detailed explanation>
<status notes if incomplete>

Types: feat, fix, refactor, test, docs, chore
```

---

## Resources

### Planning Documents
Read these first when working on features:
- `docs/00-PROJECT-OVERVIEW.md` - Vision and features
- `docs/01-ARCHITECTURE.md` - System architecture
- `docs/02-MICROSERVICES.md` - Service boundaries
- `docs/03-DATA-MODELS.md` - Database schemas
- `docs/04-LOCAL-FIRST-SYNC.md` - Sync patterns
- `docs/05-FRONTEND-ARCHITECTURE.md` - UI architecture
- `docs/06-IMPLEMENTATION-ROADMAP.md` - Development phases

### External Documentation
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [.NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/)
- [WinUI 3](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)

---

## Current Status & Next Steps

**Status**: Planning Complete ✅

**Next Immediate Actions**:
1. Create .NET solution file
2. Set up ExpressRecipe.AppHost (Aspire)
3. Create all service projects
4. Implement SqlHelper base class
5. Create first migration (Auth.User table)
6. Implement user registration

**Current Phase**: Phase 0 → Phase 1 Transition

**See**: `docs/06-IMPLEMENTATION-ROADMAP.md` for detailed next steps

---

*This guide helps AI assistants understand the ExpressRecipe architecture and development practices. Update it when significant patterns or conventions change.*
