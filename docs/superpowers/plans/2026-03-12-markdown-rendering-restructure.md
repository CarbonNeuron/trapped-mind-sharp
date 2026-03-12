# Markdown Rendering & Project Restructure Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add streaming markdown rendering to assistant messages using MDView.Renderer, restructure into src/tests folders with a .slnx solution file, and add unit tests for malformed input resilience.

**Architecture:** Restructure the flat project into `src/TrappedMindSharp/` and `tests/TrappedMindSharp.Tests/`. Add `MDView.Renderer` NuGet package and wire `MarkdownRenderer.Render()` into the live panel display. Add xUnit test project verifying malformed markdown doesn't crash rendering.

**Tech Stack:** .NET 10, Spectre.Console, MDView.Renderer (0.1.0, Markdig + TextMate), xUnit

---

## Chunk 1: Project Restructure

### Task 1: Move app project into src/ folder

**Files:**
- Move: `TrappedMindSharp.csproj`, `Program.cs`, `ChatService.cs`, `ConsoleRenderer.cs`, `CommandHandler.cs` → `src/TrappedMindSharp/`

- [ ] **Step 1: Create directory and move files**

```bash
mkdir -p src/TrappedMindSharp
git mv TrappedMindSharp.csproj src/TrappedMindSharp/
git mv Program.cs src/TrappedMindSharp/
git mv ChatService.cs src/TrappedMindSharp/
git mv ConsoleRenderer.cs src/TrappedMindSharp/
git mv CommandHandler.cs src/TrappedMindSharp/
```

- [ ] **Step 2: Verify build still works**

Run: `dotnet build src/TrappedMindSharp/`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor: move app project into src/TrappedMindSharp/"
```

### Task 2: Create .slnx solution file

**Files:**
- Create: `TrappedMindSharp.slnx`

The `.slnx` format is the new XML-based solution file format in .NET 10+.

- [ ] **Step 1: Create the .slnx file**

Create `TrappedMindSharp.slnx` at the repo root with this content:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/TrappedMindSharp/TrappedMindSharp.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 2: Verify solution builds**

Run: `dotnet build TrappedMindSharp.slnx`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add TrappedMindSharp.slnx
git commit -m "feat: add .slnx solution file"
```

### Task 3: Create test project and add to solution

**Files:**
- Create: `tests/TrappedMindSharp.Tests/TrappedMindSharp.Tests.csproj`
- Create: `tests/TrappedMindSharp.Tests/RenderHelper.cs`
- Modify: `TrappedMindSharp.slnx`

- [ ] **Step 1: Create test project csproj**

Create `tests/TrappedMindSharp.Tests/TrappedMindSharp.Tests.csproj`:

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
    <PackageReference Include="MDView.Renderer" Version="0.1.0" />
  </ItemGroup>

</Project>
```

Note: The test project references `MDView.Renderer` directly (not the app project) since it's testing markdown rendering resilience, not app internals.

- [ ] **Step 2: Create RenderHelper.cs**

Create `tests/TrappedMindSharp.Tests/RenderHelper.cs` — this is a utility to extract plain text from Spectre renderables for assertions (same pattern as MDView's own tests):

```csharp
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
```

- [ ] **Step 3: Add test project to .slnx**

Update `TrappedMindSharp.slnx`:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/TrappedMindSharp/TrappedMindSharp.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/TrappedMindSharp.Tests/TrappedMindSharp.Tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 4: Verify solution builds (both projects restore)**

Run: `dotnet build TrappedMindSharp.slnx`
Expected: Build succeeded (2 projects).

- [ ] **Step 5: Commit**

```bash
git add TrappedMindSharp.slnx tests/
git commit -m "feat: add xUnit test project with RenderHelper"
```

## Chunk 2: Markdown Rendering Integration

### Task 4: Add MDView.Renderer to app project and wire it in

**Files:**
- Modify: `src/TrappedMindSharp/TrappedMindSharp.csproj`
- Modify: `src/TrappedMindSharp/ConsoleRenderer.cs`

- [ ] **Step 1: Add MDView.Renderer package reference**

Add to `src/TrappedMindSharp/TrappedMindSharp.csproj` ItemGroup:

```xml
<PackageReference Include="MDView.Renderer" Version="0.1.0" />
```

- [ ] **Step 2: Update ConsoleRenderer.BuildAssistantPanel to use MarkdownRenderer**

In `src/TrappedMindSharp/ConsoleRenderer.cs`, replace the `BuildAssistantPanel` method:

```csharp
private static IRenderable BuildAssistantPanel(string content)
{
    var rendered = MDView.MarkdownRenderer.Render(content);
    return new Panel(rendered)
        .Header("[bold green]ai[/]")
        .BorderColor(Color.Green)
        .Expand();
}
```

This replaces `Markup.Escape(text)` with `MarkdownRenderer.Render(content)`, which returns an `IRenderable` that handles all markdown formatting. The `NonEmptyRenderable` wrapper inside MDView already protects against empty segment crashes in `LiveDisplay`.

- [ ] **Step 3: Remove unused using**

In `src/TrappedMindSharp/ConsoleRenderer.cs`, the `using System.Text` import for `StringBuilder` is still needed. The `using Spectre.Console.Rendering` import can be removed since `BuildAssistantPanel` now returns `Panel` (which is `IRenderable` but doesn't need the explicit import — `Panel` is in `Spectre.Console`). Check if it's still needed; if not, remove it.

- [ ] **Step 4: Verify build**

Run: `dotnet build TrappedMindSharp.slnx`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/TrappedMindSharp/
git commit -m "feat: integrate MDView.Renderer for markdown rendering in assistant panel"
```

## Chunk 3: Malformed Input Tests

### Task 5: Write malformed markdown resilience tests

**Files:**
- Create: `tests/TrappedMindSharp.Tests/MalformedMarkdownTests.cs`

These tests verify that `MarkdownRenderer.Render()` does not throw on malformed, incomplete, or adversarial input — the kind of content that arrives mid-stream from an LLM.

- [ ] **Step 1: Create MalformedMarkdownTests.cs**

Create `tests/TrappedMindSharp.Tests/MalformedMarkdownTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they all pass**

Run: `dotnet test TrappedMindSharp.slnx`
Expected: All tests pass. If any fail, the MDView.Renderer library has a bug to investigate.

- [ ] **Step 3: Commit**

```bash
git add tests/
git commit -m "test: add malformed markdown resilience tests"
```

### Task 6: Final cleanup and verify

- [ ] **Step 1: Verify full solution builds and tests pass**

Run: `dotnet build TrappedMindSharp.slnx && dotnet test TrappedMindSharp.slnx`
Expected: Build succeeded, all tests pass.

- [ ] **Step 2: Commit any remaining changes**

If there are any remaining changes, commit them. Otherwise skip.
