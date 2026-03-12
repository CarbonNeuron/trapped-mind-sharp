using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Spectre.Console;
using Spectre.Console.Advanced;
using Spectre.Console.Rendering;

namespace TrappedMindSharp;

public static class ConsoleRenderer
{
    public static void RenderWelcome(string model)
    {
        AnsiConsole.Write(new Rule("[bold blue]Trapped Mind Sharp[/]").RuleStyle("dim"));
        AnsiConsole.MarkupLine($"[dim]Connected to Ollama model:[/] [bold]{Markup.Escape(model)}[/]");
        AnsiConsole.MarkupLine("[dim]Type /help for commands, /exit to quit.[/]");
        AnsiConsole.WriteLine();
    }

    public static string? ReadInput()
    {
        AnsiConsole.Markup("[bold cyan]you>[/] ");
        var input = Console.ReadLine();
        // Clear the prompt line: CUU(1) = move cursor up, EL(2) = erase entire line
        AnsiConsole.Console.WriteAnsi("\x1b[1A\x1b[2K");
        return input;
    }

    public static void RenderUserMessage(string message)
    {
        var panel = new Panel(Markup.Escape(message))
            .Header("[bold cyan]you[/]")
            .BorderColor(Color.Cyan)
            .Expand();
        AnsiConsole.Write(panel);
    }

    public static async Task RenderStreamingResponseAsync(
        IAsyncEnumerable<ChatResponseUpdate> updates, CancellationToken ct = default)
    {
        var textBuffer = new StringBuilder();

        await foreach (var update in updates.WithCancellation(ct))
        {
            if (update.Contents is { Count: > 0 })
            {
                foreach (var content in update.Contents)
                {
                    switch (content)
                    {
                        case FunctionCallContent call:
                            if (textBuffer.Length > 0)
                            {
                                WriteAssistantPanel(textBuffer.ToString());
                                textBuffer.Clear();
                            }
                            var args = call.Arguments is not null
                                ? JsonSerializer.Serialize(call.Arguments)
                                : "{}";
                            RenderToolCall(call.Name ?? "unknown", args);
                            break;

                        case FunctionResultContent result:
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

    public static void RenderInfo(string markup)
    {
        AnsiConsole.MarkupLine(markup);
    }

    public static void RenderError(string markup)
    {
        AnsiConsole.MarkupLine($"[red]{markup}[/]");
    }

    public static void RenderSeparator()
    {
        AnsiConsole.Write(new Rule().RuleStyle("dim"));
    }
}
