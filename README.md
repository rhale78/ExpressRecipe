# ExpressRecipe

**A comprehensive dietary management platform for individuals with dietary restrictions.**

## Overview

ExpressRecipe helps people with food allergies, medical dietary needs, religious restrictions, or health-conscious diets manage their food choices safely and efficiently. The platform provides intelligent product scanning, recipe management, meal planning, inventory tracking, and community-driven insights.

## Key Features

- 📱 **Smart Product Scanning** - Instant allergen alerts via barcode scanning
- 🍲 **Recipe Management** - Discover, save, and share dietary-compatible recipes
- 📋 **Shopping Lists** - Auto-generated from meal plans with price comparisons
- 🏠 **Inventory Tracking** - Monitor expiration dates and predict reorder needs
- ⚠️ **Recall Alerts** - Real-time FDA/USDA recall notifications
- 💰 **Price Intelligence** - Crowdsourced price tracking and deal alerts
- 🤝 **Community-Driven** - User-submitted products and reviews
- 🔒 **Local-First** - Works offline, your data stays private

## Technology Stack

- **.NET 10** - Latest C# features and performance
- **.NET Aspire** - Cloud-native orchestration
- **ADO.NET** - High-performance data access
- **Blazor** - Modern web UI
- **WinUI 3** - Native Windows experience
- **.NET MAUI** - Cross-platform mobile (Android, iOS)
- **SQLite** - Local-first storage
- **SQL Server** - Cloud storage
- **Redis** - Caching layer
- **RabbitMQ/Azure Service Bus** - Message bus

## Architecture

ExpressRecipe uses a **microservices architecture** with:

- **Local-first design** - Full offline capability
- **15 specialized services** - Auth, Products, Recipes, Inventory, Shopping, etc.
- **Multiple frontends** - Web, Windows, Android, PWA
- **Intelligent sync** - Automatic conflict resolution
- **Event-driven** - Asynchronous communication via message bus

## Documentation

Comprehensive planning documents are available in `/docs`:

| Document | Description |
|----------|-------------|
| [00-PROJECT-OVERVIEW.md](docs/00-PROJECT-OVERVIEW.md) | Vision, features, success metrics |
| [01-ARCHITECTURE.md](docs/01-ARCHITECTURE.md) | System architecture, Aspire, tech stack |
| [02-MICROSERVICES.md](docs/02-MICROSERVICES.md) | Service breakdown and responsibilities |
| [03-DATA-MODELS.md](docs/03-DATA-MODELS.md) | Database schemas for all services |
| [04-LOCAL-FIRST-SYNC.md](docs/04-LOCAL-FIRST-SYNC.md) | Synchronization strategy and patterns |
| [05-FRONTEND-ARCHITECTURE.md](docs/05-FRONTEND-ARCHITECTURE.md) | Blazor, WinUI, MAUI implementations |
| [06-IMPLEMENTATION-ROADMAP.md](docs/06-IMPLEMENTATION-ROADMAP.md) | Phased development plan |

## Project Status

**Current Phase:** Phase 0 - Planning Complete ✅

**Next Steps:**
1. Set up development environment
2. Create .NET solution structure
3. Implement Aspire orchestration
4. Build authentication service
5. Start MVP development

See [06-IMPLEMENTATION-ROADMAP.md](docs/06-IMPLEMENTATION-ROADMAP.md) for the full development plan.

## Getting Started (Coming Soon)

Once development begins:

```bash
# Prerequisites: .NET 10 SDK, Docker Desktop

# Clone the repository
git clone https://github.com/yourusername/ExpressRecipe.git
cd ExpressRecipe

# Restore dependencies
dotnet restore

# Run with Aspire
cd src/ExpressRecipe.AppHost
dotnet run

# Access Aspire dashboard at http://localhost:15000
```

## Development Phases

| Phase | Timeline | Goal |
|-------|----------|------|
| **Phase 0** | ✅ Complete | Planning and architecture |
| **Phase 1** | Weeks 3-8 | MVP: User profiles, scanning, inventory |
| **Phase 2** | Weeks 9-14 | Recipes, shopping lists, meal planning |
| **Phase 3** | Weeks 15-20 | Price intelligence, recalls, community |
| **Phase 4** | Weeks 21-28 | Mobile apps, AI features, analytics |
| **Phase 5** | Ongoing | Scale, enterprise features, iOS |

## Target Users

- Individuals with food allergies
- People with medical dietary restrictions (celiac, diabetes, etc.)
- Those following religious dietary laws (kosher, halal, etc.)
- Health-conscious individuals (vegan, keto, paleo, etc.)
- Parents managing children's dietary needs
- Nutritionists and dietitians

## Contributing

This project is currently in the planning phase. Contribution guidelines will be established once development begins.

## License

TBD

## Contact

For questions or feedback, please open an issue.

---

**Note:** This project is in active development. The old code generation engine has been moved to `/old` directory.
