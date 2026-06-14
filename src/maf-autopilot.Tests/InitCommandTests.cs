using System.Text.Json.Nodes;
using MafDoctor;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for `maf-autopilot init` — specifically the .vscode/mcp.json merge logic.
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
        _tempDir = Path.Combine(Path.GetTempPath(), "maf-autopilot-tests-" + Guid.NewGuid().ToString("N"));
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
        // Brand key is maf-doctor; the command stays the frozen binary maf-autopilot.
        Assert.Equal("maf-autopilot", servers["maf-doctor"]!["command"]!.GetValue<string>());
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

        // And the team-server entry must still be present alongside maf-autopilot.
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
}
