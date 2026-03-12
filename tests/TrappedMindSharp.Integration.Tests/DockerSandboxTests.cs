using TrappedMindSharp;

namespace TrappedMindSharp.Integration.Tests;

public class DockerSandboxFixture : IAsyncLifetime
{
    public DockerSandbox Sandbox { get; } = new();

    public async Task InitializeAsync()
    {
        await Sandbox.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Sandbox.DisposeAsync();
    }
}

[CollectionDefinition("Docker")]
public class DockerCollection : ICollectionFixture<DockerSandboxFixture>;

[Collection("Docker")]
public class DockerSandboxTests
{
    private readonly DockerSandbox _sandbox;

    public DockerSandboxTests(DockerSandboxFixture fixture)
    {
        _sandbox = fixture.Sandbox;
    }

    [Fact]
    public async Task ExecAsync_EchoCommand_ReturnsOutput()
    {
        var result = await _sandbox.ExecAsync("echo hello");
        Assert.True(result.Success, $"Expected success but got exit {result.ExitCode}: {result.Stderr}");
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
        Assert.True(result.Success, $"Expected success but got exit {result.ExitCode}: {result.Stderr}");
        Assert.Equal("/workspace", result.Stdout.Trim());
    }

    [Fact]
    public async Task ExecAsync_Timeout_ThrowsCancellation()
    {
        var sandbox = new DockerSandbox { DefaultTimeout = TimeSpan.FromSeconds(2) };
        // Start this sandbox too (it'll reuse the existing container)
        await sandbox.StartAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sandbox.ExecAsync("sleep 30"));
    }

    [Fact]
    public async Task ExecWithStdinAsync_WritesToStdin()
    {
        var result = await _sandbox.ExecWithStdinAsync("cat > /workspace/test_stdin.txt", "hello from stdin");
        Assert.True(result.Success, $"Write failed: {result.Stderr}");

        var read = await _sandbox.ExecAsync("cat /workspace/test_stdin.txt");
        Assert.Equal("hello from stdin", read.Stdout);
    }

    [Fact]
    public async Task ExecAsync_VolumeMount_PersistsFiles()
    {
        await _sandbox.ExecAsync("echo persistent > /workspace/persist.txt");
        var result = await _sandbox.ExecAsync("cat /workspace/persist.txt");
        Assert.True(result.Success, $"Read failed: {result.Stderr}");
        Assert.Equal("persistent", result.Stdout.Trim());
    }

    [Fact]
    public async Task ExecAsync_StderrCapture_ReturnsStderr()
    {
        var result = await _sandbox.ExecAsync("echo err >&2");
        Assert.Equal("err", result.Stderr.Trim());
    }
}
