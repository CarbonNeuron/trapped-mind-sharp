using System.Text;
using Spectre.Console;
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
        // Clear the prompt line so the panel replaces it
        Console.SetCursorPosition(0, Console.CursorTop - 1);
        Console.Write(new string(' ', Console.WindowWidth));
        Console.SetCursorPosition(0, Console.CursorTop);
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

    public static async Task RenderStreamingResponseAsync(IAsyncEnumerable<string> tokens, CancellationToken ct = default)
    {
        var buffer = new StringBuilder();

        await AnsiConsole.Live(BuildAssistantPanel(""))
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                await foreach (var token in tokens.WithCancellation(ct))
                {
                    buffer.Append(token);
                    ctx.UpdateTarget(BuildAssistantPanel(buffer.ToString()));
                    ctx.Refresh();
                }
            });
    }

    private static IRenderable BuildAssistantPanel(string content)
    {
        var rendered = MDView.MarkdownRenderer.Render(content);
        return new Panel(rendered)
            .Header("[bold green]ai[/]")
            .BorderColor(Color.Green)
            .Expand();
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
