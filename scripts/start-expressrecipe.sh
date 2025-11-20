#!/bin/bash

# ExpressRecipe Startup Script
# This script helps you start the complete ExpressRecipe platform

set -e  # Exit on error

echo "================================================"
echo "ExpressRecipe Platform Startup"
echo "================================================"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}ERROR: Docker is not running!${NC}"
    echo "Please start Docker Desktop and try again."
    exit 1
fi

echo -e "${GREEN}✓ Docker is running${NC}"
echo ""

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check for docker compose
if command_exists docker-compose; then
    DOCKER_COMPOSE="docker-compose"
elif docker compose version > /dev/null 2>&1; then
    DOCKER_COMPOSE="docker compose"
else
    echo -e "${RED}ERROR: docker compose not found!${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Using: $DOCKER_COMPOSE${NC}"
echo ""

# Check if this is first run
FIRST_RUN=false
if ! docker volume ls | grep -q expressrecipe-sqlserver-data; then
    FIRST_RUN=true
    echo -e "${YELLOW}First run detected - this may take 10-15 minutes${NC}"
    echo ""
fi

# Start infrastructure services first
echo -e "${BLUE}Starting infrastructure services...${NC}"
$DOCKER_COMPOSE up -d sqlserver redis rabbitmq ollama

echo "Waiting for infrastructure to be healthy..."
sleep 10

# Check SQL Server
echo -n "Checking SQL Server... "
for i in {1..30}; do
    if docker exec expressrecipe-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "ExpressRecipe123!" -Q "SELECT 1" > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "${RED}✗ Failed${NC}"
        echo "SQL Server failed to start. Check logs with: docker logs expressrecipe-sqlserver"
        exit 1
    fi
    sleep 2
done

# Check Redis
echo -n "Checking Redis... "
for i in {1..30}; do
    if docker exec expressrecipe-redis redis-cli ping > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "${RED}✗ Failed${NC}"
        exit 1
    fi
    sleep 1
done

# Check RabbitMQ
echo -n "Checking RabbitMQ... "
for i in {1..30}; do
    if docker exec expressrecipe-rabbitmq rabbitmq-diagnostics ping > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "${RED}✗ Failed${NC}"
        exit 1
    fi
    sleep 2
done

# Check Ollama
echo -n "Checking Ollama... "
for i in {1..30}; do
    if curl -s http://localhost:11434/api/tags > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC}"
        break
    fi
    if [ $i -eq 30 ]; then
        echo -e "${RED}✗ Failed${NC}"
        exit 1
    fi
    sleep 2
done

echo ""
echo -e "${GREEN}✓ Infrastructure is ready${NC}"
echo ""

# Pull Ollama models if first run
if [ "$FIRST_RUN" = true ]; then
    echo -e "${BLUE}Pulling AI models (this may take 10-30 minutes)...${NC}"
    echo "You can skip this and pull models later with: docker exec expressrecipe-ollama ollama pull llama2"
    echo ""
    read -p "Pull AI models now? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "Pulling llama2..."
        docker exec expressrecipe-ollama ollama pull llama2
        echo "Pulling mistral..."
        docker exec expressrecipe-ollama ollama pull mistral
        echo -e "${GREEN}✓ AI models ready${NC}"
    else
        echo -e "${YELLOW}Skipping AI models - remember to pull them later${NC}"
    fi
    echo ""
fi

# Start application services
echo -e "${BLUE}Starting all microservices...${NC}"
$DOCKER_COMPOSE up -d \
    authservice userservice productservice recipeservice \
    inventoryservice shoppingservice mealplanningservice \
    scannerservice recallservice searchservice syncservice \
    aiservice notificationservice analyticsservice \
    communityservice priceservice blazorweb

echo "Waiting for services to be ready..."
sleep 20

# Check service health
echo ""
echo -e "${BLUE}Checking service health...${NC}"

check_service() {
    local name=$1
    local port=$2
    echo -n "  $name... "

    for i in {1..20}; do
        if curl -s http://localhost:$port/health > /dev/null 2>&1; then
            echo -e "${GREEN}✓${NC}"
            return 0
        fi
        sleep 2
    done

    echo -e "${YELLOW}⚠ Not responding${NC}"
    return 1
}

echo "Core Services:"
check_service "Auth Service" 5000
check_service "User Service" 5001
check_service "Product Service" 5002
check_service "Recipe Service" 5003
check_service "Inventory Service" 5004
check_service "Shopping Service" 5005
check_service "Meal Planning Service" 5006

echo ""
echo "Supporting Services:"
check_service "Scanner Service" 5007
check_service "Recall Service" 5008
check_service "Search Service" 5009
check_service "Sync Service" 5010

echo ""
echo "Advanced Services:"
check_service "AI Service" 5100
check_service "Notification Service" 5101
check_service "Analytics Service" 5102
check_service "Community Service" 5103
check_service "Price Service" 5104

echo ""
echo "Frontend:"
check_service "Blazor Web" 5080

echo ""
echo -e "${GREEN}================================================${NC}"
echo -e "${GREEN}ExpressRecipe Platform is running!${NC}"
echo -e "${GREEN}================================================${NC}"
echo ""
echo "Service URLs:"
echo ""
echo "Web Application:"
echo "  • Blazor Web UI:       http://localhost:5080"
echo ""
echo "Infrastructure:"
echo "  • Ollama API:          http://localhost:11434"
echo "  • RabbitMQ Management: http://localhost:15672 (expressrecipe / expressrecipe_dev_password)"
echo "  • SQL Server:          localhost:1433 (sa / ExpressRecipe123!)"
echo "  • Redis:               localhost:6379"
echo ""
echo "Core Microservices:"
echo "  • Auth Service:        http://localhost:5000"
echo "  • User Service:        http://localhost:5001"
echo "  • Product Service:     http://localhost:5002"
echo "  • Recipe Service:      http://localhost:5003"
echo "  • Inventory Service:   http://localhost:5004"
echo "  • Shopping Service:    http://localhost:5005"
echo "  • Meal Planning:       http://localhost:5006"
echo ""
echo "Supporting Services:"
echo "  • Scanner Service:     http://localhost:5007"
echo "  • Recall Service:      http://localhost:5008"
echo "  • Search Service:      http://localhost:5009"
echo "  • Sync Service:        http://localhost:5010"
echo "  • AI Service:          http://localhost:5100"
echo "  • Notifications:       http://localhost:5101"
echo "  • Analytics:           http://localhost:5102"
echo "  • Community:           http://localhost:5103"
echo "  • Price Tracking:      http://localhost:5104"
echo ""
echo "API Documentation (Swagger) - Add /swagger to any service URL"
echo "Examples:"
echo "  • http://localhost:5000/swagger (Auth)"
echo "  • http://localhost:5002/swagger (Products)"
echo "  • http://localhost:5100/swagger (AI)"
echo "  • http://localhost:5080 (Blazor Web UI)"
echo ""
echo "Useful commands:"
echo "  • View logs:     $DOCKER_COMPOSE logs -f"
echo "  • Stop all:      $DOCKER_COMPOSE down"
echo "  • Service status: $DOCKER_COMPOSE ps"
echo ""
echo "See DOCKER-SETUP.md for complete documentation"
echo ""
