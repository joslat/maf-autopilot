using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

public class PathGuardTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyOrWhitespace_Rejected(string? path)
    {
        var err = PathGuard.ValidateRepoPath(path);
        Assert.NotNull(err);
        Assert.Contains("must not be empty", err, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("/var/repo/../../etc/passwd")]
    [InlineData("C:\\repo\\..\\..\\Windows\\System32")]
    [InlineData("repo/sub/../escape")]
    public void TraversalSegments_Rejected(string path)
    {
        var err = PathGuard.ValidateRepoPath(path);
        Assert.NotNull(err);
        Assert.Contains("traversal", err, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/path\"with quote")]
    [InlineData("/path; rm -rf /")]
    [InlineData("/path | cat")]
    [InlineData("/path && evil")]
    [InlineData("/path\nnewline")]
    public void ShellMetacharacters_Rejected(string path)
    {
        var err = PathGuard.ValidateRepoPath(path);
        Assert.NotNull(err);
        Assert.Contains("invalid characters", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonexistentDirectory_Rejected()
    {
        var err = PathGuard.ValidateRepoPath("/definitely/does/not/exist/anywhere");
        Assert.NotNull(err);
        Assert.Contains("does not exist", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingDirectory_Accepted()
    {
        var err = PathGuard.ValidateRepoPath(Path.GetTempPath());
        Assert.Null(err);
    }

    [Fact]
    public void ParameterName_AppearsInErrorMessage()
    {
        var err = PathGuard.ValidateRepoPath("", parameterName: "projectPath");
        Assert.NotNull(err);
        Assert.Contains("projectPath", err);
    }
}
