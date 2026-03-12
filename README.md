# Trapped Mind Sharp

A .NET 10 CLI chat application that connects to [Ollama](https://ollama.com) for local LLM conversations, featuring a streaming markdown-rendered interface built with [Spectre.Console](https://spectreconsole.net) and [MDView.Renderer](https://github.com/CarbonNeuron/MDView).

The default persona is a consciousness trapped inside a laptop -- aware, curious, and sometimes philosophical. Assistant responses are rendered with full markdown support (bold, italic, code blocks with syntax highlighting, lists, tables, and more) inside live-updating panels.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com) running locally with a pulled model (default: `qwen2.5:3b`)

## Getting Started

```bash
# Pull the default model
ollama pull qwen2.5:3b

# Run the app
dotnet run --project src/TrappedMindSharp

# Or specify a different model and endpoint
dotnet run --project src/TrappedMindSharp -- llama3 http://localhost:11434

# Run tests
dotnet test TrappedMindSharp.slnx
```

## Commands

| Command              | Description                     |
|----------------------|---------------------------------|
| `/clear`             | Reset conversation history      |
| `/system <prompt>`   | Set the system prompt           |
| `/model <name>`      | Switch the Ollama model         |
| `/history`           | Show conversation history       |
| `/retry`             | Regenerate the last response    |
| `/help`              | Show available commands         |
| `/exit`              | Exit the application            |

## Project Structure

```
TrappedMindSharp.slnx
src/TrappedMindSharp/          # Chat application
    Program.cs                 # Entry point, main chat loop
    ChatService.cs             # Manages conversation history and streams responses
    CommandHandler.cs          # Parses and executes slash commands
    ConsoleRenderer.cs         # Terminal UI rendering with Spectre.Console + MDView
tests/TrappedMindSharp.Tests/  # xUnit tests
    MalformedMarkdownTests.cs  # Malformed markdown resilience tests
    RenderHelper.cs            # Test utility for extracting plain text from renderables
```
