#!/bin/bash
# setup-env.sh - Quick environment setup script for ExpressRecipe

echo "?? ExpressRecipe Environment Setup"
echo "===================================="
echo ""

# Check if .env already exists
if [ -f .env ]; then
    echo "??  .env file already exists!"
    read -p "Do you want to overwrite it? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "? Setup cancelled. Existing .env file kept."
        exit 1
    fi
fi

# Copy template
if [ ! -f .env.template ]; then
    echo "? Error: .env.template not found!"
    echo "Make sure you're running this from the project root directory."
    exit 1
fi

echo "?? Copying .env.template to .env..."
cp .env.template .env

# Generate a random JWT secret
echo "?? Generating secure JWT secret..."
JWT_SECRET=$(openssl rand -base64 64 | tr -d '\n')

# Update .env file with generated secret
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    sed -i '' "s|REPLACE-WITH-STRONG-SECRET-MIN-64-CHARS-USE-OPENSSL-RAND-BASE64-64|$JWT_SECRET|g" .env
else
    # Linux
    sed -i "s|REPLACE-WITH-STRONG-SECRET-MIN-64-CHARS-USE-OPENSSL-RAND-BASE64-64|$JWT_SECRET|g" .env
fi

echo "? .env file created successfully!"
echo ""
echo "?? Generated JWT secret (stored in .env file)"
echo ""
echo "?? Next steps:"
echo "   1. Review and edit .env file if needed"
echo "   2. Add any external API keys (USDA, OpenAI, etc.)"
echo "   3. Run the application: cd src/ExpressRecipe.AppHost.New && dotnet run"
echo ""
echo "??  Remember: NEVER commit the .env file to git!"
echo ""
