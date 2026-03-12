using MDView;
using Spectre.Console.Rendering;

namespace TrappedMindSharp.Tests;

public class MalformedMarkdownTests
{
    // -- Unclosed inline formatting --

    [Theory]
    [InlineData("This is **unclosed bold")]
    [InlineData("This is *unclosed italic")]
    [InlineData("This is __unclosed bold")]
    [InlineData("This is _unclosed italic")]
    [InlineData("Nested **bold *and italic** unclosed")]
    [InlineData("Triple ***unclosed")]
    public void Render_UnclosedInlineFormatting_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    // -- Unclosed code --

    [Theory]
    [InlineData("`unclosed inline code")]
    [InlineData("``double backtick unclosed")]
    [InlineData("```\nunclosed code fence")]
    [InlineData("```csharp\nConsole.WriteLine(\"hi\");\nno closing fence")]
    [InlineData("````\nfour backtick fence unclosed")]
    public void Render_UnclosedCode_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    // -- Spectre.Console special characters --

    [Theory]
    [InlineData("[brackets] everywhere [here] and [there]")]
    [InlineData("Text with [[double brackets]]")]
    [InlineData("[bold]not really bold[/]")]
    [InlineData("[red on blue]spectre markup attempt[/]")]
    [InlineData("Price is $[100] or [50%]")]
    public void Render_SpectreMarkupCharacters_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    // -- Broken lists --

    [Theory]
    [InlineData("- ")]
    [InlineData("- item\n-")]
    [InlineData("1. ")]
    [InlineData("1. first\n2.")]
    [InlineData("- item\n  - nested\n    - deep\n- ")]
    public void Render_MalformedLists_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    // -- Streaming partial tokens (mid-word arrivals) --

    [Theory]
    [InlineData("**")]
    [InlineData("**b")]
    [InlineData("**bo")]
    [InlineData("**bol")]
    [InlineData("**bold")]
    [InlineData("**bold*")]
    [InlineData("**bold**")]
    public void Render_IncrementalBoldToken_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    [Theory]
    [InlineData("```")]
    [InlineData("```c")]
    [InlineData("```cs")]
    [InlineData("```csharp")]
    [InlineData("```csharp\n")]
    [InlineData("```csharp\nvar")]
    [InlineData("```csharp\nvar x = 1;")]
    [InlineData("```csharp\nvar x = 1;\n")]
    [InlineData("```csharp\nvar x = 1;\n`")]
    [InlineData("```csharp\nvar x = 1;\n``")]
    [InlineData("```csharp\nvar x = 1;\n```")]
    public void Render_IncrementalCodeFence_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    // -- Edge cases --

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\n")]
    [InlineData("\n\n\n")]
    [InlineData("\t\t")]
    [InlineData("\r\n\r\n")]
    public void Render_WhitespaceVariants_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    [Theory]
    [InlineData("# ")]
    [InlineData("## ")]
    [InlineData("> ")]
    [InlineData("> > > deeply nested empty quote")]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("___")]
    public void Render_EmptyStructuralElements_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    [Theory]
    [InlineData("| broken | table")]
    [InlineData("| no | header |\n| row |")]
    [InlineData("| | | |")]
    public void Render_MalformedTables_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    [Theory]
    [InlineData("[link text](")]
    [InlineData("[link text](http://")]
    [InlineData("[](empty)")]
    [InlineData("![broken image](")]
    public void Render_MalformedLinks_DoesNotThrow(string input)
    {
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    [Fact]
    public void Render_VeryLongLine_DoesNotThrow()
    {
        var input = new string('a', 10_000);
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    [Fact]
    public void Render_DeeplyNestedFormatting_DoesNotThrow()
    {
        var input = "**bold *italic `code` italic* bold** normal";
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    [Fact]
    public void Render_MixedContentMidStream_DoesNotThrow()
    {
        // Simulates a partially arrived complex response
        var input = """
            Here's how to do it:

            ```python
            def hello():
                print("world
            """;
        var result = MarkdownRenderer.Render(input);
        AssertProducesSegments(result);
    }

    // -- Helper --

    private static void AssertProducesSegments(IRenderable renderable)
    {
        var options = RenderHelper.CreateOptions();
        var segments = renderable.Render(options, 80).ToList();
        Assert.NotEmpty(segments);
    }
}
