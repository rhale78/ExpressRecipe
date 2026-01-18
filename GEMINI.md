# Gemini Project Context: ExpressRecipe

This document provides context for the ExpressRecipe project, a comprehensive dietary management platform. It's intended to be used by the Gemini CLI to understand the project's structure, technologies, and development workflows.

## Project Overview

ExpressRecipe is a sophisticated, microservices-based platform designed for individuals with dietary restrictions. It offers features like smart product scanning, recipe management, meal planning, and AI-powered dietary advice. The system is built with a "local-first" principle, ensuring offline capability and data privacy, with optional cloud synchronization.

The architecture consists of numerous specialized microservices (e.g., Auth, Products, Recipes, AI) that communicate asynchronously via a message bus and synchronously via a central API Gateway.

## Technology Stack

- **Backend Framework:** .NET 10 & C#
- **Orchestration:** .NET Aspire for local development service discovery and orchestration.
- **Data Access:** High-performance ADO.NET with custom `SqlHelper` classes (explicitly avoiding Entity Framework).
- **Databases:**
    - **Cloud:** SQL Server
    - **Local:** SQLite
- **Caching:** Redis
- **Messaging:** RabbitMQ (for event-driven communication)
- **AI:** Ollama for local, on-device AI model serving (Llama, Mistral).
- **Frontends:**
    - **Web:** Blazor
    - **Desktop:** WinUI 3
    - **Mobile:** .NET MAUI
- **Containerization:** Docker and Docker Compose are heavily used for local development environments.

## How to Build and Run

The project can be run in two primary ways: via Docker Compose for a full-stack environment or via .NET Aspire for backend development.

### 1. Running with Docker Compose (Recommended Quick Start)

This method starts all infrastructure (databases, messaging) and the pre-built application microservices.

**Prerequisites:** Docker Desktop

**Commands:**
```bash
# Start all services in the background
docker compose up -d

# To initialize the local AI models (first time only)
# This script might need to be made executable: chmod +x scripts/init-ollama-models.sh
./scripts/init-ollama-models.sh

# Stop all services
docker compose down

# Stop services and remove all data (database, cache, etc.)
docker compose down -v
```

**Key Endpoints:**
- **AI Service:** `http://localhost:5100`
- **Notification Service:** `http://localhost:5101`
- **Ollama AI API:** `http://localhost:11434`
- **RabbitMQ Management:** `http://localhost:15672`

### 2. Running with .NET Aspire (for Development)

This method is ideal for actively developing the backend services. It uses the .NET Aspire AppHost to launch and manage services.

**Prerequisites:** .NET 10 SDK, .NET Aspire workload (`dotnet workload install aspire`), Docker Desktop (for infrastructure like SQL/Redis).

**Commands:**
```bash
# Restore all .NET dependencies for the solution
dotnet restore ExpressRecipe.sln

# Navigate to the AppHost project
cd src/ExpressRecipe.AppHost

# Run the application
dotnet run
```

**Key Endpoints:**
- **Aspire Dashboard:** `http://localhost:15000` (Provides logs, metrics, and links to all running services)

### Building the Project

To build the entire solution from the command line:
```bash
# From the root directory
dotnet build ExpressRecipe.sln
```
A `clean-and-build.cmd` script also exists for convenience.

## Development Conventions

- **Project Structure:** The solution is organized into `src`, `docs`, `scripts`, etc. The `src` directory contains the `AppHost`, `ServiceDefaults`, individual microservices under `Services`, and frontends under `Frontends`.
- **Data Access:** All data access is performed using raw ADO.NET and custom helper classes for performance. Do not introduce ORMs like Entity Framework.
- **Configuration:** Configuration follows the standard .NET model (`appsettings.json`, environment variables, user secrets). Aspire manages service-to-service configuration during local development.
- **Testing:** Tests are located in the `src/Tests` directory and can be run with the standard `dotnet test` command.
- **Documentation:** The `/docs` directory contains extensive architecture and planning documents. Refer to these for in-depth understanding of the system's design.
