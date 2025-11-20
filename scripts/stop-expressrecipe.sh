#!/bin/bash

# ExpressRecipe Stop Script

set -e

echo "================================================"
echo "Stopping ExpressRecipe Platform"
echo "================================================"
echo ""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Check for docker compose
if command -v docker-compose > /dev/null 2>&1; then
    DOCKER_COMPOSE="docker-compose"
elif docker compose version > /dev/null 2>&1; then
    DOCKER_COMPOSE="docker compose"
else
    echo -e "${RED}ERROR: docker compose not found!${NC}"
    exit 1
fi

echo "Choose shutdown option:"
echo "  1. Stop services (keep data)"
echo "  2. Stop and remove containers (keep data)"
echo "  3. Complete cleanup (removes ALL data including AI models!)"
echo ""
read -p "Enter choice (1-3): " -n 1 -r
echo ""
echo ""

case $REPLY in
    1)
        echo -e "${YELLOW}Stopping services...${NC}"
        $DOCKER_COMPOSE stop
        echo -e "${GREEN}✓ Services stopped${NC}"
        echo "Data volumes preserved. Run start-expressrecipe.sh to restart."
        ;;
    2)
        echo -e "${YELLOW}Stopping and removing containers...${NC}"
        $DOCKER_COMPOSE down
        echo -e "${GREEN}✓ Containers removed${NC}"
        echo "Data volumes preserved. Run start-expressrecipe.sh to restart."
        ;;
    3)
        echo -e "${RED}⚠️  WARNING: This will delete ALL data including:${NC}"
        echo "  • Database contents"
        echo "  • Downloaded AI models"
        echo "  • Redis cache"
        echo "  • RabbitMQ queues"
        echo ""
        read -p "Are you sure? (yes/no): " -r
        echo ""
        if [ "$REPLY" = "yes" ]; then
            echo -e "${YELLOW}Removing all containers and volumes...${NC}"
            $DOCKER_COMPOSE down -v
            echo -e "${GREEN}✓ Complete cleanup done${NC}"
            echo "All data removed. Next startup will be a fresh installation."
        else
            echo "Cancelled."
        fi
        ;;
    *)
        echo "Invalid choice. Exiting."
        exit 1
        ;;
esac

echo ""
