using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

public class PullRequestAuditToolTests
{
    [Fact]
    public void MafAuditPullRequest_EmptyPath_ReturnsError()
    {
        var tool = new PullRequestAuditTool();
        Assert.Contains("Error", tool.MafAuditPullRequest(""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MafAuditPullRequest_NonexistentDirectory_ReturnsError()
    {
        var tool = new PullRequestAuditTool();
        Assert.Contains("Error", tool.MafAuditPullRequest("/path/does/not/exist"),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("develop")]
    [InlineData("feature/x-y")]
    [InlineData("release/1.3.0")]
    [InlineData("HEAD~5")]
    public void IsSafeBranchName_ValidNames_Accepted(string branch) =>
        Assert.True(PullRequestAuditTool.IsSafeBranchName(branch));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("main; rm -rf /")]
    [InlineData("main && evil")]
    [InlineData("main | cat")]
    [InlineData("main\necho pwned")]
    [InlineData("main\"quote")]
    [InlineData("branch with space")]
    public void IsSafeBranchName_RejectsShellMetachars(string branch) =>
        Assert.False(PullRequestAuditTool.IsSafeBranchName(branch));

    [Fact]
    public void MafAuditPullRequest_InvalidBranch_ReturnsError()
    {
        var tool = new PullRequestAuditTool();
        // Use a real existing directory so we exercise the branch-validation path,
        // not the directory-existence check.
        var result = tool.MafAuditPullRequest(Path.GetTempPath(), "evil; rm -rf /");
        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid characters", result, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Phase I.1 — hermetic parser tests for `git diff --name-only` output.
    // The shell-out boundary is excluded; the parser is the testable seam.
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseGitDiffOutput_HandlesMixedFiles_FiltersToCsOnly()
    {
        // Arrange
        var stdout = "src/Foo.cs\nREADME.md\nsrc/Bar.cs\n.gitignore\n";

        // Act
        var result = PullRequestAuditTool.ParseGitDiffOutput(stdout);

        // Assert
        Assert.Equal(new[] { "src/Foo.cs", "src/Bar.cs" }, result);
    }

    [Fact]
    public void ParseGitDiffOutput_StripsCarriageReturns_OnWindowsCheckouts()
    {
        // Arrange — git on Windows emits CRLF.
        var stdout = "src/Foo.cs\r\nsrc/Bar.cs\r\n";

        // Act
        var result = PullRequestAuditTool.ParseGitDiffOutput(stdout);

        // Assert
        Assert.All(result, line => Assert.DoesNotContain('\r', line));
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseGitDiffOutput_IgnoresBinAndObjPaths()
    {
        // Arrange
        var stdout = "src/Foo.cs\nsrc/bin/Debug/net9.0/Foo.cs\nsrc/obj/Release/Bar.cs\nsrc/Bar.cs\n";

        // Act
        var result = PullRequestAuditTool.ParseGitDiffOutput(stdout);

        // Assert
        Assert.Equal(new[] { "src/Foo.cs", "src/Bar.cs" }, result);
    }

    [Fact]
    public void ParseGitDiffOutput_EmptyStdout_ReturnsEmpty()
    {
        // Arrange
        var stdout = "";

        // Act
        var result = PullRequestAuditTool.ParseGitDiffOutput(stdout);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("Foo.CS")]                          // uppercase extension
    [InlineData("deeply/nested/path/Bar.cs")]       // multi-segment path
    [InlineData("Foo.Tests/SomeTest.cs")]           // tests dir is NOT filtered here (scanner decides)
    public void ParseGitDiffOutput_AcceptsValidCsPaths(string path)
    {
        // Arrange
        var stdout = path + "\n";

        // Act
        var result = PullRequestAuditTool.ParseGitDiffOutput(stdout);

        // Assert
        Assert.Single(result);
    }
}
