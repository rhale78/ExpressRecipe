# ExpressRecipe Docker Setup Guide

This guide explains how to run the complete ExpressRecipe platform using Docker with all microservices and AI capabilities.

## Architecture Overview

The ExpressRecipe platform consists of:

### Infrastructure Services
- **SQL Server 2022** - Primary database for all microservices
- **Redis** - Caching layer for performance optimization
- **RabbitMQ** - Message broker for inter-service communication
- **Ollama** - Local AI model serving (supports llama2, mistral, codellama)

### Application Microservices
- **AIService** (Port 5100) - AI-powered recipe suggestions, ingredient substitutions, meal planning, allergen detection
- **NotificationService** (Port 5101) - Real-time notifications with SignalR, delivery tracking
- **AnalyticsService** (Port 5102) - Usage analytics, reports, insights
- **CommunityService** (Port 5103) - Recipe ratings, reviews, community features
- **PriceService** (Port 5104) - Price tracking, budget management, shopping optimization

All services use:
- **ADO.NET** with custom SqlHelper for data access (no Entity Framework)
- **SQL Server** as the database
- **JWT authentication** for security
- **RabbitMQ** for event-driven communication
- **Redis** for caching

## Prerequisites

- Docker Desktop (or Docker Engine + Docker Compose)
- At least 8GB RAM available for Docker
- 20GB free disk space (for Ollama models)

## Quick Start

### 1. Clone the Repository
```bash
git clone <repository-url>
cd ExpressRecipe
```

### 2. Start All Services
```bash
docker compose up -d
```

This will start all infrastructure and microservices. First-time startup may take 5-10 minutes as Docker pulls all images.

### 3. Initialize Ollama Models
```bash
chmod +x scripts/init-ollama-models.sh
./scripts/init-ollama-models.sh
```

This downloads the AI models (llama2, mistral, codellama). This may take 10-30 minutes depending on your internet connection.

### 4. Verify All Services are Running
```bash
docker compose ps
```

All services should show status "healthy" or "running".

### 5. Initialize Database
The database schema is automatically created on first startup of each service. If you want to manually run the initialization:

```bash
docker exec -i expressrecipe-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "ExpressRecipe123!" < scripts/init-db.sql
```

## Service URLs

Once running, services are available at:

| Service | URL | Description |
|---------|-----|-------------|
| **Ollama API** | http://localhost:11434 | AI model API |
| **SQL Server** | localhost:1433 | Database (credentials below) |
| **Redis** | localhost:6379 | Cache server |
| **RabbitMQ Management** | http://localhost:15672 | Message broker UI |
| **RabbitMQ AMQP** | localhost:5672 | Message broker |
| **AI Service** | http://localhost:5100 | AI features API |
| **Notification Service** | http://localhost:5101 | Notifications API |
| **Analytics Service** | http://localhost:5102 | Analytics API |
| **Community Service** | http://localhost:5103 | Community API |
| **Price Service** | http://localhost:5104 | Price tracking API |

### API Documentation (Swagger)

Each service exposes Swagger documentation in development mode:
- http://localhost:5100/swagger - AI Service
- http://localhost:5101/swagger - Notification Service
- http://localhost:5102/swagger - Analytics Service
- http://localhost:5103/swagger - Community Service
- http://localhost:5104/swagger - Price Service

## Default Credentials

### SQL Server
- **Server**: localhost:1433
- **Username**: sa
- **Password**: ExpressRecipe123!
- **Database**: ExpressRecipe

### RabbitMQ
- **Username**: expressrecipe
- **Password**: expressrecipe_dev_password
- **Management UI**: http://localhost:15672

## Common Commands

### View Logs
```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f aiservice
docker compose logs -f notificationservice
```

### Restart a Service
```bash
docker compose restart aiservice
```

### Rebuild a Service
```bash
docker compose up -d --build aiservice
```

### Stop All Services
```bash
docker compose down
```

### Stop and Remove Volumes (Complete Reset)
```bash
docker compose down -v
```

**⚠️ Warning**: This deletes all data including database, Ollama models, and cache!

## AI Service Features

The AI Service uses Ollama to provide:

### 1. Recipe Suggestions
```bash
POST http://localhost:5100/api/ai/recipes/suggest
Content-Type: application/json

{
  "availableIngredients": ["chicken", "rice", "broccoli"],
  "userAllergens": ["nuts"],
  "userDislikes": ["mushrooms"],
  "suggestionsCount": 5
}
```

### 2. Ingredient Substitutions
```bash
POST http://localhost:5100/api/ai/ingredients/substitute
Content-Type: application/json

{
  "originalIngredient": "butter",
  "userAllergens": ["dairy"],
  "preferHealthier": true
}
```

### 3. Recipe Extraction from Text
```bash
POST http://localhost:5100/api/ai/recipes/extract
Content-Type: application/json

{
  "recipeText": "Grandma's Chocolate Chip Cookies\n\nIngredients:\n- 2 cups flour\n- 1 cup butter..."
}
```

### 4. Meal Plan Generation
```bash
POST http://localhost:5100/api/ai/meal-plans/suggest
Content-Type: application/json

{
  "daysCount": 7,
  "userAllergens": ["gluten"],
  "dietaryPreferences": ["vegetarian"],
  "calorieTarget": 2000
}
```

### 5. Allergen Detection
```bash
POST http://localhost:5100/api/ai/allergens/detect
Content-Type: application/json

{
  "ingredients": ["wheat flour", "eggs", "milk", "peanuts"],
  "userAllergens": ["gluten", "dairy"]
}
```

### 6. AI Chat Assistant
```bash
POST http://localhost:5100/api/ai/chat
Content-Type: application/json

{
  "message": "What can I make with chicken and rice?",
  "context": {
    "userAllergens": ["nuts"],
    "dietaryPreferences": ["low-carb"]
  }
}
```

## Changing AI Models

By default, the AI Service uses **llama2**. You can change this in the docker-compose.yml:

```yaml
aiservice:
  environment:
    - AI__DefaultModel=mistral  # or codellama
```

Then restart the service:
```bash
docker compose restart aiservice
```

## Health Checks

All services include health checks. Check service health:

```bash
# AI Service
curl http://localhost:5100/health

# Notification Service
curl http://localhost:5101/health

# SQL Server
docker exec expressrecipe-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "ExpressRecipe123!" -Q "SELECT 1"

# Redis
docker exec expressrecipe-redis redis-cli ping

# Ollama
curl http://localhost:11434/api/tags
```

## Troubleshooting

### Service Won't Start
1. Check logs: `docker compose logs <service-name>`
2. Verify dependencies are healthy: `docker compose ps`
3. Check port availability: `netstat -an | grep <port>`

### Database Connection Issues
1. Verify SQL Server is running: `docker compose ps sqlserver`
2. Test connection:
   ```bash
   docker exec expressrecipe-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "ExpressRecipe123!" -Q "SELECT @@VERSION"
   ```
3. Check connection string in service environment variables

### Ollama Model Not Found
1. Pull the model manually:
   ```bash
   docker exec expressrecipe-ollama ollama pull llama2
   ```
2. List available models:
   ```bash
   docker exec expressrecipe-ollama ollama list
   ```

### Out of Memory
If Docker runs out of memory:
1. Increase Docker memory limit in Docker Desktop settings
2. Remove unused Ollama models:
   ```bash
   docker exec expressrecipe-ollama ollama rm <model-name>
   ```

### RabbitMQ Connection Issues
1. Check RabbitMQ is healthy: `docker compose ps rabbitmq`
2. Verify credentials in docker-compose.yml match service configuration
3. Check management UI: http://localhost:15672

## Performance Tuning

### SQL Server
The SQL Server container is configured for development. For production:
- Increase memory limits
- Use persistent volumes
- Configure proper backup strategy

### Redis
Current configuration uses append-only file (AOF) persistence. For production:
- Consider RDB snapshots
- Configure maxmemory policy
- Set up Redis Sentinel for high availability

### Ollama
- Smaller models (llama2) use ~4GB RAM
- Larger models (mistral) use ~8GB RAM
- Consider running Ollama on a separate machine for production

## Development Workflow

### Making Changes to a Service

1. **Edit the code**
2. **Rebuild the service**:
   ```bash
   docker compose up -d --build <service-name>
   ```
3. **View logs**:
   ```bash
   docker compose logs -f <service-name>
   ```

### Running Without Docker

You can run services locally outside Docker for development:

1. **Start infrastructure only**:
   ```bash
   docker compose up -d sqlserver redis rabbitmq ollama
   ```

2. **Run a service locally**:
   ```bash
   cd src/Services/ExpressRecipe.AIService
   dotnet run
   ```

3. **Update connection strings** in `appsettings.Development.json` to use `localhost` instead of service names.

## Production Considerations

This docker-compose.yml is configured for **development**. For production:

### Security
- [ ] Change all default passwords
- [ ] Enable HTTPS/TLS for all services
- [ ] Configure proper JWT secret keys
- [ ] Use secrets management (Azure Key Vault, Docker Secrets)
- [ ] Enable SQL Server authentication with least privilege
- [ ] Configure RabbitMQ with proper user permissions

### Scalability
- [ ] Use external SQL Server (Azure SQL, RDS)
- [ ] Use managed Redis (Azure Cache, ElastiCache)
- [ ] Use managed RabbitMQ (CloudAMQP, Azure Service Bus)
- [ ] Deploy to Kubernetes or Azure Container Apps
- [ ] Implement horizontal scaling for microservices
- [ ] Add load balancer (nginx, Azure Application Gateway)

### Monitoring
- [ ] Configure Application Insights or similar
- [ ] Set up log aggregation (ELK, Seq, Azure Monitor)
- [ ] Configure alerts for service health
- [ ] Monitor database performance
- [ ] Track AI model performance and costs

### Backup & Recovery
- [ ] Implement automated database backups
- [ ] Configure disaster recovery plan
- [ ] Test backup restoration regularly
- [ ] Document recovery procedures

## Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Ollama Documentation](https://ollama.ai/docs)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [SQL Server on Linux](https://learn.microsoft.com/en-us/sql/linux/)
- [RabbitMQ Documentation](https://www.rabbitmq.com/documentation.html)

## Support

For issues or questions:
1. Check the troubleshooting section above
2. Review service logs: `docker compose logs -f`
3. Open an issue on GitHub
4. Check the main README.md for project documentation

---

**Last Updated**: 2025-11-19
**Version**: 1.0.0
