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
    // Phase I.1 — hermetic parser tests for `git diff -z --name-only` output.
    // The shell-out boundary is excluded; the parser is the testable seam.
    // -------------------------------------------------------------------------

    [Fact]
    public void ParseGitDiffOutput_HandlesMixedFiles_FiltersToCsOnly()
    {
        // Arrange
        var stdout = "src/Foo.cs\0README.md\0src/Bar.cs\0.gitignore\0";

        // Act
        var result = PullRequestAuditTool.ParseGitDiffOutput(stdout);

        // Assert
        Assert.Equal(new[] { "src/Foo.cs", "src/Bar.cs" }, result);
    }

    [Fact]
    public void ParseGitDiffOutput_FilenameWithEmbeddedNewline_ParsedAsOneEntry()
    {
        // F-07 (round-2 review fixup) — the whole point of `-z`/NUL-delimited
        // parsing: a filename containing a literal newline (legal on POSIX
        // filesystems) must not fragment into two bogus paths the way a
        // newline-split parse would.
        var weirdName = "weird\nname.cs";
        var stdout = weirdName + "\0src/Bar.cs\0";

        var result = PullRequestAuditTool.ParseGitDiffOutput(stdout);

        Assert.Equal(new[] { weirdName, "src/Bar.cs" }, result);
    }

    [Fact]
    public void ParseGitDiffOutput_IgnoresBinAndObjPaths()
    {
        // Arrange
        var stdout = "src/Foo.cs\0src/bin/Debug/net9.0/Foo.cs\0src/obj/Release/Bar.cs\0src/Bar.cs\0";

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
        var stdout = path + "\0";

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
        // Verified-in-review fixup: when every changed file was actually
        // scanned, the count must not carry a misleading "N of M" qualifier.
        Assert.Contains("**Files scanned:** 1 changed", report, StringComparison.Ordinal);
        Assert.DoesNotContain(" of 1 changed", report, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildReport_PartialScan_FilesScannedCountReflectsActuallyScannedNotTotalChanged()
    {
        // Verified-in-review fixup: "Files scanned" used to always report
        // changedFiles.Count (the total diff size) even when the per-file cap
        // meant one of them was never actually read — self-contradictory
        // alongside a "Scan incomplete" warning. Two changed files, one over
        // the per-file cap: exactly 1 of 2 should be reported as scanned.
        using var repo = new TempRepo();
        var small = System.IO.Path.Combine(repo.Path, "Small.cs");
        File.WriteAllText(small, "class Small { }");
        var huge = System.IO.Path.Combine(repo.Path, "Huge.cs");
        using (var fs = new FileStream(huge, FileMode.Create))
        {
            fs.SetLength(PullRequestAuditTool.MaxFileBytes + 1);
        }

        var report = PullRequestAuditTool.BuildReport(repo.Path, "main", ["Small.cs", "Huge.cs"]);

        Assert.Contains("Scan incomplete", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("**Files scanned:** 1 of 2 changed", report, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildReport_SymlinkedChangedFile_SkipsRefusesAndReportsIncomplete()
    {
        using var repo = new TempRepo();
        var outsideTarget = System.IO.Path.Combine(Path.GetTempPath(), "pr-audit-target-" + Guid.NewGuid().ToString("N") + ".cs");
        File.WriteAllText(outsideTarget, "class Secret { object F() => new DefaultAzureCredential(); }");
        var symlinkPath = System.IO.Path.Combine(repo.Path, "Linked.cs");

        try
        {
            try { File.CreateSymbolicLink(symlinkPath, outsideTarget); }
            catch (UnauthorizedAccessException) { return; }
            catch (IOException) { return; }
            catch (PlatformNotSupportedException) { return; }

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

    // Round-3 review fixup — the two tests above only prove the caps reject
    // input ONE unit over; each production check is a strict `>` (allow
    // at-cap), so a positive-side pin is needed too: exactly-at-cap input
    // must still be processed. An accidental flip to `>=` would silently
    // ship without this.
    [Fact]
    public void BuildReport_FileExactlyAtPerFileCap_NotSkippedByPerFileCheck()
    {
        // MaxFileBytes (10 MB) is larger than MaxAggregateBytes (5 MB), so a
        // single file at exactly the per-file cap will always also trip the
        // (separate, correctly-firing) aggregate cap — there's no way to
        // observe "scanned to completion" for a file this size in isolation.
        // What IS observable, and is the actual point of this boundary test:
        // the per-file check's own skip message must NOT fire for this file
        // (only the aggregate one may) — pinning `>` vs `>=` on MaxFileBytes
        // specifically, independent of the smaller aggregate cap.
        using var repo = new TempRepo();
        var file = System.IO.Path.Combine(repo.Path, "AtCap.cs");
        using (var fs = new FileStream(file, FileMode.Create))
        {
            fs.SetLength(PullRequestAuditTool.MaxFileBytes);
        }

        var report = PullRequestAuditTool.BuildReport(repo.Path, "main", ["AtCap.cs"]);

        Assert.DoesNotContain("per-file cap", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aggregate cap", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildReport_ChangedFileCountExactlyAtCap_AllScanned()
    {
        using var repo = new TempRepo();
        var changed = new List<string>();
        for (var i = 0; i < PullRequestAuditTool.MaxChangedFiles; i++)
        {
            var name = $"File{i}.cs";
            File.WriteAllText(System.IO.Path.Combine(repo.Path, name), "class C { }");
            changed.Add(name);
        }

        var report = PullRequestAuditTool.BuildReport(repo.Path, "main", changed);

        Assert.DoesNotContain("Scan incomplete", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"**Files scanned:** {PullRequestAuditTool.MaxChangedFiles} changed", report);
    }

    [Fact]
    public void BuildReport_AggregateBytesExceedCap_StopsScanningAndReportsIncomplete()
    {
        // MaxAggregateBytes was the one cap in this tool with zero test
        // coverage of any kind (not even an over-the-cap test existed).
        using var repo = new TempRepo();
        var twoMb = 2L * 1024 * 1024; // each individually well under MaxFileBytes (10 MB)
        var names = new[] { "A.cs", "B.cs", "C.cs" }; // 3 x 2 MB = 6 MB > MaxAggregateBytes (5 MB)
        foreach (var name in names)
        {
            using var fs = new FileStream(System.IO.Path.Combine(repo.Path, name), FileMode.Create);
            fs.SetLength(twoMb);
        }

        var report = PullRequestAuditTool.BuildReport(repo.Path, "main", names.ToList());

        Assert.Contains("Scan incomplete", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aggregate cap", report, StringComparison.OrdinalIgnoreCase);
        // A (2 MB) and B (2 MB) fit within the 5 MB aggregate cap; C would
        // push it to 6 MB and is the one that trips the break.
        Assert.Contains("**Files scanned:** 2 of 3 changed", report);
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
