# ExpressRecipe - Development Environment Setup

**Last Updated:** 2025-11-19

## Prerequisites

### Required Software

1. **.NET 9.0 SDK** (or later)
   - Download: https://dotnet.microsoft.com/download
   - Verify: `dotnet --version` (should show 9.0.x or higher)

2. **Visual Studio 2024** (Recommended) OR **Visual Studio Code**
   - Visual Studio 2024: https://visualstudio.microsoft.com/
     - Workloads: ASP.NET and web development, .NET Aspire
   - VS Code: https://code.visualstudio.com/
     - Extensions: C# Dev Kit, .NET Aspire

3. **Docker Desktop** (Required for Aspire)
   - Download: https://www.docker.com/products/docker-desktop
   - Used for: Redis, RabbitMQ, SQL Server containers

4. **Git**
   - Download: https://git-scm.com/
   - Verify: `git --version`

### Optional (But Recommended)

- **SQL Server Management Studio** (for database management)
- **Azure Data Studio** (lighter alternative to SSMS)
- **Postman** or **REST Client** (for API testing)
- **Azure CLI** (for cloud deployment)

---

## Initial Setup

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/ExpressRecipe.git
cd ExpressRecipe
```

### 2. Restore Dependencies

```bash
dotnet restore ExpressRecipe.sln
```

### 3. Install .NET Aspire Workload

```bash
dotnet workload update
dotnet workload install aspire
```

### 4. Start Docker Desktop

Ensure Docker Desktop is running before starting Aspire.

### 5. Run the Application

#### Option A: Visual Studio 2024
1. Open `ExpressRecipe.sln`
2. Set `ExpressRecipe.AppHost` as startup project
3. Press F5 or click "Run"
4. Aspire Dashboard will open in your browser

#### Option B: Command Line
```bash
cd src/ExpressRecipe.AppHost
dotnet run
```

The Aspire Dashboard will be available at: **http://localhost:15000**

---

## Project Structure

```
ExpressRecipe/
├── src/
│   ├── ExpressRecipe.AppHost/           # .NET Aspire orchestration
│   ├── ExpressRecipe.ServiceDefaults/   # Shared Aspire configuration
│   ├── ExpressRecipe.Shared/            # Shared models, DTOs, interfaces
│   ├── ExpressRecipe.Data.Common/       # ADO.NET helpers (SqlHelper, SqliteHelper)
│   ├── Services/
│   │   ├── ExpressRecipe.AuthService/        # Authentication & authorization
│   │   ├── ExpressRecipe.UserService/        # User profiles & preferences
│   │   ├── ExpressRecipe.ProductService/     # Product catalog
│   │   ├── ExpressRecipe.RecipeService/      # Recipe management
│   │   ├── ExpressRecipe.InventoryService/   # Food inventory
│   │   ├── ExpressRecipe.ShoppingService/    # Shopping lists
│   │   ├── ExpressRecipe.MealPlanningService/# Meal planning
│   │   ├── ExpressRecipe.PriceService/       # Price tracking
│   │   ├── ExpressRecipe.ScannerService/     # Barcode scanning
│   │   ├── ExpressRecipe.RecallService/      # FDA/USDA recalls
│   │   ├── ExpressRecipe.NotificationService/# Notifications
│   │   ├── ExpressRecipe.CommunityService/   # User-generated content
│   │   ├── ExpressRecipe.SyncService/        # Cloud sync
│   │   ├── ExpressRecipe.SearchService/      # Full-text search
│   │   └── ExpressRecipe.AnalyticsService/   # Usage analytics
│   ├── Frontends/
│   │   ├── ExpressRecipe.BlazorWeb/     # Blazor web application
│   │   ├── ExpressRecipe.Windows/       # WinUI 3 desktop app
│   │   └── ExpressRecipe.MAUI/          # Android/iOS mobile app
│   └── Tests/
│       ├── Unit/                        # Unit tests
│       ├── Integration/                 # Integration tests
│       └── E2E/                         # End-to-end tests
├── docs/                                # Planning documentation
├── old/                                 # Archived previous project
├── ExpressRecipe.sln                    # Solution file
└── README.md
```

---

## Database Setup

### Local Development (SQL Server via Docker)

Aspire automatically starts SQL Server in a container. Connection strings are managed by Aspire.

**Manual Setup (if needed):**
```bash
# Pull SQL Server image
docker pull mcr.microsoft.com/mssql/server:2022-latest

# Run SQL Server container
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" \
   -p 1433:1433 --name expressrecipe-sqlserver \
   -d mcr.microsoft.com/mssql/server:2022-latest
```

**Connection String:**
```
Server=localhost,1433;Database=ExpressRecipe.Auth;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
```

### Apply Migrations

```bash
# Navigate to service
cd src/Services/ExpressRecipe.AuthService

# Run migrations (when implemented)
dotnet run -- migrate
```

---

## Aspire Dashboard

Once running, the Aspire Dashboard provides:

- **Service URLs**: Links to all running services
- **Logs**: Centralized logging from all services
- **Metrics**: Performance metrics and traces
- **Health Checks**: Service health status

**Default URL:** http://localhost:15000

### Services & Ports (Aspire Auto-Assigns)

| Service | Default Port | Description |
|---------|--------------|-------------|
| Aspire Dashboard | 15000 | Orchestration dashboard |
| Auth Service | 5100 | Authentication API |
| User Service | 5101 | User profile API |
| Product Service | 5102 | Product catalog API |
| Recipe Service | 5103 | Recipe API |
| Blazor Web | 5000 | Web application |
| SQL Server | 1433 | Database |
| Redis | 6379 | Cache |
| RabbitMQ | 5672 | Message bus |
| RabbitMQ Management | 15672 | Message bus UI |

---

## Running Individual Services

### Run a Single Service

```bash
# Auth Service
cd src/Services/ExpressRecipe.AuthService
dotnet run

# Product Service
cd src/Services/ExpressRecipe.ProductService
dotnet run
```

### Run with Watch (Auto-reload)

```bash
dotnet watch run
```

### Run Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test src/Tests/Unit/ExpressRecipe.Tests.Unit.csproj

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Configuration

### User Secrets (Development)

Store sensitive configuration locally:

```bash
cd src/Services/ExpressRecipe.AuthService
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:AuthDb" "Server=localhost;..."
dotnet user-secrets set "JwtSettings:SecretKey" "your-secret-key"
```

### appsettings.json Hierarchy

```
appsettings.json              # Base settings (checked into git)
appsettings.Development.json  # Development overrides (gitignored)
appsettings.Local.json        # Local overrides (gitignored)
User Secrets                  # Sensitive data (not in files)
Environment Variables         # Runtime overrides
```

---

## Common Tasks

### Add a New NuGet Package

```bash
cd src/Services/ExpressRecipe.ProductService
dotnet add package Newtonsoft.Json
```

### Create a New Migration (Example Pattern)

```bash
# Create SQL file in Migrations folder
echo "CREATE TABLE Product (...)" > Migrations/001_CreateProductTable.sql
```

### Format Code

```bash
# Format entire solution
dotnet format ExpressRecipe.sln

# Format specific project
dotnet format src/Services/ExpressRecipe.AuthService/ExpressRecipe.AuthService.csproj
```

### Build for Release

```bash
dotnet build -c Release
dotnet publish -c Release -o ./publish
```

---

## Troubleshooting

### "Aspire is not installed"

```bash
dotnet workload install aspire
```

### "Docker is not running"

1. Start Docker Desktop
2. Wait for Docker to fully start
3. Restart Aspire AppHost

### "Port already in use"

```bash
# Kill process on port (Windows)
netstat -ano | findstr :5000
taskkill /PID <process_id> /F

# Kill process on port (Linux/Mac)
lsof -ti:5000 | xargs kill -9
```

### "Connection to SQL Server failed"

1. Verify Docker container is running: `docker ps`
2. Check connection string in appsettings
3. Verify SA password meets complexity requirements
4. Try connecting with SSMS or Azure Data Studio

### "Cannot find type or namespace"

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

---

## Development Workflow

### 1. Start Development Session

```bash
# Start Aspire (all services)
cd src/ExpressRecipe.AppHost
dotnet run

# Or open in Visual Studio and press F5
```

### 2. Make Changes

- Edit code in your preferred editor
- Services with `dotnet watch` will auto-reload
- Refresh browser to see UI changes

### 3. Run Tests

```bash
# Quick test
dotnet test --filter FullyQualifiedName~ProductServiceTests

# Full test suite
dotnet test
```

### 4. Commit Changes

```bash
git add .
git commit -m "feat: add product search endpoint"
git push
```

---

## IDE Configuration

### Visual Studio 2024

**Recommended Extensions:**
- .NET Aspire
- GitHub Copilot (optional)
- SonarLint (code quality)

**Settings:**
- Enable "Format on Save"
- Enable "Remove unused usings on save"
- Set tab size to 4 spaces

### Visual Studio Code

**Required Extensions:**
- C# Dev Kit
- .NET Aspire

**Recommended Extensions:**
- GitLens
- REST Client
- SQL Server (mssql)
- Docker

**settings.json:**
```json
{
  "editor.formatOnSave": true,
  "csharp.format.enable": true,
  "omnisharp.enableRoslynAnalyzers": true
}
```

---

## Next Steps

1. Read the planning docs in `/docs`
2. Review the CLAUDE.md file for architecture patterns
3. Check the current phase in `docs/06-IMPLEMENTATION-ROADMAP.md`
4. Look at open issues or the todo list
5. Pick a task and start coding!

---

## Getting Help

- **Documentation:** `/docs` folder
- **Issues:** GitHub Issues
- **Architecture Questions:** See CLAUDE.md
- **API Documentation:** Swagger UI at each service URL (e.g., http://localhost:5100/swagger)

---

## Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [ADO.NET Documentation](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
