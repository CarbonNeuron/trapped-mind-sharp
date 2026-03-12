using System.Diagnostics;

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
        var inspect = await RunDockerAsync(["inspect", "--format", "{{.State.Running}}", ContainerName], ct);
        if (inspect.ExitCode == 0)
        {
            if (inspect.Stdout.Trim() == "true")
                return; // Already running

            // Exists but stopped — start it
            await RunDockerAsync(["start", ContainerName], ct);
            return;
        }

        // Create and start new container
        await RunDockerAsync([
            "run", "-d", "--name", ContainerName,
            "-v", $"{VolumeName}:{WorkDir}",
            "-w", WorkDir,
            "--memory=512m", "--cpus=1",
            Image, "sleep", "infinity"
        ], ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await RunDockerAsync(["stop", ContainerName], ct);
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
            ["exec", ContainerName, "bash", "-c", command],
            cts.Token);
    }

    public async Task<ExecResult> ExecWithStdinAsync(string command, string stdin, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultTimeout);

        return await RunDockerAsync(
            ["exec", "-i", ContainerName, "bash", "-c", command],
            cts.Token,
            stdin);
    }

    private static async Task<ExecResult> RunDockerAsync(string[] arguments, CancellationToken ct, string? stdin = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

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
}

public record ExecResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;

    public string CombinedOutput =>
        string.IsNullOrEmpty(Stderr) ? Stdout :
        string.IsNullOrEmpty(Stdout) ? Stderr :
        $"{Stdout}\n{Stderr}";
}
