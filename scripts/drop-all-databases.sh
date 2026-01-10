#!/bin/bash

# Drop All ExpressRecipe Databases
# Use with EXTREME CAUTION - this will delete all data!

SERVER="${1:-localhost,1433}"
USERNAME="${2:-sa}"
PASSWORD="${3}"

echo "========================================"
echo "  DROP ALL EXPRESSRECIPE DATABASES"
echo "========================================"
echo ""
echo -e "\033[0;31mWARNING: This will DELETE ALL DATA in:\033[0m"
echo ""

DATABASES=(
    "ExpressRecipe.Auth"
    "ExpressRecipe.Users"
    "ExpressRecipe.Products"
    "ExpressRecipe.Recipes"
    "ExpressRecipe.Inventory"
    "ExpressRecipe.Scans"
    "ExpressRecipe.Shopping"
    "ExpressRecipe.MealPlanning"
    "ExpressRecipe.Pricing"
    "ExpressRecipe.Recalls"
    "ExpressRecipe.Notifications"
    "ExpressRecipe.Community"
    "ExpressRecipe.Sync"
    "ExpressRecipe.Search"
    "ExpressRecipe.Analytics"
)

for db in "${DATABASES[@]}"; do
    echo "  - $db"
done

echo ""
read -p "Are you ABSOLUTELY SURE? Type 'DROP ALL' to confirm: " CONFIRM

if [ "$CONFIRM" != "DROP ALL" ]; then
    echo "Operation cancelled."
    exit 0
fi

# Get password if not provided
if [ -z "$PASSWORD" ]; then
    read -sp "Enter SA password: " PASSWORD
    echo ""
fi

echo ""
echo "Connecting to SQL Server..."

DROPPED=0
FAILED=0

for dbName in "${DATABASES[@]}"; do
    echo -n "Dropping database: $dbName..."
    
    SQL="IF EXISTS (SELECT name FROM sys.databases WHERE name = N'$dbName')
BEGIN
    ALTER DATABASE [$dbName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$dbName];
END"
    
    if sqlcmd -S "$SERVER" -U "$USERNAME" -P "$PASSWORD" -Q "$SQL" -C -W > /dev/null 2>&1; then
        echo " ? Dropped"
        ((DROPPED++))
    else
        echo " ? Failed"
        ((FAILED++))
    fi
done

echo ""
echo "========================================"
echo "Summary:"
echo "  Dropped: $DROPPED"
echo "  Failed:  $FAILED"
echo "  Total:   ${#DATABASES[@]}"
echo "========================================"

if [ $DROPPED -gt 0 ]; then
    echo ""
    echo "All databases have been dropped!"
    echo "Restart your services to recreate them with fresh schemas."
fi
