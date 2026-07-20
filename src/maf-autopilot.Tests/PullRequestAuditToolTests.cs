using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

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

    // -------------------------------------------------------------------------
    // F-07 — BuildReport previously combined repoPath + relPath with plain
    // Path.Combine and read it with File.ReadAllText: no containment check (a
    // symlinked changed-file entry could read outside the repo) and no per-file/
    // aggregate/count cap. BuildReport is internal specifically so these can be
    // exercised without a real git subprocess for every case.
    // -------------------------------------------------------------------------

    private sealed class TempRepo : IDisposable
    {
        public string Path { get; }
        public TempRepo()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pr-audit-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void BuildReport_NormalSmallFile_ScansCleanlyAndReportsComplete()
    {
        using var repo = new TempRepo();
        var file = System.IO.Path.Combine(repo.Path, "Clean.cs");
        File.WriteAllText(file, "class Clean { }");

        var report = PullRequestAuditTool.BuildReport(repo.Path, "main", ["Clean.cs"]);

        Assert.DoesNotContain("Scan incomplete", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clean", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildReport_SymlinkedChangedFile_SkipsRefusesAndReportsIncomplete()
    {
        using var repo = new TempRepo();
        var outsideTarget = System.IO.Path.Combine(Path.GetTempPath(), "pr-audit-target-" + Guid.NewGuid().ToString("N") + ".cs");
        File.WriteAllText(outsideTarget, "class Secret { object F() => new DefaultAzureCredential(); }");
        var symlinkPath = System.IO.Path.Combine(repo.Path, "Linked.cs");

        try { File.CreateSymbolicLink(symlinkPath, outsideTarget); }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }
        catch (PlatformNotSupportedException) { return; }

        try
        {
            var report = PullRequestAuditTool.BuildReport(repo.Path, "main", ["Linked.cs"]);

            Assert.Contains("Scan incomplete", report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("refused", report, StringComparison.OrdinalIgnoreCase);
            // The symlinked file's content must never have been read into the report.
            Assert.DoesNotContain("DefaultAzureCredential", report);
        }
        finally
        {
            try { File.Delete(symlinkPath); } catch { /* best effort */ }
            try { File.Delete(outsideTarget); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void BuildReport_FileOverPerFileCap_SkippedAndReportsIncomplete()
    {
        using var repo = new TempRepo();
        var file = System.IO.Path.Combine(repo.Path, "Huge.cs");
        // Exceeds MaxFileBytes (10 MB) without actually allocating 10 MB+ of
        // content on disk for the test — WriteAllText + a byte count check is
        // what BuildReport looks at, not the actual scan, so a sparse-ish file
        // is fine as long as FileInfo.Length reports it correctly.
        using (var fs = new FileStream(file, FileMode.Create))
        {
            fs.SetLength(PullRequestAuditTool.MaxFileBytes + 1);
        }

        var report = PullRequestAuditTool.BuildReport(repo.Path, "main", ["Huge.cs"]);

        Assert.Contains("Scan incomplete", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("per-file cap", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildReport_MoreChangedFilesThanCountCap_TruncatesAndReportsIncomplete()
    {
        using var repo = new TempRepo();
        var changed = new List<string>();
        for (var i = 0; i < PullRequestAuditTool.MaxChangedFiles + 5; i++)
        {
            var name = $"File{i}.cs";
            File.WriteAllText(System.IO.Path.Combine(repo.Path, name), "class C { }");
            changed.Add(name);
        }

        var report = PullRequestAuditTool.BuildReport(repo.Path, "main", changed);

        Assert.Contains("Scan incomplete", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{PullRequestAuditTool.MaxChangedFiles}-file audit cap", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildReport_DeletedFileInDiff_SkippedWithoutError()
    {
        using var repo = new TempRepo();

        var report = PullRequestAuditTool.BuildReport(repo.Path, "main", ["Deleted.cs"]);

        // A file listed in the diff but absent on disk (deleted in this branch)
        // must not be treated as a containment refusal or a scan failure.
        Assert.DoesNotContain("Scan incomplete", report, StringComparison.OrdinalIgnoreCase);
    }
}
