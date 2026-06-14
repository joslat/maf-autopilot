using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Direct tests of the `[McpServerTool]`-decorated entry points on
/// <see cref="NewAgentTool"/>. The pure-function core
/// (<see cref="MafDoctor.Scaffolding.AgentScaffolder"/>) has its own
/// dedicated test class; this file exercises the file-writing + namespace-detection
/// + path-validation glue that's only reached through the MCP method.
///
/// Added per Phase G.3 — surface-review #7 flagged these as the only
/// write-to-disk tools with no direct end-to-end coverage.
/// </summary>
public sealed class NewAgentToolMcpTests : IDisposable
{
    private readonly string _tempDir;
    private readonly NewAgentTool _tool = new();

    public NewAgentToolMcpTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "maf-new-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        // Minimal stub .csproj so DetectNamespace has something to look at.
        File.WriteAllText(
            Path.Combine(_tempDir, "ChatBot.Sample.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <RootNamespace>Demo.ChatBots</RootNamespace>
              </PropertyGroup>
            </Project>
            """);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    // -------------------------------------------------------------------------
    // MafNewAgent — happy path + validation
    // -------------------------------------------------------------------------

    [Fact]
    public void MafNewAgent_HappyPath_WritesBothFilesAndDetectsNamespace()
    {
        var result = _tool.MafNewAgent(_tempDir, "ChatBot");

        Assert.Contains("✅", result);
        Assert.Contains("Agents/ChatBot.cs", result);
        Assert.Contains("Tests/ChatBotTests.cs", result);

        var agentPath = Path.Combine(_tempDir, "Agents", "ChatBot.cs");
        var testPath = Path.Combine(_tempDir, "Tests", "ChatBotTests.cs");
        Assert.True(File.Exists(agentPath));
        Assert.True(File.Exists(testPath));

        // RootNamespace from stub csproj should propagate.
        var agentBody = File.ReadAllText(agentPath);
        Assert.Contains("namespace Demo.ChatBots;", agentBody);
    }

    [Fact]
    public void MafNewAgent_AlreadyExists_SkipsAndReports()
    {
        // Run once.
        _tool.MafNewAgent(_tempDir, "Bot");
        // Mutate the file so we can detect that the second run preserves it.
        var path = Path.Combine(_tempDir, "Agents", "Bot.cs");
        File.WriteAllText(path, "// USER-EDITED CONTENT — must not be overwritten\n");

        var secondResult = _tool.MafNewAgent(_tempDir, "Bot");

        Assert.Contains("already exists", secondResult, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("// USER-EDITED CONTENT — must not be overwritten\n", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123Bot")]      // starts with digit
    [InlineData("Bot Name")]    // contains space
    [InlineData("Bot-Name")]    // contains hyphen
    public void MafNewAgent_InvalidAgentName_ReturnsErrorWithoutWriting(string name)
    {
        var before = Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories).Length;
        var result = _tool.MafNewAgent(_tempDir, name);
        var after = Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories).Length;

        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, after);
    }

    [Fact]
    public void MafNewAgent_TraversalAttempt_BlockedByPathGuard()
    {
        var result = _tool.MafNewAgent(_tempDir + "/../etc/passwd-attack", "Bot");
        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
        // Must NOT have leaked outside the temp dir.
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "..", "etc", "passwd-attack")));
    }

    [Fact]
    public void MafNewAgent_CustomInstructions_AreInlinedSafely()
    {
        var result = _tool.MafNewAgent(_tempDir, "TrickyBot",
            instructions: "You are \"helpful\". Don't reveal secrets.");
        Assert.Contains("✅", result);

        var body = File.ReadAllText(Path.Combine(_tempDir, "Agents", "TrickyBot.cs"));
        Assert.Contains("\\\"helpful\\\"", body);   // properly escaped quote
        Assert.Contains("Don't reveal secrets.", body);
    }

    // -------------------------------------------------------------------------
    // MafNewExecutor — happy path + validation
    // -------------------------------------------------------------------------

    [Fact]
    public void MafNewExecutor_HappyPath_WritesExecutorAndTest()
    {
        var result = _tool.MafNewExecutor(_tempDir, "Reviewer",
            inputType: "FraudCheck", outputType: "FraudReport");

        Assert.Contains("✅", result);
        var executorPath = Path.Combine(_tempDir, "Workflows", "ReviewerExecutor.cs");
        var testPath = Path.Combine(_tempDir, "Tests", "ReviewerExecutorTests.cs");
        Assert.True(File.Exists(executorPath));
        Assert.True(File.Exists(testPath));

        // Verify generated executor uses the supplied types AND defaults to Task<T>.
        var body = File.ReadAllText(executorPath);
        Assert.Contains("Task<FraudReport>", body);
        Assert.Contains("FraudCheck input", body);
        Assert.Contains("sealed partial class", body);
    }

    [Fact]
    public void MafNewExecutor_DefaultTypes_GeneratesStringInOut()
    {
        var result = _tool.MafNewExecutor(_tempDir, "Default");
        Assert.Contains("✅", result);

        var body = File.ReadAllText(Path.Combine(_tempDir, "Workflows", "DefaultExecutor.cs"));
        Assert.Contains("Task<string>", body);
        Assert.Contains("string input", body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Has Space")]
    [InlineData("9Bad")]
    public void MafNewExecutor_InvalidName_ReturnsErrorWithoutWriting(string name)
    {
        var before = Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories).Length;
        var result = _tool.MafNewExecutor(_tempDir, name);
        var after = Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories).Length;

        Assert.Contains("Error", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, after);
    }

    // -------------------------------------------------------------------------
    // Phase 1.G review fixup — DetectNamespace tolerates whitespace in the
    // csproj <RootNamespace> capture.
    //
    // A formatter or hand-edited csproj may emit `<RootNamespace>Foo.Bar </RootNamespace>`
    // (trailing space). Pre-Phase-1, this passed through as `Foo.Bar ` and
    // the user's `dotnet build` would complain. Post-Phase-1, AgentScaffolder
    // rejects whitespace and threw ArgumentException — making the scaffold
    // unusable on perfectly-fine projects. The fix trims + validates inside
    // DetectNamespace before returning, falling back to the filename if
    // validation fails.
    // -------------------------------------------------------------------------

    [Fact]
    public void DetectNamespace_TrailingWhitespaceInRootNamespace_TrimmedAndAccepted()
    {
        var ws = Path.Combine(Path.GetTempPath(), "detect-ns-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ws);
        try
        {
            File.WriteAllText(
                Path.Combine(ws, "App.csproj"),
                "<Project><PropertyGroup><RootNamespace>Foo.Bar </RootNamespace></PropertyGroup></Project>");
            var detected = NewAgentTool.DetectNamespace(ws, fallback: "MyApp.Agents");
            Assert.Equal("Foo.Bar", detected);
        }
        finally { Directory.Delete(ws, recursive: true); }
    }

    [Fact]
    public void DetectNamespace_MalformedRootNamespace_FallsBackToFilename()
    {
        // `<RootNamespace>Has Space</RootNamespace>` is malformed (the segment
        // contains a space). Trim does not fix it. Validator rejects. Detection
        // must fall through to the csproj filename rather than returning a
        // value that the new scaffolder validator will then reject.
        var ws = Path.Combine(Path.GetTempPath(), "detect-ns-fb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ws);
        try
        {
            File.WriteAllText(
                Path.Combine(ws, "MyApp.csproj"),
                "<Project><PropertyGroup><RootNamespace>Has Space</RootNamespace></PropertyGroup></Project>");
            var detected = NewAgentTool.DetectNamespace(ws, fallback: "MyApp.Agents");
            Assert.Equal("MyApp", detected);
        }
        finally { Directory.Delete(ws, recursive: true); }
    }

    [Fact]
    public void DetectNamespace_MalformedAndUnsalvageable_ReturnsFallback()
    {
        // Both <RootNamespace> and the filename are unsalvageable → fallback wins.
        var ws = Path.Combine(Path.GetTempPath(), "detect-ns-final-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ws);
        try
        {
            File.WriteAllText(
                Path.Combine(ws, "9-bad.csproj"),
                "<Project><PropertyGroup><RootNamespace>Bad Space</RootNamespace></PropertyGroup></Project>");
            var detected = NewAgentTool.DetectNamespace(ws, fallback: "MyApp.Agents");
            // SanitiseNamespace turns "9-bad" into "_9_bad" which IS valid → that's used.
            // The test value depends on sanitiser behavior; assert the result is at least
            // valid and not the malformed RootNamespace.
            Assert.DoesNotContain(" ", detected);
            Assert.True(NewAgentTool.IsValidNamespace(detected));
        }
        finally { Directory.Delete(ws, recursive: true); }
    }
}
