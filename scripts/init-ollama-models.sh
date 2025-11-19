#!/bin/bash

echo "Initializing Ollama models for ExpressRecipe..."

# Wait for Ollama to be ready
echo "Waiting for Ollama service..."
until curl -s http://localhost:11434/api/tags > /dev/null 2>&1; do
    sleep 2
done

echo "Ollama is ready!"

# Pull recommended models
echo "Pulling llama2 model (recommended for general AI tasks)..."
docker exec expressrecipe-ollama ollama pull llama2

echo "Pulling mistral model (faster, good for quick responses)..."
docker exec expressrecipe-ollama ollama pull mistral

echo "Pulling codellama model (optional, for code-related tasks)..."
docker exec expressrecipe-ollama ollama pull codellama

echo "Models installed successfully!"
echo "Available models:"
docker exec expressrecipe-ollama ollama list

echo ""
echo "ExpressRecipe AI is ready to use!"
echo "Default model: llama2"
echo "Access Ollama API at: http://localhost:11434"
