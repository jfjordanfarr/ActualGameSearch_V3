# Infrastructure Setup for ActualGameSearch

This directory contains the minimal infrastructure setup needed to make ActualGameSearch development repeatable across environments.

## Ollama 8k Context Fix

**Problem**: The default `nomic-embed-text:v1.5` model has a hardcoded 2k context window, but we need 8k for processing long game reviews and descriptions.

**Solution**: We create a custom model with an 8k context window using a simple Modelfile override.

### Files
- `Modelfile.nomic-embed-8k` - Simple Modelfile that sets `PARAMETER num_ctx 8192`
- `setup-ollama-models.sh` - One-command setup script

### Usage

In any fresh environment (new Codespace, local dev, deployment):

```bash
# Run from project root
./infrastructure/setup-ollama-models.sh
```

This will:
1. Check if Ollama is running
2. Pull the base `nomic-embed-text:v1.5` model if needed
3. Create `nomic-embed-8k:latest` with 8k context
4. Test that the model works
5. Report the embedding dimension (should be 768)

### Why This Approach?

**Simple**: No Docker complexity, works with existing Aspire + Ollama setup
**Repeatable**: One script recreates the exact environment
**Fast**: Reuses base model layers, only adds the parameter override
**Testable**: Includes verification that the model actually works

### Current Architecture

```
GitHub Codespaces (or local dev)
├── Ollama (native host process)
│   ├── nomic-embed-text:v1.5 (base model)
│   └── nomic-embed-8k:latest (custom 8k context)
├── .NET Aspire AppHost (orchestrates)
│   ├── Cosmos DB Emulator (container)
│   ├── ActualGameSearch.Api
│   └── ActualGameSearch.Worker
└── VS Code (devcontainer)
```

**No Docker orchestration needed** - Aspire handles Cosmos, Ollama runs natively, everything just works.