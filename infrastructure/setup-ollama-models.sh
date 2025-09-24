#!/bin/bash

# Simple setup script for Ollama custom models in ActualGameSearch
# This ensures the 8k context model is available for development

set -euo pipefail

echo "🚀 Setting up Ollama 8k context model..."

# Check if Ollama is running
if ! curl -s http://localhost:11434/api/tags >/dev/null 2>&1; then
    echo "❌ Ollama is not running on localhost:11434"
    echo "Please start Ollama first (it should already be running in Codespaces)"
    exit 1
fi

# Check if our custom model already exists
if curl -s http://localhost:11434/api/tags | jq -r '.models[].name' | grep -q "^nomic-embed-8k:"; then
    echo "✅ Custom model nomic-embed-8k already exists"
else
    echo "🔧 Creating custom 8k context model..."
    
    # Pull base model if needed
    echo "📥 Ensuring base model is available..."
    ollama pull nomic-embed-text:v1.5
    
    # Create custom model from our Modelfile
    MODELFILE_PATH="$(dirname "$0")/Modelfile.nomic-embed-8k"
    if [ ! -f "$MODELFILE_PATH" ]; then
        echo "❌ Modelfile not found at $MODELFILE_PATH"
        exit 1
    fi
    
    echo "��️  Creating nomic-embed-8k from Modelfile..."
    ollama create nomic-embed-8k -f "$MODELFILE_PATH"
    
    if [ $? -eq 0 ]; then
        echo "✅ Successfully created nomic-embed-8k:latest"
    else
        echo "❌ Failed to create custom model"
        exit 1
    fi
fi

# Quick test
echo "🧪 Testing model..."
RESPONSE=$(curl -s -X POST http://localhost:11434/api/embeddings \
    -H "Content-Type: application/json" \
    -d '{"model": "nomic-embed-8k:latest", "prompt": "test"}')

if echo "$RESPONSE" | jq -e '.embedding | length > 0' >/dev/null 2>&1; then
    DIM=$(echo "$RESPONSE" | jq '.embedding | length')
    echo "✅ Model working! Embedding dimension: $DIM"
else
    echo "❌ Model test failed: $RESPONSE"
    exit 1
fi

echo "🎉 Setup complete! Use 'nomic-embed-8k:latest' in your app."
