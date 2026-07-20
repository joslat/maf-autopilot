using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

public class SourceFileWalkerTests
{
    // -------------------------------------------------------------------------
    // ScanBudget cap — #148 finding 2. A single repo walk must not be able to
    // run unbounded; verify the cap actually stops enumeration and reports
    // truncation rather than silently returning everything.
    // -------------------------------------------------------------------------

    [Fact]
    public void EnumerateCsFiles_UnderDefaultBudget_ReturnsEveryFile_NotTruncated()
    {
        var root = CreateTempRoot();
        try
        {
            for (var i = 0; i < 5; i++)
                File.WriteAllText(Path.Combine(root, $"File{i}.cs"), "// content");

            var budget = new SourceFileWalker.ScanBudget();
            var files = SourceFileWalker.EnumerateCsFiles(root, excludes: null, budget).ToList();

            Assert.Equal(5, files.Count);
            Assert.Equal(5, budget.FilesSeen);
            Assert.False(budget.Truncated);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void EnumerateCsFiles_OverBudget_StopsEarlyAndReportsTruncated()
    {
        var root = CreateTempRoot();
        try
        {
            for (var i = 0; i < 10; i++)
                File.WriteAllText(Path.Combine(root, $"File{i}.cs"), "// content");

            var budget = new SourceFileWalker.ScanBudget { MaxFiles = 3 };
            var files = SourceFileWalker.EnumerateCsFiles(root, excludes: null, budget).ToList();

            Assert.Equal(3, files.Count);
            Assert.Equal(3, budget.FilesSeen);
            Assert.True(budget.Truncated);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void EnumerateCsFiles_TwoArgOverload_AppliesDefaultBudgetSilently()
    {
        // The 2-arg overload (used by every scanning tool today) must still be
        // bounded even though callers don't pass a ScanBudget explicitly —
        // that's what protects existing call sites without touching them.
        var root = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "Solo.cs"), "// content");
            var files = SourceFileWalker.EnumerateCsFiles(root, excludes: null).ToList();
            Assert.Single(files);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ScanBudget_ExcludesAndCapInteract_ExcludedFilesDoNotCountAgainstBudget()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "samples"));
            File.WriteAllText(Path.Combine(root, "samples", "Bait.cs"), "// bait");
            File.WriteAllText(Path.Combine(root, "Real.cs"), "// real");

            var budget = new SourceFileWalker.ScanBudget { MaxFiles = 1 };
            var files = SourceFileWalker.EnumerateCsFiles(root, excludes: ["samples/"], budget).ToList();

            Assert.Single(files);
            Assert.EndsWith("Real.cs", files[0]);
            Assert.False(budget.Truncated);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    // -------------------------------------------------------------------------
    // Per-file size cap — lives in the shared ScanBudget (not per-tool) so
    // EVERY caller of EnumerateCsFiles (AntiPatternScannerTool, EstimateCostTool,
    // AutoFixTool, etc.), not just DoctorTool, is protected against a single
    // pathologically large file.
    // -------------------------------------------------------------------------

    [Fact]
    public void EnumerateCsFiles_OversizedFile_SkippedButWalkContinues()
    {
        var root = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "Small.cs"), "// x");
            File.WriteAllText(Path.Combine(root, "Big.cs"), new string('x', 100));

            var budget = new SourceFileWalker.ScanBudget { MaxFileBytes = 10 };
            var files = SourceFileWalker.EnumerateCsFiles(root, excludes: null, budget).ToList();

            Assert.Single(files);
            Assert.EndsWith("Small.cs", files[0]);
            Assert.Equal(1, budget.FilesSkippedOversized);
            Assert.False(budget.Truncated); // count cap never entered into it
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void EnumerateCsFiles_OversizedSkip_DoesNotCountAgainstFileCountCap()
    {
        var root = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "Big.cs"), new string('x', 100));
            File.WriteAllText(Path.Combine(root, "Small1.cs"), "// x");
            File.WriteAllText(Path.Combine(root, "Small2.cs"), "// x");

            var budget = new SourceFileWalker.ScanBudget { MaxFileBytes = 10, MaxFiles = 2 };
            var files = SourceFileWalker.EnumerateCsFiles(root, excludes: null, budget).ToList();

            // Both small files fit under MaxFiles=2 even though Big.cs was seen
            // first in enumeration order — the oversized skip must not consume
            // a slot in the file-count budget.
            Assert.Equal(2, files.Count);
            Assert.False(budget.Truncated);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourcefilewalker-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    // -------------------------------------------------------------------------
    // F-23 — MakeRelative used a bare StartsWith(rootFull) with no separator
    // boundary, so "/work/repo-evil/file.cs" incorrectly passed containment
    // against root "/work/repo" (prefix collision, not a real subdirectory).
    // -------------------------------------------------------------------------

    [Fact]
    public void MakeRelative_PrefixCollisionSibling_Throws()
    {
        var root = CreateTempRoot();
        var sibling = root + "-evil";
        Directory.CreateDirectory(sibling);
        var siblingFile = Path.Combine(sibling, "file.cs");
        File.WriteAllText(siblingFile, "// x");

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => SourceFileWalker.MakeRelative(root, siblingFile));
            Assert.Contains("outside the repository root", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(sibling, recursive: true);
        }
    }

    [Fact]
    public void MakeRelative_FileDirectlyUnderRoot_ReturnsRelativePath()
    {
        var root = CreateTempRoot();
        var file = Path.Combine(root, "sub", "Foo.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "// x");

        try
        {
            var relative = SourceFileWalker.MakeRelative(root, file);
            Assert.Equal("sub/Foo.cs", relative);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void MakeRelative_FileEqualsRoot_ReturnsEmptyString()
    {
        var root = CreateTempRoot();
        try
        {
            Assert.Equal(string.Empty, SourceFileWalker.MakeRelative(root, root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
