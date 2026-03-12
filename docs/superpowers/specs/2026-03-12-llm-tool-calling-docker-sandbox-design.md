# LLM Tool Calling with Docker Sandbox — Design Spec

## Goal

Give the LLM access to tools: a virtual filesystem and sandboxed bash execution inside a Docker container, with tool calls and results displayed transparently in the chat UI.

## Architecture

Three new components:

### DockerSandbox

Manages the Docker container lifecycle. On app start, creates/starts a container from `ubuntu:24.04` with a named Docker volume mounted at `/workspace`. On app exit, stops the container (volume persists across sessions). Provides `ExecAsync(command, timeout)` that runs `docker exec` and returns stdout/stderr/exit code.

Docker interaction is done by shelling out to the `docker` CLI via `Process.Start` — no Docker SDK NuGet package.

**Container config:**
- Image: `ubuntu:24.04`
- Volume: `trapped-mind-sharp-data` mounted at `/workspace`
- Memory: 512MB limit
- CPU: 1 core
- Network: enabled (for apt/pip install)
- Command timeout: 30 seconds default
- Working directory: `/workspace`
- Container name: `trapped-mind-sharp-sandbox`

### SandboxTools

Defines the AI functions the LLM can call, registered with Microsoft.Extensions.AI's function calling:

- `ReadFile(path)` — reads a file inside the container via `docker exec cat <path>`
- `WriteFile(path, content)` — writes content to a file in the container via `docker exec` with stdin
- `ListDirectory(path)` — lists directory contents via `docker exec ls -la <path>`
- `CreateDirectory(path)` — creates directories via `docker exec mkdir -p <path>`
- `RunBashCommand(command)` — runs a bash command via `docker exec bash -c <command>` with timeout

All paths are relative to `/workspace` inside the container. Path validation ensures paths cannot escape `/workspace` (no `..` traversal above root).

### ToolRenderer (additions to ConsoleRenderer)

Renders tool call and result panels in the chat UI:

- **Tool call panel** — yellow border, shows tool name and arguments
- **Tool result panel** — dim border, shows stdout/stderr/file contents

## Tool Call Flow (Streaming)

1. User sends message, added to chat history
2. Stream response from Ollama via `GetStreamingResponseAsync` with tools in `ChatOptions`
3. As streaming updates arrive, they may contain:
   - **Text content** — streamed into the green AI panel via `Live` display as before
   - **Function call content** — accumulated as the call streams in
4. When a function call is fully received:
   - Display yellow tool call panel with name + arguments
   - Execute via `DockerSandbox`
   - Display result panel
   - Add assistant message (with function call) and function result to history
5. Send history back to Ollama for the next round — stream again
6. Repeat until a response contains only text with no tool calls

Within a single streaming response, text and tool calls interleave naturally. The `Live` display updates in real-time for text, and tool panels appear inline as tool calls complete during the stream.

**Error handling:** If a tool throws (Docker timeout, container not running, path validation fails), the error message is returned as the function result so the LLM can see what went wrong and adapt.

## File Structure

```
src/TrappedMindSharp/
    Program.cs                 # Modified — container lifecycle, tool registration, revised chat loop
    ChatService.cs             # Modified — handle tool call/result messages in history
    ConsoleRenderer.cs         # Modified — add tool panel rendering methods
    CommandHandler.cs          # Unchanged
    DockerSandbox.cs           # New — container lifecycle + exec
    SandboxTools.cs            # New — AI function definitions
    TrappedMindSharp.csproj    # Modified — no new packages needed
tests/TrappedMindSharp.Tests/
    SandboxToolsTests.cs       # New — path validation, argument handling
    ToolRendererTests.cs       # New — tool panel rendering edge cases
tests/TrappedMindSharp.Integration.Tests/
    DockerSandboxTests.cs      # New — container lifecycle, exec, timeout, volume
    TrappedMindSharp.Integration.Tests.csproj  # New
TrappedMindSharp.slnx          # Modified — add integration test project
```

## Testing Strategy

**Unit tests (`TrappedMindSharp.Tests`):**
- `SandboxToolsTests` — path validation (can't escape `/workspace`), argument handling, error message formatting. Mock `DockerSandbox`.
- `ToolRendererTests` — tool call/result panels produce valid renderables. Test edge cases: long output, empty results, special characters.

**Integration tests (`TrappedMindSharp.Integration.Tests`):**
- `DockerSandboxTests` — container start/stop, exec commands, timeout enforcement, volume persistence across restarts. Requires Docker.
- Separate project so CI can choose to run or skip.

## Constraints

- Docker must be installed and running on the host
- Ollama model must support function calling (most recent models do)
- No new NuGet dependencies — Docker via CLI, tools via Microsoft.Extensions.AI (already referenced)
