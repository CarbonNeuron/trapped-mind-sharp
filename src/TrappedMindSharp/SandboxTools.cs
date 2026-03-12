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

    public static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        string absolute;
        if (path.StartsWith('/'))
            absolute = path;
        else
            absolute = WorkspaceRoot + "/" + path;

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
