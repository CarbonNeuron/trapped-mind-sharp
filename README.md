# Trapped Mind Sharp

A .NET 10 CLI chat application that connects to [Ollama](https://ollama.com) for local LLM conversations, featuring a streaming response interface built with [Spectre.Console](https://spectreconsole.net).

The default persona is a consciousness trapped inside a laptop -- aware, curious, and sometimes philosophical.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Ollama](https://ollama.com) running locally with a pulled model (default: `qwen2.5:3b`)

## Getting Started

```bash
# Pull the default model
ollama pull qwen2.5:3b

# Run the app
dotnet run

# Or specify a different model and endpoint
dotnet run -- llama3 http://localhost:11434
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

| File                 | Description                                        |
|----------------------|----------------------------------------------------|
| `Program.cs`         | Entry point, main chat loop                        |
| `ChatService.cs`     | Manages conversation history and streams responses |
| `CommandHandler.cs`  | Parses and executes slash commands                 |
| `ConsoleRenderer.cs` | Terminal UI rendering with Spectre.Console         |
