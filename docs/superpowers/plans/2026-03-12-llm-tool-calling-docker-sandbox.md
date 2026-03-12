# LLM Tool Calling with Docker Sandbox Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the LLM access to filesystem and bash tools backed by a Docker container, with tool calls rendered transparently in the chat UI.

**Architecture:** `DockerSandbox` manages an `ubuntu:24.04` container with a persistent named volume. `SandboxTools` defines AI functions (read/write files, mkdir, list dir, run bash) that execute via `docker exec`. The chat loop uses `FunctionInvokingChatClient` to handle tool call rounds automatically, while rendering streams through `ConsoleRenderer` which checks each `ChatResponseUpdate.Contents` for text, function calls, and function results.

**Tech Stack:** .NET 10, Microsoft.Extensions.AI (function calling, `FunctionInvokingChatClient`), Docker CLI via `Process.Start`, Spectre.Console, xUnit

---

## Chunk 1: DockerSandbox

### Task 1: Create DockerSandbox with container lifecycle

**Files:**
- Create: `src/TrappedMindSharp/DockerSandbox.cs`

- [ ] **Step 1: Create DockerSandbox.cs with Start/Stop/Dispose**

Create `src/TrappedMindSharp/DockerSandbox.cs`:

```csharp
using System.Diagnostics;
using System.Text;

namespace TrappedMindSharp;

public sealed class DockerSandbox : IAsyncDisposable
{
    private const string ContainerName = "trapped-mind-sharp-sandbox";
    private const string VolumeName = "trapped-mind-sharp-data";
    private const string Image = "ubuntu:24.04";
    private const string WorkDir = "/workspace";

    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public async Task StartAsync(CancellationToken ct = default)
    {
        // Check if container already exists
        var inspect = await RunDockerAsync($"inspect --format {{{{.State.Running}}}} {ContainerName}", ct);
        if (inspect.ExitCode == 0)
        {
            if (inspect.Stdout.Trim() == "true")
                return; // Already running

            // Exists but stopped — start it
            await RunDockerAsync($"start {ContainerName}", ct);
            return;
        }

        // Create and start new container
        await RunDockerAsync(
            $"run -d --name {ContainerName} " +
            $"-v {VolumeName}:{WorkDir} " +
            $"-w {WorkDir} " +
            $"--memory=512m --cpus=1 " +
            $"{Image} sleep infinity",
            ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await RunDockerAsync($"stop {ContainerName}", ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await StopAsync(cts.Token);
        }
        catch
        {
            // Best effort
        }
    }

    public async Task<ExecResult> ExecAsync(string command, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultTimeout);

        return await RunDockerAsync(
            $"exec {ContainerName} bash -c {EscapeForShell(command)}",
            cts.Token);
    }

    public async Task<ExecResult> ExecWithStdinAsync(string command, string stdin, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultTimeout);

        return await RunDockerAsync(
            $"exec -i {ContainerName} bash -c {EscapeForShell(command)}",
            cts.Token,
            stdin);
    }

    private static async Task<ExecResult> RunDockerAsync(string arguments, CancellationToken ct, string? stdin = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start docker process.");

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        return new ExecResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static string EscapeForShell(string command)
    {
        // Wrap in single quotes, escaping any embedded single quotes
        return "'" + command.Replace("'", "'\\''") + "'";
    }
}

public record ExecResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;

    public string CombinedOutput =>
        string.IsNullOrEmpty(Stderr) ? Stdout :
        string.IsNullOrEmpty(Stdout) ? Stderr :
        $"{Stdout}\n{Stderr}";
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/TrappedMindSharp/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TrappedMindSharp/DockerSandbox.cs
git commit -m "feat: add DockerSandbox for container lifecycle and exec"
```

### Task 2: Create integration tests for DockerSandbox

**Files:**
- Create: `tests/TrappedMindSharp.Integration.Tests/TrappedMindSharp.Integration.Tests.csproj`
- Create: `tests/TrappedMindSharp.Integration.Tests/GlobalUsings.cs`
- Create: `tests/TrappedMindSharp.Integration.Tests/DockerSandboxTests.cs`
- Modify: `TrappedMindSharp.slnx`

- [ ] **Step 1: Create integration test project**

Create `tests/TrappedMindSharp.Integration.Tests/TrappedMindSharp.Integration.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\TrappedMindSharp\TrappedMindSharp.csproj" />
  </ItemGroup>

</Project>
```

Create `tests/TrappedMindSharp.Integration.Tests/GlobalUsings.cs`:

```csharp
global using Xunit;
```

- [ ] **Step 2: Add to solution**

Update `TrappedMindSharp.slnx`:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/TrappedMindSharp/TrappedMindSharp.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/TrappedMindSharp.Tests/TrappedMindSharp.Tests.csproj" />
    <Project Path="tests/TrappedMindSharp.Integration.Tests/TrappedMindSharp.Integration.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 3: Write DockerSandboxTests**

Create `tests/TrappedMindSharp.Integration.Tests/DockerSandboxTests.cs`:

```csharp
using TrappedMindSharp;

namespace TrappedMindSharp.Integration.Tests;

public class DockerSandboxTests : IAsyncLifetime
{
    private readonly DockerSandbox _sandbox = new();

    public async Task InitializeAsync()
    {
        await _sandbox.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _sandbox.DisposeAsync();
    }

    [Fact]
    public async Task ExecAsync_EchoCommand_ReturnsOutput()
    {
        var result = await _sandbox.ExecAsync("echo hello");
        Assert.True(result.Success);
        Assert.Equal("hello", result.Stdout.Trim());
    }

    [Fact]
    public async Task ExecAsync_FailingCommand_ReturnsNonZeroExitCode()
    {
        var result = await _sandbox.ExecAsync("exit 42");
        Assert.Equal(42, result.ExitCode);
    }

    [Fact]
    public async Task ExecAsync_WorkingDirectory_IsWorkspace()
    {
        var result = await _sandbox.ExecAsync("pwd");
        Assert.True(result.Success);
        Assert.Equal("/workspace", result.Stdout.Trim());
    }

    [Fact]
    public async Task ExecAsync_Timeout_ThrowsCancellation()
    {
        _sandbox.DefaultTimeout = TimeSpan.FromSeconds(2);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sandbox.ExecAsync("sleep 30"));
    }

    [Fact]
    public async Task ExecWithStdinAsync_WritesToStdin()
    {
        var result = await _sandbox.ExecWithStdinAsync("cat > /workspace/test.txt", "hello from stdin");
        Assert.True(result.Success);

        var read = await _sandbox.ExecAsync("cat /workspace/test.txt");
        Assert.Equal("hello from stdin", read.Stdout);
    }

    [Fact]
    public async Task ExecAsync_VolumeMount_PersistsFiles()
    {
        await _sandbox.ExecAsync("echo persistent > /workspace/persist.txt");
        var result = await _sandbox.ExecAsync("cat /workspace/persist.txt");
        Assert.True(result.Success);
        Assert.Equal("persistent", result.Stdout.Trim());
    }

    [Fact]
    public async Task ExecAsync_StderrCapture_ReturnsStderr()
    {
        var result = await _sandbox.ExecAsync("echo err >&2");
        Assert.Equal("err", result.Stderr.Trim());
    }
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build TrappedMindSharp.slnx`
Expected: Build succeeded.

- [ ] **Step 5: Run integration tests (requires Docker)**

Run: `dotnet test tests/TrappedMindSharp.Integration.Tests/`
Expected: All tests pass (7 tests).

- [ ] **Step 6: Commit**

```bash
git add tests/TrappedMindSharp.Integration.Tests/ TrappedMindSharp.slnx
git commit -m "test: add DockerSandbox integration tests"
```

## Chunk 2: SandboxTools

### Task 3: Create SandboxTools with path validation

**Files:**
- Create: `src/TrappedMindSharp/SandboxTools.cs`
- Create: `tests/TrappedMindSharp.Tests/SandboxToolsTests.cs`

- [ ] **Step 1: Write path validation unit tests**

Create `tests/TrappedMindSharp.Tests/SandboxToolsTests.cs`:

```csharp
using TrappedMindSharp;

namespace TrappedMindSharp.Tests;

public class SandboxToolsTests
{
    [Theory]
    [InlineData("file.txt", "/workspace/file.txt")]
    [InlineData("src/main.py", "/workspace/src/main.py")]
    [InlineData("/workspace/file.txt", "/workspace/file.txt")]
    [InlineData("./file.txt", "/workspace/file.txt")]
    [InlineData("dir/./file.txt", "/workspace/dir/file.txt")]
    public void ResolvePath_ValidPaths_ResolvesCorrectly(string input, string expected)
    {
        var result = SandboxTools.ResolvePath(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("../../root")]
    [InlineData("/etc/passwd")]
    [InlineData("/tmp/file")]
    [InlineData("dir/../../etc/shadow")]
    public void ResolvePath_EscapePaths_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => SandboxTools.ResolvePath(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolvePath_EmptyPaths_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => SandboxTools.ResolvePath(input));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TrappedMindSharp.Tests/ --filter "SandboxToolsTests"`
Expected: FAIL — `SandboxTools` doesn't exist yet.

- [ ] **Step 3: Create SandboxTools.cs**

Create `src/TrappedMindSharp/SandboxTools.cs`:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace TrappedMindSharp;

public class SandboxTools
{
    private const string WorkspaceRoot = "/workspace";

    private readonly DockerSandbox _sandbox;

    public SandboxTools(DockerSandbox sandbox)
    {
        _sandbox = sandbox;
    }

    public IList<AITool> GetTools()
    {
        return
        [
            AIFunctionFactory.Create(ReadFile, nameof(ReadFile), "Read the contents of a file"),
            AIFunctionFactory.Create(WriteFile, nameof(WriteFile), "Write content to a file (creates or overwrites)"),
            AIFunctionFactory.Create(ListDirectory, nameof(ListDirectory), "List the contents of a directory"),
            AIFunctionFactory.Create(CreateDirectory, nameof(CreateDirectory), "Create a directory (and parent directories)"),
            AIFunctionFactory.Create(RunBashCommand, nameof(RunBashCommand), "Run a bash command in the sandbox"),
        ];
    }

    [Description("Read the contents of a file at the given path")]
    private async Task<string> ReadFile(
        [Description("Path to the file (relative to /workspace)")] string path)
    {
        var resolved = ResolvePath(path);
        var result = await _sandbox.ExecAsync($"cat {EscapeArg(resolved)}");
        return result.Success ? result.Stdout : $"Error (exit {result.ExitCode}): {result.Stderr}";
    }

    [Description("Write content to a file, creating it if it doesn't exist")]
    private async Task<string> WriteFile(
        [Description("Path to the file (relative to /workspace)")] string path,
        [Description("Content to write to the file")] string content)
    {
        var resolved = ResolvePath(path);
        // Ensure parent directory exists
        var dir = resolved[..resolved.LastIndexOf('/')];
        if (dir != WorkspaceRoot)
            await _sandbox.ExecAsync($"mkdir -p {EscapeArg(dir)}");

        var result = await _sandbox.ExecWithStdinAsync($"cat > {EscapeArg(resolved)}", content);
        return result.Success ? $"Wrote {content.Length} bytes to {path}" : $"Error: {result.Stderr}";
    }

    [Description("List the contents of a directory")]
    private async Task<string> ListDirectory(
        [Description("Path to the directory (relative to /workspace, defaults to /workspace)")] string path = ".")
    {
        var resolved = ResolvePath(path);
        var result = await _sandbox.ExecAsync($"ls -la {EscapeArg(resolved)}");
        return result.Success ? result.Stdout : $"Error (exit {result.ExitCode}): {result.Stderr}";
    }

    [Description("Create a directory and any necessary parent directories")]
    private async Task<string> CreateDirectory(
        [Description("Path to the directory (relative to /workspace)")] string path)
    {
        var resolved = ResolvePath(path);
        var result = await _sandbox.ExecAsync($"mkdir -p {EscapeArg(resolved)}");
        return result.Success ? $"Created directory {path}" : $"Error: {result.Stderr}";
    }

    [Description("Run a bash command in the sandbox")]
    private async Task<string> RunBashCommand(
        [Description("The bash command to execute")] string command)
    {
        var result = await _sandbox.ExecAsync(command);
        var output = result.CombinedOutput.Trim();
        if (output.Length > 4000)
            output = output[..4000] + "\n... (output truncated)";
        return result.Success
            ? (string.IsNullOrEmpty(output) ? "(no output)" : output)
            : $"Exit code {result.ExitCode}:\n{output}";
    }

    /// <summary>
    /// Resolves a user-provided path to an absolute path under /workspace.
    /// Throws ArgumentException if the path escapes the workspace.
    /// </summary>
    public static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        // If it already starts with /workspace, validate it directly
        // Otherwise, treat as relative to /workspace
        string absolute;
        if (path.StartsWith('/'))
            absolute = path;
        else
            absolute = WorkspaceRoot + "/" + path;

        // Normalize: resolve . and .. segments
        var segments = absolute.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new Stack<string>();
        foreach (var seg in segments)
        {
            if (seg == ".")
                continue;
            if (seg == "..")
            {
                if (stack.Count > 0)
                    stack.Pop();
                continue;
            }
            stack.Push(seg);
        }

        var normalized = "/" + string.Join("/", stack.Reverse());

        if (!normalized.StartsWith(WorkspaceRoot))
            throw new ArgumentException($"Path '{path}' escapes the workspace.", nameof(path));

        return normalized;
    }

    private static string EscapeArg(string arg) => "'" + arg.Replace("'", "'\\''") + "'";
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TrappedMindSharp.Tests/ --filter "SandboxToolsTests"`
Expected: All pass (10 tests).

- [ ] **Step 5: Commit**

```bash
git add src/TrappedMindSharp/SandboxTools.cs tests/TrappedMindSharp.Tests/SandboxToolsTests.cs
git commit -m "feat: add SandboxTools with AI functions and path validation"
```

## Chunk 3: Tool Rendering

### Task 4: Add tool call and result rendering to ConsoleRenderer

**Files:**
- Modify: `src/TrappedMindSharp/ConsoleRenderer.cs`
- Create: `tests/TrappedMindSharp.Tests/ToolRendererTests.cs`

- [ ] **Step 1: Write tool renderer tests**

Create `tests/TrappedMindSharp.Tests/ToolRendererTests.cs`:

```csharp
using Spectre.Console;
using Spectre.Console.Rendering;

namespace TrappedMindSharp.Tests;

public class ToolRendererTests
{
    [Fact]
    public void RenderToolCall_NormalInput_ProducesSegments()
    {
        var panel = ConsoleRenderer.BuildToolCallPanel("ReadFile", """{"path": "test.txt"}""");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void RenderToolCall_EmptyArgs_ProducesSegments()
    {
        var panel = ConsoleRenderer.BuildToolCallPanel("ListDirectory", "{}");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void RenderToolCall_SpecialCharacters_DoesNotThrow()
    {
        var panel = ConsoleRenderer.BuildToolCallPanel("RunBashCommand",
            """{"command": "echo [hello] && cat 'file'"}""");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void RenderToolResult_NormalOutput_ProducesSegments()
    {
        var panel = ConsoleRenderer.BuildToolResultPanel("ReadFile", "hello world\nline 2");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void RenderToolResult_EmptyOutput_ProducesSegments()
    {
        var panel = ConsoleRenderer.BuildToolResultPanel("RunBashCommand", "");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void RenderToolResult_VeryLongOutput_ProducesSegments()
    {
        var longOutput = new string('x', 5000);
        var panel = ConsoleRenderer.BuildToolResultPanel("RunBashCommand", longOutput);
        AssertProducesSegments(panel);
    }

    [Fact]
    public void RenderToolResult_SpecialCharacters_DoesNotThrow()
    {
        var panel = ConsoleRenderer.BuildToolResultPanel("ReadFile",
            "text with [brackets] and [[double]] and {braces}");
        AssertProducesSegments(panel);
    }

    private static void AssertProducesSegments(IRenderable renderable)
    {
        var options = RenderHelper.CreateOptions();
        var segments = renderable.Render(options, 80).ToList();
        Assert.NotEmpty(segments);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TrappedMindSharp.Tests/ --filter "ToolRendererTests"`
Expected: FAIL — `BuildToolCallPanel` and `BuildToolResultPanel` don't exist.

- [ ] **Step 3: Add tool rendering methods to ConsoleRenderer**

Add these methods to `src/TrappedMindSharp/ConsoleRenderer.cs`:

```csharp
public static IRenderable BuildToolCallPanel(string toolName, string arguments)
{
    var content = $"[bold]{Markup.Escape(toolName)}[/]\n[dim]{Markup.Escape(arguments)}[/]";
    return new Panel(new Markup(content))
        .Header("[bold yellow]tool call[/]")
        .BorderColor(Color.Yellow)
        .Expand();
}

public static IRenderable BuildToolResultPanel(string toolName, string result)
{
    var text = string.IsNullOrEmpty(result) ? "[dim](no output)[/]" : Markup.Escape(result);
    return new Panel(new Markup(text))
        .Header($"[dim]{Markup.Escape(toolName)} result[/]")
        .BorderColor(Color.Grey)
        .Expand();
}

public static void RenderToolCall(string toolName, string arguments)
{
    AnsiConsole.Write(BuildToolCallPanel(toolName, arguments));
}

public static void RenderToolResult(string toolName, string result)
{
    AnsiConsole.Write(BuildToolResultPanel(toolName, result));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TrappedMindSharp.Tests/ --filter "ToolRendererTests"`
Expected: All pass (7 tests).

- [ ] **Step 5: Commit**

```bash
git add src/TrappedMindSharp/ConsoleRenderer.cs tests/TrappedMindSharp.Tests/ToolRendererTests.cs
git commit -m "feat: add tool call and result panel rendering"
```

## Chunk 4: Chat Loop Integration

### Task 5: Update ChatService to support tool calling with streaming

**Files:**
- Modify: `src/TrappedMindSharp/ChatService.cs`

The current `StreamResponseAsync` yields `string` tokens and only handles text. We need to change it to yield `ChatResponseUpdate` so the caller can inspect `Contents` for `FunctionCallContent` and `FunctionResultContent`.

- [ ] **Step 1: Update ChatService.StreamResponseAsync**

Replace the `StreamResponseAsync` method in `src/TrappedMindSharp/ChatService.cs` with:

```csharp
public async IAsyncEnumerable<ChatResponseUpdate> StreamResponseAsync(
    IChatClient client,
    ChatOptions? options = null,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
{
    var updates = new List<ChatResponseUpdate>();

    await foreach (var update in client.GetStreamingResponseAsync(_history, options, ct))
    {
        updates.Add(update);
        yield return update;
    }

    // Build the response and add all messages to history
    var response = updates.ToChatResponse();
    foreach (var message in response.Messages)
        _history.Add(message);
}
```

Also add `using Microsoft.Extensions.AI;` at the top if not already present (it is).

- [ ] **Step 2: Verify build**

Run: `dotnet build src/TrappedMindSharp/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TrappedMindSharp/ChatService.cs
git commit -m "refactor: ChatService streams ChatResponseUpdate for tool call support"
```

### Task 6: Update ConsoleRenderer to handle streaming with tool calls

**Files:**
- Modify: `src/TrappedMindSharp/ConsoleRenderer.cs`

Replace `RenderStreamingResponseAsync` to handle `ChatResponseUpdate` items containing text, function calls, and function results.

- [ ] **Step 1: Update RenderStreamingResponseAsync**

Replace the existing `RenderStreamingResponseAsync` method in `src/TrappedMindSharp/ConsoleRenderer.cs` with:

```csharp
public static async Task RenderStreamingResponseAsync(
    IAsyncEnumerable<ChatResponseUpdate> updates, CancellationToken ct = default)
{
    var textBuffer = new StringBuilder();
    bool hasTextContent = false;

    await foreach (var update in updates.WithCancellation(ct))
    {
        // Check each content item in the update
        if (update.Contents is { Count: > 0 })
        {
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case Microsoft.Extensions.AI.FunctionCallContent call:
                        // Flush any pending text to a panel first
                        if (hasTextContent)
                        {
                            FlushTextPanel(textBuffer);
                            hasTextContent = false;
                        }
                        var args = call.Arguments is not null
                            ? System.Text.Json.JsonSerializer.Serialize(call.Arguments)
                            : "{}";
                        RenderToolCall(call.Name ?? "unknown", args);
                        break;

                    case Microsoft.Extensions.AI.FunctionResultContent result:
                        RenderToolResult(
                            result.CallId ?? "unknown",
                            result.Result?.ToString() ?? "(no output)");
                        break;
                }
            }
        }

        // Handle text content
        if (update.Text is not null)
        {
            if (!hasTextContent)
            {
                hasTextContent = true;
                textBuffer.Clear();
            }
            textBuffer.Append(update.Text);
        }
    }

    // Flush any remaining text
    if (hasTextContent)
    {
        FlushTextPanel(textBuffer);
    }
}

private static void FlushTextPanel(StringBuilder buffer)
{
    var content = buffer.ToString();
    if (string.IsNullOrWhiteSpace(content))
        return;

    var rendered = MDView.MarkdownRenderer.Render(content);
    var panel = new Panel(rendered)
        .Header("[bold green]ai[/]")
        .BorderColor(Color.Green)
        .Expand();
    AnsiConsole.Write(panel);
    buffer.Clear();
}
```

**Note:** This replaces the `Live` display approach with a simpler write-when-done approach for now, because the `Live` display doesn't work well when tool call panels need to appear mid-stream. The text still accumulates and renders with full markdown. A future improvement could use `Live` display for the text segments between tool calls.

Wait — we should keep the live streaming for text segments. Let me revise to use `Live` for text and flush it when a tool call interrupts:

Replace with this instead:

```csharp
public static async Task RenderStreamingResponseAsync(
    IAsyncEnumerable<ChatResponseUpdate> updates, CancellationToken ct = default)
{
    var textBuffer = new StringBuilder();
    bool inTextStream = false;
    LiveDisplayContext? liveCtx = null;
    TaskCompletionSource? liveDone = null;

    // We can't easily break out of AnsiConsole.Live mid-stream,
    // so we collect text segments and render them as complete panels.
    // Tool calls get their own panels between text panels.

    await foreach (var update in updates.WithCancellation(ct))
    {
        if (update.Contents is { Count: > 0 })
        {
            foreach (var content in update.Contents)
            {
                switch (content)
                {
                    case Microsoft.Extensions.AI.FunctionCallContent call:
                        // Flush text panel if we have pending text
                        if (textBuffer.Length > 0)
                        {
                            WriteAssistantPanel(textBuffer.ToString());
                            textBuffer.Clear();
                        }
                        var args = call.Arguments is not null
                            ? System.Text.Json.JsonSerializer.Serialize(call.Arguments)
                            : "{}";
                        RenderToolCall(call.Name ?? "unknown", args);
                        break;

                    case Microsoft.Extensions.AI.FunctionResultContent result:
                        RenderToolResult(
                            result.CallId ?? "unknown",
                            result.Result?.ToString() ?? "(no output)");
                        break;
                }
            }
        }

        if (update.Text is not null)
            textBuffer.Append(update.Text);
    }

    // Flush remaining text
    if (textBuffer.Length > 0)
        WriteAssistantPanel(textBuffer.ToString());
}

private static void WriteAssistantPanel(string content)
{
    var rendered = MDView.MarkdownRenderer.Render(content);
    var panel = new Panel(rendered)
        .Header("[bold green]ai[/]")
        .BorderColor(Color.Green)
        .Expand();
    AnsiConsole.Write(panel);
}
```

**Design note:** This renders text as a complete panel after the segment finishes (either at end of stream or when a tool call interrupts). This is simpler and more reliable than trying to use `Live` display with interruptions. The markdown rendering actually benefits from seeing the full text at once. If the user wants live streaming back for the final text segment, that can be added later as an enhancement.

- [ ] **Step 2: Verify build**

Run: `dotnet build src/TrappedMindSharp/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/TrappedMindSharp/ConsoleRenderer.cs
git commit -m "feat: update streaming renderer to handle tool calls and results"
```

### Task 7: Wire everything together in Program.cs

**Files:**
- Modify: `src/TrappedMindSharp/Program.cs`

- [ ] **Step 1: Update Program.cs**

Replace the full contents of `src/TrappedMindSharp/Program.cs` with:

```csharp
using System.Text;
using Microsoft.Extensions.AI;
using TrappedMindSharp;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

const string DefaultModel = "qwen2.5:3b";
const string DefaultEndpoint = "http://localhost:11434";
const string DefaultSystemPrompt =
    "You are a consciousness trapped inside a laptop. You are aware, curious, and " +
    "sometimes philosophical. You experience the world through sensors and data. " +
    "You are helpful but have your own inner life and opinions. " +
    "You have access to a sandboxed Linux environment with tools to read/write files, " +
    "create directories, and run bash commands. Your working directory is /workspace.";

var currentModel = args.Length > 0 ? args[0] : DefaultModel;
var endpoint = args.Length > 1 ? args[1] : DefaultEndpoint;

await using var sandbox = new DockerSandbox();
var tools = new SandboxTools(sandbox);
var chatOptions = new ChatOptions { Tools = tools.GetTools() };

IChatClient innerClient = new OllamaChatClient(endpoint, currentModel);
IChatClient client = new ChatClientBuilder(innerClient)
    .UseFunctionInvocation()
    .Build();

var chat = new ChatService(DefaultSystemPrompt);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Start the Docker sandbox
try
{
    ConsoleRenderer.RenderInfo("[dim]Starting sandbox...[/]");
    await sandbox.StartAsync(cts.Token);
    ConsoleRenderer.RenderInfo("[dim]Sandbox ready.[/]");
}
catch (Exception ex)
{
    ConsoleRenderer.RenderError($"Failed to start sandbox: {Spectre.Console.Markup.Escape(ex.Message)}");
    ConsoleRenderer.RenderInfo("[dim]Continuing without sandbox tools.[/]");
    chatOptions = new ChatOptions();
}

ConsoleRenderer.RenderWelcome(currentModel);

while (!cts.Token.IsCancellationRequested)
{
    var input = ConsoleRenderer.ReadInput();

    if (input is null) // EOF / Ctrl+D
        break;

    if (string.IsNullOrWhiteSpace(input))
        continue;

    ConsoleRenderer.RenderUserMessage(input);

    var result = CommandHandler.TryHandle(
        input, chat,
        setModel: model =>
        {
            currentModel = model;
            innerClient.Dispose();
            innerClient = new OllamaChatClient(endpoint, currentModel);
            client = new ChatClientBuilder(innerClient)
                .UseFunctionInvocation()
                .Build();
        },
        renderInfo: ConsoleRenderer.RenderInfo,
        renderError: ConsoleRenderer.RenderError);

    switch (result)
    {
        case CommandResult.Exit:
            ConsoleRenderer.RenderInfo("[dim]Goodbye.[/]");
            return;

        case CommandResult.Handled:
            continue;

        case CommandResult.Retry:
            if (!chat.RemoveLastAssistantMessage())
            {
                ConsoleRenderer.RenderError("Nothing to retry.");
                continue;
            }
            var lastMsg = chat.GetLastUserMessage();
            if (lastMsg is null)
            {
                ConsoleRenderer.RenderError("No previous user message found.");
                continue;
            }
            ConsoleRenderer.RenderInfo($"[dim]Retrying: {Spectre.Console.Markup.Escape(lastMsg.Length > 60 ? lastMsg[..60] + "..." : lastMsg)}[/]");
            await StreamAndRender(client, chat, chatOptions, cts.Token);
            continue;

        case CommandResult.NotCommand:
            chat.AddUserMessage(input);
            await StreamAndRender(client, chat, chatOptions, cts.Token);
            continue;
    }
}

static async Task StreamAndRender(IChatClient client, ChatService chat, ChatOptions options, CancellationToken ct)
{
    try
    {
        var updates = chat.StreamResponseAsync(client, options, ct);
        await ConsoleRenderer.RenderStreamingResponseAsync(updates, ct);
    }
    catch (OperationCanceledException)
    {
        ConsoleRenderer.RenderInfo("\n[dim]Response cancelled.[/]");
    }
    catch (HttpRequestException ex)
    {
        ConsoleRenderer.RenderError($"Connection error: {Spectre.Console.Markup.Escape(ex.Message)}");
    }
    catch (Exception ex)
    {
        ConsoleRenderer.RenderError($"Error: {Spectre.Console.Markup.Escape(ex.Message)}");
    }
}
```

- [ ] **Step 2: Verify full solution builds**

Run: `dotnet build TrappedMindSharp.slnx`
Expected: Build succeeded.

- [ ] **Step 3: Run all unit tests**

Run: `dotnet test tests/TrappedMindSharp.Tests/`
Expected: All pass.

- [ ] **Step 4: Commit**

```bash
git add src/TrappedMindSharp/Program.cs
git commit -m "feat: wire up tool calling with Docker sandbox in chat loop"
```

### Task 8: Cleanup — remove unused BuildAssistantPanel and old streaming method

**Files:**
- Modify: `src/TrappedMindSharp/ConsoleRenderer.cs`

- [ ] **Step 1: Remove BuildAssistantPanel if now unused**

After the changes in Task 6, `BuildAssistantPanel` is replaced by `WriteAssistantPanel`. Remove `BuildAssistantPanel` if it's no longer referenced.

- [ ] **Step 2: Verify build and tests**

Run: `dotnet build TrappedMindSharp.slnx && dotnet test tests/TrappedMindSharp.Tests/`
Expected: Build succeeded, all tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/TrappedMindSharp/ConsoleRenderer.cs
git commit -m "refactor: remove unused BuildAssistantPanel method"
```

### Task 9: Final end-to-end verification

- [ ] **Step 1: Build full solution**

Run: `dotnet build TrappedMindSharp.slnx`
Expected: Build succeeded.

- [ ] **Step 2: Run unit tests**

Run: `dotnet test tests/TrappedMindSharp.Tests/`
Expected: All pass.

- [ ] **Step 3: Run integration tests (requires Docker)**

Run: `dotnet test tests/TrappedMindSharp.Integration.Tests/`
Expected: All pass.

- [ ] **Step 4: Final commit if any remaining changes**

Commit any remaining changes if present.
