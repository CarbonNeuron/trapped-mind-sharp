using Spectre.Console;
using Spectre.Console.Rendering;

namespace TrappedMindSharp.Tests;

internal static class RenderHelper
{
    public static string GetPlainText(IRenderable renderable, int width = 120)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null)
        });
        console.Profile.Width = width;
        var options = RenderOptions.Create(console, console.Profile.Capabilities);
        var segments = renderable.Render(options, width);
        return string.Concat(segments.Select(s => s.IsLineBreak ? "\n" : s.Text));
    }

    public static RenderOptions CreateOptions(int width = 120)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null)
        });
        console.Profile.Width = width;
        return RenderOptions.Create(console, console.Profile.Capabilities);
    }
}
