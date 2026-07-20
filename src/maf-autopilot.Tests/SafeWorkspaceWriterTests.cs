using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for the shared atomic-write primitive introduced to close F-01/F-02/F-03/
/// F-22/F-24 (all five findings shared the same root cause: writing through a
/// predictable temp path or a symlinked destination). Symlink creation on Windows
/// requires admin or Developer Mode — those cases skip gracefully if neither is
/// available, matching PathGuardTests' convention.
/// </summary>
public sealed class SafeWorkspaceWriterTests : IDisposable
{
    private readonly string _root;

    public SafeWorkspaceWriterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "safe-writer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void WriteAtomic_NewFile_WritesContentAndCreatesParentDirs()
    {
        var dest = Path.Combine(_root, "nested", "dir", "file.txt");

        SafeWorkspaceWriter.WriteAtomic(_root, dest, "hello");

        Assert.Equal("hello", File.ReadAllText(dest));
    }

    [Fact]
    public void WriteAtomic_ExistingFile_OverwritesContent()
    {
        var dest = Path.Combine(_root, "file.txt");
        File.WriteAllText(dest, "old");

        SafeWorkspaceWriter.WriteAtomic(_root, dest, "new");

        Assert.Equal("new", File.ReadAllText(dest));
    }

    [Fact]
    public void WriteAtomic_LeavesNoTempFileBehind()
    {
        var dest = Path.Combine(_root, "file.txt");

        SafeWorkspaceWriter.WriteAtomic(_root, dest, "hello");

        var leftovers = Directory.GetFiles(_root, "*.mafdoctor.tmp", SearchOption.AllDirectories);
        Assert.Empty(leftovers);
    }

    [Fact]
    public void WriteAtomic_PathEscapesRoot_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => SafeWorkspaceWriter.WriteAtomic(_root, Path.Combine(_root, "..", "escaped.txt"), "x"));
        Assert.Contains("..", ex.Message);
    }

    [Fact]
    public void WriteAtomic_SymlinkedLeaf_RefusesWithoutFollowing()
    {
        var outsideTarget = Path.Combine(Path.GetTempPath(), "safe-writer-target-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(outsideTarget, "SECRET");
        var symlinkPath = Path.Combine(_root, "safe.txt");

        try { File.CreateSymbolicLink(symlinkPath, outsideTarget); }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }
        catch (PlatformNotSupportedException) { return; }

        try
        {
            var ex = Assert.Throws<ArgumentException>(
                () => SafeWorkspaceWriter.WriteAtomic(_root, symlinkPath, "attacker-controlled"));
            Assert.Contains("symlink", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("SECRET", File.ReadAllText(outsideTarget));
        }
        finally
        {
            try { File.Delete(symlinkPath); } catch { /* best effort */ }
            try { File.Delete(outsideTarget); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void WriteAtomic_SymlinkedParentDirectory_RefusesWithoutFollowing()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "safe-writer-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var linkedDir = Path.Combine(_root, "linked");

        try { Directory.CreateSymbolicLink(linkedDir, outsideDir); }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }
        catch (PlatformNotSupportedException) { return; }

        try
        {
            var dest = Path.Combine(linkedDir, "file.txt");
            var ex = Assert.Throws<ArgumentException>(
                () => SafeWorkspaceWriter.WriteAtomic(_root, dest, "attacker-controlled"));
            Assert.Contains("symlink", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(outsideDir, "file.txt")));
        }
        finally
        {
            try { Directory.Delete(linkedDir); } catch { /* best effort */ }
            try { Directory.Delete(outsideDir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void TryCreateNew_NewFile_CreatesAndReturnsTrue()
    {
        var dest = Path.Combine(_root, "sub", "New.cs");

        var created = SafeWorkspaceWriter.TryCreateNew(_root, dest, "content");

        Assert.True(created);
        Assert.Equal("content", File.ReadAllText(dest));
    }

    [Fact]
    public void TryCreateNew_ExistingFile_SkipsWithoutOverwriting()
    {
        var dest = Path.Combine(_root, "Existing.cs");
        File.WriteAllText(dest, "original");

        var created = SafeWorkspaceWriter.TryCreateNew(_root, dest, "should-not-overwrite");

        Assert.False(created);
        Assert.Equal("original", File.ReadAllText(dest));
    }

    [Fact]
    public void TryCreateNew_SymlinkedTargetFile_RefusesWithoutFollowing()
    {
        var outsideTarget = Path.Combine(Path.GetTempPath(), "safe-writer-target-" + Guid.NewGuid().ToString("N") + ".cs");
        File.WriteAllText(outsideTarget, "SECRET");
        var symlinkPath = Path.Combine(_root, "Agent.cs");

        try { File.CreateSymbolicLink(symlinkPath, outsideTarget); }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }
        catch (PlatformNotSupportedException) { return; }

        try
        {
            var ex = Assert.Throws<ArgumentException>(
                () => SafeWorkspaceWriter.TryCreateNew(_root, symlinkPath, "attacker-controlled"));
            Assert.Contains("symlink", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("SECRET", File.ReadAllText(outsideTarget));
        }
        finally
        {
            try { File.Delete(symlinkPath); } catch { /* best effort */ }
            try { File.Delete(outsideTarget); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void TryCreateNew_SymlinkedParentDirectory_RefusesWithoutFollowing()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "safe-writer-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var linkedDir = Path.Combine(_root, "Tests");

        try { Directory.CreateSymbolicLink(linkedDir, outsideDir); }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }
        catch (PlatformNotSupportedException) { return; }

        try
        {
            var dest = Path.Combine(linkedDir, "AgentTests.cs");
            Assert.Throws<ArgumentException>(
                () => SafeWorkspaceWriter.TryCreateNew(_root, dest, "attacker-controlled"));
            Assert.False(File.Exists(Path.Combine(outsideDir, "AgentTests.cs")));
        }
        finally
        {
            try { Directory.Delete(linkedDir); } catch { /* best effort */ }
            try { Directory.Delete(outsideDir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void ConcurrentWriteAtomic_SameFile_DoesNotCorrupt()
    {
        // Windows/NTFS can transiently throw UnauthorizedAccessException when two
        // File.Move calls race onto the identical destination (unlike POSIX
        // rename(), it isn't guaranteed to cleanly serialize) — that's a
        // contention error, not corruption. The property that actually matters
        // (and is what "one succeeds safely or both serialize without
        // corrupting data" means): whichever write DOES land is one writer's
        // complete, un-torn content — never an interleaved/truncated mix.
        var dest = Path.Combine(_root, "concurrent.txt");
        var contents = Enumerable.Range(0, 8).Select(i => new string((char)('a' + i), 10_000)).ToArray();

        Parallel.For(0, contents.Length, i =>
        {
            try { SafeWorkspaceWriter.WriteAtomic(_root, dest, contents[i]); }
            catch (IOException) { /* lost the race to another concurrent writer */ }
            catch (UnauthorizedAccessException) { /* lost the race to another concurrent writer */ }
        });

        var final = File.ReadAllText(dest);
        Assert.Contains(final, contents);
    }
}
