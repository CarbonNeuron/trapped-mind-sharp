using TrappedMindSharp;

namespace TrappedMindSharp.Tests;

public class SandboxToolsTests
{
    [Theory]
    [InlineData("file.txt", "/workspace/file.txt")]
    [InlineData("src/main.py", "/workspace/src/main.py")]
    [InlineData("/workspace/file.txt", "/workspace/file.txt")]
    [InlineData("./file.txt", "/workspace/file.txt")]
    [InlineData("dir/./file.txt", "/workspace/dir/file.txt")]
    public void ResolvePath_ValidPaths_ResolvesCorrectly(string input, string expected)
    {
        var result = SandboxTools.ResolvePath(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("../../root")]
    [InlineData("/etc/passwd")]
    [InlineData("/tmp/file")]
    [InlineData("dir/../../etc/shadow")]
    public void ResolvePath_EscapePaths_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => SandboxTools.ResolvePath(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolvePath_EmptyPaths_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => SandboxTools.ResolvePath(input));
    }
}
