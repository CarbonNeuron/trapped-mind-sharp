using Spectre.Console;
using Spectre.Console.Rendering;

namespace TrappedMindSharp.Tests;

public class ToolRendererTests
{
    [Fact]
    public void BuildToolCallPanel_NormalInput_ProducesSegments()
    {
        var panel = ConsoleRenderer.BuildToolCallPanel("ReadFile", """{"path": "test.txt"}""");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void BuildToolCallPanel_EmptyArgs_ProducesSegments()
    {
        var panel = ConsoleRenderer.BuildToolCallPanel("ListDirectory", "{}");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void BuildToolCallPanel_SpecialCharacters_DoesNotThrow()
    {
        var panel = ConsoleRenderer.BuildToolCallPanel("RunBashCommand",
            """{"command": "echo [hello] && cat 'file'"}""");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void BuildToolResultPanel_NormalOutput_ProducesSegments()
    {
        var panel = ConsoleRenderer.BuildToolResultPanel("ReadFile", "hello world\nline 2");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void BuildToolResultPanel_EmptyOutput_ProducesSegments()
    {
        var panel = ConsoleRenderer.BuildToolResultPanel("RunBashCommand", "");
        AssertProducesSegments(panel);
    }

    [Fact]
    public void BuildToolResultPanel_VeryLongOutput_ProducesSegments()
    {
        var longOutput = new string('x', 5000);
        var panel = ConsoleRenderer.BuildToolResultPanel("RunBashCommand", longOutput);
        AssertProducesSegments(panel);
    }

    [Fact]
    public void BuildToolResultPanel_SpecialCharacters_DoesNotThrow()
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
