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
}
