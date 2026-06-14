using MafDoctor.Data;
using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for the pure parser core of Cs0618HuntTool. The shell-out to `dotnet build`
/// is intentionally NOT tested here — that's integration territory. The parser is
/// where regressions hide.
/// </summary>
public class Cs0618HuntToolTests
{
    [Fact]
    public void ParseBuildOutput_Empty_ReturnsEmpty()
    {
        Assert.Empty(Cs0618HuntTool.ParseBuildOutput(""));
        Assert.Empty(Cs0618HuntTool.ParseBuildOutput("   \n  \n  "));
    }

    [Fact]
    public void ParseBuildOutput_NoDiagnostics_ReturnsEmpty()
    {
        const string output = """
            Microsoft (R) Build Engine version 17.0.0
              Determining projects to restore...
              Restored.
              Build succeeded.
                0 Warning(s)
                0 Error(s)
            """;
        Assert.Empty(Cs0618HuntTool.ParseBuildOutput(output));
    }

    [Fact]
    public void ParseBuildOutput_OneCs0618_Warning_IsExtracted()
    {
        const string output =
            """
            C:\repo\src\Workflow.cs(42,17): warning CS0618: 'WorkflowBuilder.AddFanInBarrierEdge(ExecutorBinding, params ExecutorBinding[])' is obsolete: 'Use the (sources, target) overload instead.' [C:\repo\src\MyProj.csproj]
            """;

        var findings = Cs0618HuntTool.ParseBuildOutput(output);

        var d = Assert.Single(findings);
        Assert.Equal("CS0618", d.Code);
        Assert.Equal("warning", d.Severity);
        Assert.Equal(42, d.Line);
        Assert.Contains("AddFanInBarrierEdge", d.Message);
    }

    [Fact]
    public void ParseBuildOutput_Cs0246_Error_IsExtracted()
    {
        const string output =
            """
            /repo/src/Executor.cs(15,6): error CS0246: The type or namespace name 'StreamsMessageAttribute' could not be found
            """;

        var findings = Cs0618HuntTool.ParseBuildOutput(output);

        var d = Assert.Single(findings);
        Assert.Equal("CS0246", d.Code);
        Assert.Equal("error", d.Severity);
        Assert.Contains("StreamsMessageAttribute", d.Message);
    }

    [Fact]
    public void ParseBuildOutput_OtherCsCodes_AreIgnored()
    {
        // Only CS0618 and CS0246 are scope. Other codes (CS8600 nullable, CS1591 missing-XML) skipped.
        const string output = """
            /repo/src/A.cs(1,1): warning CS8600: Possible null reference
            /repo/src/B.cs(2,2): warning CS1591: Missing XML comment
            /repo/src/C.cs(3,3): warning CS0618: 'X' is obsolete: 'Use Y'
            """;

        var findings = Cs0618HuntTool.ParseBuildOutput(output);
        var d = Assert.Single(findings);
        Assert.Equal("CS0618", d.Code);
    }

    [Fact]
    public void ParseBuildOutput_DuplicatesAcrossTfms_AreDeduplicated()
    {
        // MSBuild emits the same diagnostic once per target framework.
        const string output = """
            /repo/src/A.cs(10,1): warning CS0618: 'X' is obsolete: 'Use Y'
            /repo/src/A.cs(10,1): warning CS0618: 'X' is obsolete: 'Use Y'
            /repo/src/A.cs(10,1): warning CS0618: 'X' is obsolete: 'Use Y'
            """;

        var findings = Cs0618HuntTool.ParseBuildOutput(output);
        Assert.Single(findings);
    }

    [Fact]
    public void ParseBuildOutput_MultipleDistinctDiagnostics_AreAllExtracted()
    {
        const string output = """
            /repo/A.cs(5,1): warning CS0618: 'A' is obsolete
            /repo/B.cs(6,1): warning CS0618: 'B' is obsolete
            /repo/C.cs(7,1): error CS0246: type 'C' not found
            """;

        var findings = Cs0618HuntTool.ParseBuildOutput(output).ToList();
        Assert.Equal(3, findings.Count);
    }

    [Fact]
    public void MatchToRegistry_KnownObsoleteSignature_FindsRegistryEntry()
    {
        var registry = new RegistryService();
        var diag = new BuildDiagnostic(
            File: "/repo/src/Workflow.cs",
            Line: 42,
            Severity: "warning",
            Code: "CS0618",
            Message: "'WorkflowBuilder.AddFanInBarrierEdge(ExecutorBinding, params ExecutorBinding[])' is obsolete");

        var match = Cs0618HuntTool.MatchToRegistry(diag, registry);

        Assert.NotNull(match);
        Assert.Contains("FAN-IN", match!.Id, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MatchToRegistry_AgentThreadRemoval_FindsThreadEntry()
    {
        var registry = new RegistryService();
        var diag = new BuildDiagnostic(
            File: "/repo/src/Bot.cs",
            Line: 12,
            Severity: "error",
            Code: "CS0246",
            Message: "The type or namespace name 'AgentThread' could not be found");

        var match = Cs0618HuntTool.MatchToRegistry(diag, registry);
        Assert.NotNull(match);
        Assert.Contains("THREAD", match!.Id, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mcp_EmptyPath_ReturnsErrorMessage()
    {
        var tool = new Cs0618HuntTool(new RegistryService());
        var result = tool.MafRunCs0618Hunt("");
        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mcp_NonexistentPath_ReturnsErrorMessage()
    {
        var tool = new Cs0618HuntTool(new RegistryService());
        var result = tool.MafRunCs0618Hunt("/path/that/definitely/does/not/exist");
        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Phase I.2 — ResolveBuildTarget. The path-resolution logic that runs BEFORE
    // `dotnet build` is shelled. If this regresses, the user sees "could not
    // find a .csproj or .sln" even when one exists. Hermetic — uses a single
    // throwaway temp dir, no shell-outs.
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveBuildTarget_NonexistentPath_ReturnsNull()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "definitely-not-a-real-path-" + Guid.NewGuid().ToString("N"));

        // Act
        var resolved = Cs0618HuntTool.ResolveBuildTarget(path);

        // Assert
        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveBuildTarget_FileWithUnsupportedExtension_ReturnsNull()
    {
        // Arrange — a .txt file that EXISTS on disk should not be accepted.
        using var fixture = new TempProjectDir();
        var notAProject = Path.Combine(fixture.Path, "notes.txt");
        File.WriteAllText(notAProject, "not a build target");

        // Act
        var resolved = Cs0618HuntTool.ResolveBuildTarget(notAProject);

        // Assert
        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveBuildTarget_DirectoryWithCsproj_ReturnsThatCsproj()
    {
        // Arrange — directory containing a single .csproj file.
        using var fixture = new TempProjectDir();
        var csprojPath = Path.Combine(fixture.Path, "Sample.csproj");
        File.WriteAllText(csprojPath, "<Project />");

        // Act
        var resolved = Cs0618HuntTool.ResolveBuildTarget(fixture.Path);

        // Assert
        Assert.Equal(csprojPath, resolved);
    }

    [Fact]
    public void ResolveBuildTarget_DirectoryWithBothSlnAndCsproj_PrefersSln()
    {
        // Arrange — when both exist, .sln wins (covers solution-level builds).
        using var fixture = new TempProjectDir();
        var slnPath = Path.Combine(fixture.Path, "Sample.sln");
        var csprojPath = Path.Combine(fixture.Path, "Sample.csproj");
        File.WriteAllText(slnPath, "Microsoft Visual Studio Solution File");
        File.WriteAllText(csprojPath, "<Project />");

        // Act
        var resolved = Cs0618HuntTool.ResolveBuildTarget(fixture.Path);

        // Assert
        Assert.Equal(slnPath, resolved);
    }

    [Fact]
    public void ResolveBuildTarget_ExplicitCsprojFile_ReturnsItDirectly()
    {
        // Arrange — passing the .csproj path directly, not a directory.
        using var fixture = new TempProjectDir();
        var csprojPath = Path.Combine(fixture.Path, "Direct.csproj");
        File.WriteAllText(csprojPath, "<Project />");

        // Act
        var resolved = Cs0618HuntTool.ResolveBuildTarget(csprojPath);

        // Assert
        Assert.Equal(csprojPath, resolved);
    }

    /// <summary>
    /// Throwaway temp directory; auto-cleaned via IDisposable. Guid-suffixed so
    /// parallel test runs don't collide. Cleanup is best-effort — Windows file
    /// locks during test teardown should never fail a test.
    /// </summary>
    private sealed class TempProjectDir : IDisposable
    {
        public string Path { get; }
        public TempProjectDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "maf-cs0618-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
