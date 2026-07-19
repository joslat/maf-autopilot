using System.Text.Json.Nodes;
using MafDoctor;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for `maf-doctor init` — specifically the .vscode/mcp.json merge logic.
///
/// The clobber-prevention scenario (malformed JSON must back up, not silently destroy)
/// was identified as plan task #36 during the 2026-05-11 project review. The fix:
/// on parse failure, copy the existing file to `mcp.json.bak.&lt;timestamp&gt;` before
/// proceeding, so other servers' configuration can be recovered manually.
/// </summary>
public sealed class InitCommandTests : IDisposable
{
    private readonly string _tempDir;

    public InitCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "maf-doctor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    // -------------------------------------------------------------------------
    // Happy paths
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WriteMcpJsonAsync_NoExistingFile_CreatesWithMafDoctorEntry()
    {
        await InitCommand.WriteMcpJsonAsync(_tempDir);

        var mcpJsonPath = Path.Combine(_tempDir, ".vscode", "mcp.json");
        Assert.True(File.Exists(mcpJsonPath));

        var root = JsonNode.Parse(await File.ReadAllTextAsync(mcpJsonPath))!.AsObject();
        var servers = root["servers"]!.AsObject();
        Assert.True(servers.ContainsKey("maf-doctor"));
        Assert.Equal("stdio", servers["maf-doctor"]!["type"]!.GetValue<string>());
        // Brand key is maf-doctor; the command stays the frozen binary maf-doctor.
        Assert.Equal("maf-doctor", servers["maf-doctor"]!["command"]!.GetValue<string>());
        Assert.Equal(InitCommand.CurrentVersion,
          servers["maf-doctor"]!["env"]![InitCommand.InitVersionEnvName]!.GetValue<string>());
    }

    [Fact]
    public async Task WriteMcpJsonAsync_AlreadyConfigured_IsIdempotent()
    {
        await InitCommand.WriteMcpJsonAsync(_tempDir);
        var mcpJsonPath = Path.Combine(_tempDir, ".vscode", "mcp.json");
        var firstWrite = await File.ReadAllTextAsync(mcpJsonPath);

        await InitCommand.WriteMcpJsonAsync(_tempDir);
        var secondWrite = await File.ReadAllTextAsync(mcpJsonPath);

        Assert.Equal(firstWrite, secondWrite);
    }

    [Fact]
    public async Task WriteMcpJsonAsync_PreservesUnrelatedServers_OnValidExistingJson()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        var mcpJsonPath = Path.Combine(vscodeDir, "mcp.json");
        await File.WriteAllTextAsync(mcpJsonPath, """
            {
              "servers": {
                "my-other-server": {
                  "type": "stdio",
                  "command": "node",
                  "args": ["/some/script.js"]
                }
              }
            }
            """);

        await InitCommand.WriteMcpJsonAsync(_tempDir);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(mcpJsonPath))!.AsObject();
        var servers = root["servers"]!.AsObject();
        Assert.True(servers.ContainsKey("my-other-server"),
            "Existing server entries must be preserved.");
        Assert.True(servers.ContainsKey("maf-doctor"),
            "maf-doctor entry must be added.");
    }

    // -------------------------------------------------------------------------
    // Plan #36 — JSON-clobber bug: malformed mcp.json must NOT silently destroy data
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WriteMcpJsonAsync_TrulyMalformedJson_BacksUpOriginalBeforeOverwriting()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        var mcpJsonPath = Path.Combine(vscodeDir, "mcp.json");
        const string malformedOriginal = """
            {
              "servers": {
                "my-precious-server": { "type": "stdio", "command": "node"
              ← unclosed structure, no recovery possible
            """;
        await File.WriteAllTextAsync(mcpJsonPath, malformedOriginal);

        await InitCommand.WriteMcpJsonAsync(_tempDir);

        // The original content must survive somewhere — a .bak.* file next to the original.
        var backups = Directory.GetFiles(vscodeDir, "mcp.json.bak.*");
        Assert.NotEmpty(backups);
        var backupContent = await File.ReadAllTextAsync(backups[0]);
        Assert.Equal(malformedOriginal, backupContent);
    }

    /// <summary>
    /// VS Code's mcp.json officially supports JSONC (// comments + trailing commas).
    /// A comment-bearing file is VALID per the spec — it must NOT be treated as malformed
    /// and quarantined to .bak. Pre-fix this test failed: comments triggered JSON parse
    /// failure → backup path → silent quarantine of a perfectly valid file.
    /// </summary>
    [Fact]
    public async Task WriteMcpJsonAsync_JsoncWithComments_PreservesNoBackup()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        var mcpJsonPath = Path.Combine(vscodeDir, "mcp.json");
        await File.WriteAllTextAsync(mcpJsonPath, """
            // VS Code MCP server config — supports JSONC
            {
              "servers": {
                /* My team's custom MCP server */
                "team-server": {
                  "type": "stdio",
                  "command": "node",
                  "args": ["server.js"],
                },
              },
            }
            """);

        await InitCommand.WriteMcpJsonAsync(_tempDir);

        // No backup should exist — the comments are valid JSONC.
        var backups = Directory.GetFiles(vscodeDir, "mcp.json.bak.*");
        Assert.Empty(backups);

        // And the team-server entry must still be present alongside maf-doctor.
        var root = JsonNode.Parse(await File.ReadAllTextAsync(mcpJsonPath))!.AsObject();
        var servers = root["servers"]!.AsObject();
        Assert.True(servers.ContainsKey("team-server"));
        Assert.True(servers.ContainsKey("maf-doctor"));
    }

    /// <summary>
    /// Non-`servers` top-level keys (e.g., `inputs`) must survive the merge.
    /// </summary>
    [Fact]
    public async Task WriteMcpJsonAsync_PreservesNonServersTopLevelKeys()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        var mcpJsonPath = Path.Combine(vscodeDir, "mcp.json");
        await File.WriteAllTextAsync(mcpJsonPath, """
            {
              "inputs": [
                { "id": "api-key", "type": "promptString", "password": true }
              ],
              "servers": {}
            }
            """);

        await InitCommand.WriteMcpJsonAsync(_tempDir);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(mcpJsonPath))!.AsObject();
        Assert.True(root.ContainsKey("inputs"),
            "Non-servers top-level keys must survive the merge.");
        Assert.True(root["servers"]!.AsObject().ContainsKey("maf-doctor"));
    }

    // -------------------------------------------------------------------------
    // Claude Code .mcp.json (repo root, "mcpServers" key)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WriteClaudeMcpJsonAsync_NoExistingFile_CreatesMcpServersEntry()
    {
        await InitCommand.WriteClaudeMcpJsonAsync(_tempDir);

        var path = Path.Combine(_tempDir, ".mcp.json");
        Assert.True(File.Exists(path));

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        // Claude Code uses "mcpServers" (not VS Code's "servers").
        var servers = root["mcpServers"]!.AsObject();
        Assert.True(servers.ContainsKey("maf-doctor"));
        // Claude Code .mcp.json omits "type" — stdio is the implied default.
        Assert.False(servers["maf-doctor"]!.AsObject().ContainsKey("type"));
        Assert.Equal("maf-doctor", servers["maf-doctor"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public async Task WriteClaudeMcpJsonAsync_IsIdempotent()
    {
        await InitCommand.WriteClaudeMcpJsonAsync(_tempDir);
        var path = Path.Combine(_tempDir, ".mcp.json");
        var first = await File.ReadAllTextAsync(path);

        await InitCommand.WriteClaudeMcpJsonAsync(_tempDir);
        var second = await File.ReadAllTextAsync(path);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task WriteClaudeMcpJsonAsync_PreservesUnrelatedServers()
    {
        var path = Path.Combine(_tempDir, ".mcp.json");
        await File.WriteAllTextAsync(path, """
            {
              "mcpServers": {
                "my-other-server": { "type": "stdio", "command": "node", "args": ["x.js"] }
              }
            }
            """);

        await InitCommand.WriteClaudeMcpJsonAsync(_tempDir);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        var servers = root["mcpServers"]!.AsObject();
        Assert.True(servers.ContainsKey("my-other-server"), "Existing entries must be preserved.");
        Assert.True(servers.ContainsKey("maf-doctor"), "maf-doctor entry must be added.");
    }

    [Fact]
    public async Task WriteClaudeMcpJsonAsync_RecognizesLegacyMafAutopilotKey_NoDuplicate()
    {
        var path = Path.Combine(_tempDir, ".mcp.json");
        await File.WriteAllTextAsync(path, """
            {
              "mcpServers": {
                "maf-autopilot": { "type": "stdio", "command": "maf-autopilot" }
              }
            }
            """);

        await InitCommand.WriteClaudeMcpJsonAsync(_tempDir);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        var servers = root["mcpServers"]!.AsObject();
        Assert.False(servers.ContainsKey("maf-autopilot"));
        Assert.True(servers.ContainsKey("maf-doctor"));
        Assert.Equal("maf-doctor", servers["maf-doctor"]!["command"]!.GetValue<string>());
        Assert.False(servers["maf-doctor"]!.AsObject().ContainsKey("type"));
        Assert.Equal(InitCommand.CurrentVersion,
            servers["maf-doctor"]!["env"]![InitCommand.InitVersionEnvName]!.GetValue<string>());
    }

    [Fact]
    public async Task WriteMcpJsonAsync_StaleManagedEntry_IsRepaired()
    {
        var vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        var mcpJsonPath = Path.Combine(vscodeDir, "mcp.json");
        await File.WriteAllTextAsync(mcpJsonPath, """
            {
              "servers": {
                "maf-doctor": {
                  "type": "stdio",
                  "command": "maf-autopilot",
                  "args": ["--old"],
                  "env": { "KEEP_ME": "yes" }
                },
                "other": { "type": "stdio", "command": "node" }
              }
            }
            """);

        await InitCommand.WriteMcpJsonAsync(_tempDir);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(mcpJsonPath))!.AsObject();
        var servers = root["servers"]!.AsObject();
        Assert.True(servers.ContainsKey("other"));
        var entry = servers["maf-doctor"]!.AsObject();
        Assert.Equal("maf-doctor", entry["command"]!.GetValue<string>());
        Assert.Empty(entry["args"]!.AsArray());
        Assert.Equal("yes", entry["env"]!["KEEP_ME"]!.GetValue<string>());
        Assert.Equal(InitCommand.CurrentVersion,
            entry["env"]![InitCommand.InitVersionEnvName]!.GetValue<string>());
    }

    // -------------------------------------------------------------------------
    // Steering sidecars — overwrite-on-reinit, convention-correct, never duplicated,
    // and never touching the user's own copilot-instructions.md / CLAUDE.md body.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task WriteSidecar_CopilotInstructions_CreatesAutoLoadedSidecar_NotUserFile()
    {
        await InitCommand.WriteSidecarAsync(_tempDir, "steering/copilot-instructions.md",
            ".github/instructions/maf-doctor.instructions.md", "---\napplyTo: '**'\n---\n\n");

        var sidecar = Path.Combine(_tempDir, ".github", "instructions", "maf-doctor.instructions.md");
        Assert.True(File.Exists(sidecar));

        var content = await File.ReadAllTextAsync(sidecar);
        Assert.StartsWith("---", content);                 // applyTo frontmatter so Copilot auto-loads it
        Assert.Contains("applyTo:", content);
        Assert.Contains("## Using maf-doctor", content);    // steering body present

        // It must NOT create/touch the user's own copilot-instructions.md.
        Assert.False(File.Exists(Path.Combine(_tempDir, ".github", "copilot-instructions.md")));
    }

    [Fact]
    public async Task WriteSidecar_ReRun_OverwritesWithoutDuplicating()
    {
        const string rel = ".github/instructions/maf-doctor.instructions.md";
        const string fm = "---\napplyTo: '**'\n---\n\n";
        await InitCommand.WriteSidecarAsync(_tempDir, "steering/copilot-instructions.md", rel, fm);
        var first = await File.ReadAllTextAsync(Path.Combine(_tempDir, ".github", "instructions", "maf-doctor.instructions.md"));

        await InitCommand.WriteSidecarAsync(_tempDir, "steering/copilot-instructions.md", rel, fm);
        var second = await File.ReadAllTextAsync(Path.Combine(_tempDir, ".github", "instructions", "maf-doctor.instructions.md"));

        Assert.Equal(first, second);
        Assert.Equal(1, second.Split("## Using maf-doctor").Length - 1);
    }

    [Fact]
    public async Task WriteClaudeSteering_WritesSidecarAndImportsItExactlyOnce()
    {
        await InitCommand.WriteClaudeSteeringAsync(_tempDir);
        await InitCommand.WriteClaudeSteeringAsync(_tempDir);  // re-run must stay idempotent

        var sidecar = Path.Combine(_tempDir, ".claude", "maf-doctor.md");
        var claudeMd = Path.Combine(_tempDir, "CLAUDE.md");
        Assert.True(File.Exists(sidecar));
        Assert.True(File.Exists(claudeMd));
        Assert.Contains("## Microsoft Agent Framework", await File.ReadAllTextAsync(sidecar));

        var claudeContent = await File.ReadAllTextAsync(claudeMd);
        Assert.Equal(1, claudeContent.Split("@.claude/maf-doctor.md").Length - 1);
    }

    [Fact]
    public async Task WriteClaudeSteering_PreservesExistingClaudeMdContent()
    {
        var claudeMd = Path.Combine(_tempDir, "CLAUDE.md");
        await File.WriteAllTextAsync(claudeMd, "# My project rules\n\nUse 4-space indents.\n");

        await InitCommand.WriteClaudeSteeringAsync(_tempDir);

        var content = await File.ReadAllTextAsync(claudeMd);
        Assert.Contains("Use 4-space indents.", content);    // user content preserved
        Assert.Contains("@.claude/maf-doctor.md", content);  // import appended
    }

    [Fact]
    public async Task UpsertManagedBlock_Agents_ReplacesBlockWithoutDuplicating()
    {
        await InitCommand.UpsertManagedBlockAsync(_tempDir, "steering/agents.md", "AGENTS.md");
        await InitCommand.UpsertManagedBlockAsync(_tempDir, "steering/agents.md", "AGENTS.md");

        var content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "AGENTS.md"));
        Assert.Equal(1, content.Split("<!-- BEGIN maf-doctor").Length - 1);
        Assert.Equal(1, content.Split("<!-- END maf-doctor -->").Length - 1);
    }

    [Fact]
    public async Task UpsertManagedBlock_Agents_PreservesSurroundingUserContent()
    {
        var path = Path.Combine(_tempDir, "AGENTS.md");
        await File.WriteAllTextAsync(path, "# My agents\n\nProject-specific notes.\n");

        await InitCommand.UpsertManagedBlockAsync(_tempDir, "steering/agents.md", "AGENTS.md");

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("Project-specific notes.", content);
        Assert.Contains("<!-- BEGIN maf-doctor", content);
    }

    [Fact]
    public void GetWorkspaceInitStatus_LargeMarkerFile_DoesNotReadWholeFile()
    {
        var claudeMd = Path.Combine(_tempDir, "CLAUDE.md");
        File.WriteAllText(claudeMd, new string('x', 1024 * 1024 + 1));

        var status = InitCommand.GetWorkspaceInitStatus(_tempDir);

        Assert.Contains(status.Findings,
            finding => finding.Contains("CLAUDE.md exceeds the 1 MB marker-scan safety cap", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FindInitializedWorkspaceRoot_WalksUpFromSubdirectory()
    {
        await InitCommand.WriteMcpJsonAsync(_tempDir);
        var nested = Path.Combine(_tempDir, "src", "app");
        Directory.CreateDirectory(nested);

        var root = InitCommand.FindInitializedWorkspaceRoot(nested);

        Assert.Equal(Path.GetFullPath(_tempDir), root);
    }

    // -------------------------------------------------------------------------
    // Drift guard — copilot-instructions.md / claude-instructions.md / agents.md
    // intentionally use different H2 titles and opening framing per tool, but the
    // numbered guidance (item 1 onward) is meant to be identical prose duplicated
    // across three files with no shared source. Nothing else catches drift if one
    // is edited and the others aren't — pin the shared tail here.
    // -------------------------------------------------------------------------

    [Fact]
    public void SteeringBodies_CopilotClaudeAgents_ShareIdenticalActionableGuidance()
    {
        var copilot = ReadEmbeddedSteeringSnippet("steering/copilot-instructions.md");
        var claude = ReadEmbeddedSteeringSnippet("steering/claude-instructions.md");
        var agents = ReadEmbeddedSteeringSnippet("steering/agents.md");

        const string anchor = "1. **Always call `MafDoctor` first**";
        var copilotTail = copilot[copilot.IndexOf(anchor, StringComparison.Ordinal)..];
        var claudeTail = claude[claude.IndexOf(anchor, StringComparison.Ordinal)..];
        var agentsTail = agents[agents.IndexOf(anchor, StringComparison.Ordinal)..];

        Assert.Equal(copilotTail, claudeTail);
        Assert.Equal(copilotTail, agentsTail);
    }

    private static string ReadEmbeddedSteeringSnippet(string logicalName)
    {
        using var stream = typeof(InitCommand).Assembly.GetManifestResourceStream(logicalName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
