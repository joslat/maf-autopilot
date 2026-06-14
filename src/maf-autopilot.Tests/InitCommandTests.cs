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
        Assert.Equal("stdio", servers["maf-doctor"]!["type"]!.GetValue<string>());
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
        Assert.True(servers.ContainsKey("maf-autopilot"));
        Assert.False(servers.ContainsKey("maf-doctor"),
            "Legacy maf-autopilot key must be recognized so no duplicate maf-doctor entry is added.");
    }

    // -------------------------------------------------------------------------
    // Steering merge idempotency — marker must match the snippet's actual header
    // -------------------------------------------------------------------------

    /// <summary>
    /// Regression: the merge-marker once read "## Using maf-doctor" while the rename left
    /// the copilot snippet header at "## Using maf-autopilot (MAF Doctor)", so re-running
    /// init appended a duplicate section. Headers + markers are now aligned.
    /// </summary>
    [Fact]
    public async Task DropSteeringFile_CopilotInstructions_ReRun_DoesNotDuplicate()
    {
        await InitCommand.DropSteeringFileAsync(_tempDir, "steering/copilot-instructions.md", ".github/copilot-instructions.md");
        await InitCommand.DropSteeringFileAsync(_tempDir, "steering/copilot-instructions.md", ".github/copilot-instructions.md");

        var path = Path.Combine(_tempDir, ".github", "copilot-instructions.md");
        var content = await File.ReadAllTextAsync(path);
        var occurrences = content.Split("## Using maf-doctor").Length - 1;
        Assert.Equal(1, occurrences);
    }
}
