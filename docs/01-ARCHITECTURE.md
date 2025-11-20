# ExpressRecipe - System Architecture

## Architecture Principles

### 1. Local-First
- Data stored locally by default
- Full offline functionality
- Cloud sync is optional enhancement
- User owns their data

### 2. Microservices
- Bounded contexts for each domain
- Independent deployment
- Fault isolation
- Technology flexibility per service

### 3. API-First
- All services expose REST/gRPC APIs
- Frontend apps are API consumers
- Third-party integration ready
- Versioned contracts

### 4. Security by Design
- Zero-trust architecture
- Encrypted data at rest and in transit
- Principle of least privilege
- Audit logging

### 5. Scalability
- Horizontal scaling for stateless services
- Caching strategies
- Database sharding capabilities
- CDN for static content

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Client Layer                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │  Blazor  │  │ Windows  │  │ Android  │  │   PWA    │       │
│  │   Web    │  │   App    │  │   App    │  │          │       │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘       │
│       │             │              │             │              │
│       └─────────────┴──────────────┴─────────────┘              │
│                          │                                       │
└──────────────────────────┼───────────────────────────────────────┘
                           │
                ┌──────────▼──────────┐
                │   API Gateway       │
                │   (YARP/Aspire)     │
                └──────────┬──────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
┌───────▼────────┐ ┌───────▼────────┐ ┌──────▼───────┐
│  Auth Service  │ │  Sync Service  │ │Cache (Redis) │
└───────┬────────┘ └───────┬────────┘ └──────────────┘
        │                  │
        └──────────┬───────┘
                   │
    ┌──────────────┼──────────────┐
    │              │              │
┌───▼────┐  ┌──────▼─────┐  ┌────▼─────┐
│ User   │  │  Product   │  │  Recipe  │
│Service │  │  Service   │  │  Service │
└───┬────┘  └──────┬─────┘  └────┬─────┘
    │              │              │
┌───▼────┐  ┌──────▼─────┐  ┌────▼─────┐
│Inventory│ │  Shopping  │  │   Meal   │
│Service │  │   Service  │  │  Planner │
└───┬────┘  └──────┬─────┘  └────┬─────┘
    │              │              │
┌───▼────┐  ┌──────▼─────┐  ┌────▼─────┐
│ Price  │  │  Scanner   │  │  Recall  │
│Service │  │  Service   │  │  Service │
└───┬────┘  └──────┬─────┘  └────┬─────┘
    │              │              │
    └──────────────┼──────────────┘
                   │
         ┌─────────▼─────────┐
         │   Message Bus     │
         │ (RabbitMQ/ASB)    │
         └───────────────────┘
```

## .NET Aspire Integration

### What is Aspire?
.NET Aspire is an opinionated, cloud-ready stack for building observable, production-ready, distributed applications. It provides:
- Service discovery
- Configuration management
- Health checks
- Telemetry and logging
- Orchestration for development

### Aspire in ExpressRecipe

**AppHost Project** (`ExpressRecipe.AppHost`)
- Orchestrates all services
- Manages service-to-service communication
- Configures Redis, RabbitMQ, SQL Server
- Development environment setup

**ServiceDefaults Project** (`ExpressRecipe.ServiceDefaults`)
- Shared health checks
- Common telemetry configuration
- Resilience patterns
- Service discovery helpers

**Example Aspire Setup:**
```csharp
// ExpressRecipe.AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var redis = builder.AddRedis("redis");
var rabbitmq = builder.AddRabbitMQ("messaging");
var sqlServer = builder.AddSqlServer("sql")
    .AddDatabase("expressrecipe");

// Services
var auth = builder.AddProject<Projects.ExpressRecipe_AuthService>("auth")
    .WithReference(sqlServer)
    .WithReference(redis);

var products = builder.AddProject<Projects.ExpressRecipe_ProductService>("products")
    .WithReference(sqlServer)
    .WithReference(redis)
    .WithReference(rabbitmq);

var recipes = builder.AddProject<Projects.ExpressRecipe_RecipeService>("recipes")
    .WithReference(sqlServer)
    .WithReference(redis)
    .WithReference(rabbitmq);

// API Gateway
builder.AddProject<Projects.ExpressRecipe_ApiGateway>("gateway")
    .WithReference(auth)
    .WithReference(products)
    .WithReference(recipes);

// Frontend
builder.AddProject<Projects.ExpressRecipe_BlazorWeb>("webapp")
    .WithReference(gateway);

builder.Build().Run();
```

## Service Layer Architecture

### Service Structure (Per Microservice)

```
ExpressRecipe.ProductService/
├── Controllers/              # API endpoints
├── Services/                 # Business logic
│   ├── ProductService.cs
│   └── ProductValidation.cs
├── Data/                     # Data access
│   ├── ProductRepository.cs
│   ├── SqlHelper.cs         # ADO.NET helper
│   └── Migrations/
├── Models/                   # Domain models
│   ├── Product.cs
│   └── Ingredient.cs
├── Contracts/                # DTOs and API contracts
│   ├── Requests/
│   └── Responses/
├── Events/                   # Message bus events
│   └── ProductUpdatedEvent.cs
├── Configuration/            # Service config
│   └── ProductServiceOptions.cs
├── Middleware/              # Custom middleware
└── Program.cs               # Service entry point
```

### ADO.NET Base Helper Class

**Benefits:**
- Maximum performance
- Full control over SQL
- Minimal dependencies
- Easy debugging
- Explicit data access

**Base Helper Design:**
```csharp
public abstract class SqlHelper
{
    protected string ConnectionString { get; }

    protected async Task<T> ExecuteScalarAsync<T>(
        string sql,
        params SqlParameter[] parameters);

    protected async Task<int> ExecuteNonQueryAsync(
        string sql,
        params SqlParameter[] parameters);

    protected async Task<List<T>> ExecuteReaderAsync<T>(
        string sql,
        Func<SqlDataReader, T> mapper,
        params SqlParameter[] parameters);

    protected async Task<T> ExecuteTransactionAsync<T>(
        Func<SqlConnection, SqlTransaction, Task<T>> operation);
}
```

## Data Storage Strategy

### Local Storage (SQLite)

**Each client maintains:**
- User profile and preferences
- Downloaded recipes
- Personal inventory
- Shopping lists
- Cached product data
- Offline sync queue

**Location:**
- Windows: `%APPDATA%/ExpressRecipe/local.db`
- Android: `data/data/com.expressrecipe/databases/local.db`
- Web: IndexedDB (via Blazor)

### Cloud Storage (SQL Server)

**Database per Service:**
- `ExpressRecipe.Users`
- `ExpressRecipe.Products`
- `ExpressRecipe.Recipes`
- `ExpressRecipe.Inventory`
- `ExpressRecipe.Shopping`
- `ExpressRecipe.Pricing`
- `ExpressRecipe.Recalls`

**Partitioning Strategy:**
- Products: By category hash
- User data: By user ID hash
- Pricing: By region
- Historical: By date range

### Caching Strategy (Redis)

**Cache Layers:**
1. **Product Cache** - Frequently accessed products (1 hour TTL)
2. **Recipe Cache** - Popular recipes (30 min TTL)
3. **Price Cache** - Latest prices (15 min TTL)
4. **User Session** - Active user data (session lifetime)
5. **Search Results** - Recent searches (5 min TTL)

**Cache Patterns:**
- Cache-aside for products
- Write-through for prices
- Refresh-ahead for popular recipes

## Communication Patterns

### Synchronous (REST/gRPC)

**When to use:**
- Client → Gateway → Service
- Service → Service (critical path)
- Read operations
- Real-time data needs

**Example:** Product lookup during scanning

### Asynchronous (Message Bus)

**When to use:**
- Service → Service (non-critical)
- Event notifications
- Background processing
- Data sync

**Example:** Product update triggers inventory recalculation

### Event Types

**Domain Events:**
- `UserCreated`
- `ProductAdded`
- `RecipeShared`
- `InventoryItemExpired`
- `RecallIssued`

**Integration Events:**
- `PricingUpdated`
- `ProductDataSynced`
- `UserPreferencesChanged`

## Security Architecture

### Authentication & Authorization

**OAuth 2.0 + OpenID Connect:**
- Local identity provider (Duende IdentityServer or custom)
- Support for external providers (Google, Microsoft)
- JWT tokens for API access
- Refresh token rotation

**Claims-Based Authorization:**
- User ID
- Subscription tier (Free/Premium/Family)
- Roles (User, Moderator, Admin)
- Permissions (can_submit_products, can_moderate, etc.)

### Data Encryption

**At Rest:**
- TDE for SQL Server databases
- SQLCipher for SQLite local databases
- Encrypted fields for sensitive data (medical conditions)

**In Transit:**
- TLS 1.3 for all API communication
- Certificate pinning for mobile apps

### Privacy

**Data Minimization:**
- Only sync what's necessary
- Local-only option for sensitive data
- Automatic data retention policies

**User Control:**
- Export all data (GDPR)
- Delete account and all data
- Opt-in for cloud sync
- Granular sharing preferences

## Observability

### Logging (Serilog)

**Structured Logging:**
- Trace: Development debugging
- Debug: Detailed flow
- Information: Key events
- Warning: Degraded performance
- Error: Failures
- Critical: System down

**Log Sinks:**
- Console (Development)
- File (All environments)
- Seq (Centralized, Development/Staging)
- Application Insights (Production)

### Metrics (OpenTelemetry)

**Application Metrics:**
- Request rate, duration, errors
- Database query performance
- Cache hit/miss rates
- Queue depths
- Business metrics (scans/day, recipes saved)

**Infrastructure Metrics:**
- CPU, memory, disk usage
- Network I/O
- Database connections
- Queue processing rate

### Distributed Tracing

**Trace Context:**
- Propagated across all service calls
- Includes user ID for debugging
- Performance bottleneck identification

**Trace Spans:**
- HTTP requests
- Database queries
- External API calls
- Message processing

### Health Checks

**Liveness:**
- Service is running
- Basic connectivity

**Readiness:**
- Database accessible
- Dependencies available
- Can serve requests

**Startup:**
- Migration status
- Cache warming
- Configuration loaded

## Deployment Architecture

### Development
- Local Aspire orchestration
- Docker containers for infrastructure
- SQLite for local data
- In-memory message bus

### Staging
- Azure Container Apps
- Azure SQL Database
- Azure Cache for Redis
- Azure Service Bus
- Application Insights

### Production
- Multi-region Azure deployment
- Geo-replicated databases
- CDN for static content
- Azure Front Door for global routing
- Auto-scaling based on metrics

### CI/CD Pipeline

**Build:**
1. Code checkout
2. Restore dependencies
3. Build all projects
4. Run unit tests
5. Run integration tests
6. Build containers
7. Push to container registry

**Deploy:**
1. Deploy infrastructure (Bicep/Terraform)
2. Run database migrations
3. Deploy services (blue-green)
4. Run smoke tests
5. Switch traffic
6. Monitor metrics

## Resilience Patterns

### Circuit Breaker
- Prevent cascading failures
- Automatic recovery attempts
- Fallback to cached data

### Retry with Backoff
- Transient failure handling
- Exponential backoff
- Maximum retry count

### Timeout
- Prevent hanging requests
- Per-operation timeouts
- Graceful degradation

### Bulkhead
- Isolate critical resources
- Separate thread pools
- Connection pool limits

### Rate Limiting
- Per-user limits
- Per-IP limits
- API quota enforcement

## Scalability Considerations

### Horizontal Scaling
- Stateless services (easy scale)
- Sticky sessions for web app
- Distributed cache for shared state

### Database Scaling
- Read replicas for queries
- Write to primary only
- Sharding for user data
- CQRS for complex queries

### Performance Optimization
- Lazy loading
- Pagination
- Background jobs
- Async processing
- Response compression

## Technology Stack Summary

| Component | Technology | Purpose |
|-----------|------------|---------|
| Framework | .NET 10 | Latest features, performance |
| Orchestration | .NET Aspire | Service coordination |
| API Gateway | YARP | Reverse proxy, routing |
| Auth | Duende IdentityServer | OAuth/OIDC |
| Web UI | Blazor Server/WASM | Interactive web app |
| Desktop | WPF/WinUI | Native Windows app |
| Mobile | .NET MAUI | Android (iOS future) |
| Data Access | ADO.NET | Performance, control |
| Cloud DB | Azure SQL | Scalable relational DB |
| Local DB | SQLite | Offline storage |
| Cache | Redis | Fast in-memory cache |
| Messaging | RabbitMQ/ASB | Async communication |
| Logging | Serilog | Structured logging |
| Metrics | OpenTelemetry | Observability |
| Tracing | OpenTelemetry | Distributed tracing |
| Hosting | Azure Container Apps | Serverless containers |
| CDN | Azure CDN | Static content delivery |
| CI/CD | GitHub Actions | Automation |
| IaC | Bicep | Infrastructure as code |

## Development Environment Setup

### Prerequisites
- .NET 10 SDK
- Visual Studio 2024 or VS Code
- Docker Desktop
- Azure subscription (for cloud features)
- SQL Server (or Docker image)
- Git

### Initial Setup
```bash
# Clone repository
git clone https://github.com/yourusername/ExpressRecipe.git
cd ExpressRecipe

# Restore dependencies
dotnet restore

# Run Aspire AppHost
cd src/ExpressRecipe.AppHost
dotnet run

# Access Aspire dashboard
# http://localhost:15000
```

### Local Development URLs
- Aspire Dashboard: http://localhost:15000
- API Gateway: http://localhost:5000
- Blazor Web: http://localhost:5001
- Auth Service: http://localhost:5100
- Product Service: http://localhost:5101
- Recipe Service: http://localhost:5102

## Next Steps
See individual service architecture in `02-MICROSERVICES.md`
