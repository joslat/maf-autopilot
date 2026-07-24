using MafDoctor;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for the CLI `maf-doctor new agent|executor` entry point
/// (<see cref="NewCommand"/>). This path bypasses <c>NewAgentTool</c> entirely —
/// it calls the scaffolder directly and writes files itself — so it needed its
/// own F-03 containment/symlink fix independent of the MCP tool's.
/// </summary>
public sealed class NewCommandTests : IDisposable
{
    private readonly string _tempDir;

    public NewCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "maf-new-command-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void Run_NewAgent_WritesFileUnderTargetPath()
    {
        var exitCode = NewCommand.Run(["new", "agent", "ChatBot", "--path", _tempDir]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(_tempDir, "Agents", "ChatBot.cs")));
    }

    [Fact]
    public void Run_NewAgent_AlreadyExists_SkipsWithoutOverwriting()
    {
        NewCommand.Run(["new", "agent", "Bot", "--path", _tempDir]);
        var path = Path.Combine(_tempDir, "Agents", "Bot.cs");
        File.WriteAllText(path, "// USER-EDITED — must not be overwritten\n");

        NewCommand.Run(["new", "agent", "Bot", "--path", _tempDir]);

        Assert.Equal("// USER-EDITED — must not be overwritten\n", File.ReadAllText(path));
    }

    // ---- WM-05: the advertised `new executor <Name> [In] [Out]` positionals work ----

    [Fact]
    public void PositionalsAfter_SkipsFlagsAndTheirValues()
    {
        var pos = NewCommand.PositionalsAfter(
            ["new", "executor", "Foo", "In", "--path", "/dir", "Out"],
            startIndex: 3, "--path", "--input", "--output");
        Assert.Equal(new[] { "In", "Out" }, pos);
    }

    [Fact]
    public void Run_NewExecutor_PositionalTypes_AreHonored()
    {
        var exitCode = NewCommand.Run(["new", "executor", "FraudReviewer", "FraudCheck", "FraudReport", "--path", _tempDir]);
        Assert.Equal(0, exitCode);
        var file = Path.Combine(_tempDir, "Workflows", "FraudReviewerExecutor.cs"); // scaffolder appends "Executor"
        Assert.True(File.Exists(file), file);
        var content = File.ReadAllText(file);
        Assert.Contains("FraudCheck", content);
        Assert.Contains("FraudReport", content);
    }

    [Fact]
    public void Run_NewExecutor_FlagsWinOverPositionals()
    {
        NewCommand.Run(["new", "executor", "Rev", "PosIn", "PosOut", "--input", "FlagIn", "--output", "FlagOut", "--path", _tempDir]);
        var content = File.ReadAllText(Path.Combine(_tempDir, "Workflows", "RevExecutor.cs"));
        Assert.Contains("FlagIn", content);
        Assert.Contains("FlagOut", content);
        Assert.DoesNotContain("PosIn", content);
    }

    // -------------------------------------------------------------------------
    // F-03 — WriteFiles previously wrote through a symlinked Agents/Workflows
    // directory (or a symlinked target file) with plain File.WriteAllText, no
    // containment or symlink check. Symlink creation on Windows requires admin
    // or Developer Mode — skip gracefully if neither is available.
    // -------------------------------------------------------------------------

    [Fact]
    public void Run_NewAgent_SymlinkedAgentsDirectory_RefusesWithoutFollowing()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "new-command-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var linkedAgentsDir = Path.Combine(_tempDir, "Agents");

        try
        {
            try { Directory.CreateSymbolicLink(linkedAgentsDir, outsideDir); }
            catch (UnauthorizedAccessException) { return; }
            catch (IOException) { return; }
            catch (PlatformNotSupportedException) { return; }

            NewCommand.Run(["new", "agent", "Bot", "--path", _tempDir]);
            Assert.False(File.Exists(Path.Combine(outsideDir, "Bot.cs")));
        }
        finally
        {
            try { Directory.Delete(linkedAgentsDir); } catch { /* best effort */ }
            try { Directory.Delete(outsideDir, recursive: true); } catch { /* best effort */ }
        }
    }

    // -------------------------------------------------------------------------
    // Round-2 review fixup — WriteFiles previously validated and wrote one file
    // at a time, so a symlinked Workflows/ dir could refuse the executor file
    // while the non-symlinked Tests/ dir still received the test file: a
    // confusing half-generated scaffold (a test referencing a class that was
    // never written). Mirrors NewAgentTool.WriteAndReport's pre-flight fix.
    // -------------------------------------------------------------------------

    [Fact]
    public void Run_NewExecutor_SymlinkedWorkflowsDirectory_WritesNeitherFile()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "new-command-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var linkedWorkflowsDir = Path.Combine(_tempDir, "Workflows");

        try
        {
            try { Directory.CreateSymbolicLink(linkedWorkflowsDir, outsideDir); }
            catch (UnauthorizedAccessException) { return; }
            catch (IOException) { return; }
            catch (PlatformNotSupportedException) { return; }

            NewCommand.Run(["new", "executor", "Reviewer", "--path", _tempDir]);

            Assert.False(File.Exists(Path.Combine(outsideDir, "ReviewerExecutor.cs")));
            // The all-or-nothing pre-flight must refuse the WHOLE scaffold, so the
            // non-symlinked Tests/ directory must not receive the test file either.
            Assert.False(File.Exists(Path.Combine(_tempDir, "Tests", "ReviewerExecutorTests.cs")));
        }
        finally
        {
            try { Directory.Delete(linkedWorkflowsDir); } catch { /* best effort */ }
            try { Directory.Delete(outsideDir, recursive: true); } catch { /* best effort */ }
        }
    }
}
