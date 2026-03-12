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
