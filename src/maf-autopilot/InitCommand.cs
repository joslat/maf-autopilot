using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MafDoctor;

/// <summary>
/// CLI init subcommand: `maf-doctor init [--with-cursor]`
///
/// Configures a target repository for MAF migration:
///   1. Writes (or merges) .vscode/mcp.json with the maf-doctor global-tool server entry.
///   2. Creates .github/copilot-instructions.md (MAF hard constraints) — only if it doesn't exist.
///   3. Drops steering snippets: CLAUDE.md and AGENTS.md with merge-not-overwrite semantics.
///   4. With --with-cursor: also drops .cursorrules.
///
/// Run from the root of the repository you want to configure:
///   cd /path/to/your-repo
///   maf-doctor init
///   maf-doctor init --with-cursor   # also drops .cursorrules
/// </summary>
internal static class InitCommand
{
    /// <summary>
    /// VS Code's mcp.json is JSONC — supports // line comments, /* block */ comments,
    /// and trailing commas. Use these options instead of strict JSON parsing.
    /// </summary>
    private static readonly JsonDocumentOptions JsoncOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Copies the file to a backup path with a unique suffix, retrying on collision.
    /// Returns the path that was actually used.
    /// </summary>
    private static string CreateBackupWithRetry(string originalPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"{originalPath}.bak.{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
            try
            {
                File.Copy(originalPath, candidate, overwrite: false);
                return candidate;
            }
            catch (IOException) when (attempt < 4)
            {
                // Same-millisecond collision is astronomically unlikely; retry regenerates the suffix.
            }
        }
        throw new IOException(
            $"Could not create a unique backup for '{originalPath}' after 5 attempts. " +
            $"Check that the directory is writable.");
    }

    public static async Task<int> RunAsync(string[]? args = null)
    {
        var withCursor = args != null && args.Contains("--with-cursor", StringComparer.OrdinalIgnoreCase);
        var targetDir = Directory.GetCurrentDirectory();

        Console.WriteLine("maf-doctor init");
        Console.WriteLine($"  Configuring: {targetDir}");
        if (withCursor) Console.WriteLine("  --with-cursor: .cursorrules will also be dropped");
        Console.WriteLine();

        await WriteMcpJsonAsync(targetDir);
        await WriteCopilotInstructionsAsync(targetDir);
        await DropSteeringFileAsync(targetDir, "steering/copilot-instructions.md", ".github/copilot-instructions.md");
        await DropSteeringFileAsync(targetDir, "steering/claude-instructions.md", "CLAUDE.md");
        await DropSteeringFileAsync(targetDir, "steering/agents.md", "AGENTS.md");
        if (withCursor)
            await DropSteeringFileAsync(targetDir, "steering/cursor-rules.md", ".cursorrules");

        Console.WriteLine();
        Console.WriteLine("Done. ⚡ Try these three commands first:");
        Console.WriteLine();
        Console.WriteLine("  maf-doctor doctor .            # instant A/B/C/F health letter");
        Console.WriteLine("  maf-doctor new agent ChatBot   # scaffold a clean 1.3.0 agent");
        Console.WriteLine();
        Console.WriteLine("Then open this folder in VS Code. In Copilot Chat:");
        Console.WriteLine("  @maf-auditor                      # pre-migration scan + plan");
        Console.WriteLine("  @maf-best-practice-reviewer       # steady-state best-practice audit");
        Console.WriteLine("  @maf-migration                    # execute a migration-plan.md");
        Console.WriteLine();
        Console.WriteLine("Full guide:        read maf://guide");
        Console.WriteLine("Constraint rules:  read maf://constraints");

        return 0;
    }

    // ------------------------------------------------------------------ //
    //  .vscode/mcp.json                                                    //
    // ------------------------------------------------------------------ //

    internal static async Task WriteMcpJsonAsync(string targetDir)
    {
        var vscodeDir = Path.Combine(targetDir, ".vscode");
        var mcpJsonPath = Path.Combine(vscodeDir, "mcp.json");

        Directory.CreateDirectory(vscodeDir);

        // Read existing file if present. VS Code's mcp.json spec allows // and /* */
        // comments and trailing commas (JSON-with-comments / JSONC), so use lenient
        // parser options before deciding the file is malformed. On a true parse failure,
        // back up the original so we never silently destroy other MCP-server entries.
        // Security: cap the JSON read at 1 MB. A malicious or corrupt mcp.json (from a
        // template repo, a stale upstream, or a paste accident) could otherwise OOM the
        // host on the very first `maf-doctor init` invocation. Real mcp.json files
        // are kilobytes at most.
        const long McpJsonMaxBytes = 1 * 1024 * 1024;
        JsonObject root;
        if (File.Exists(mcpJsonPath))
        {
            var info = new FileInfo(mcpJsonPath);
            if (info.Length > McpJsonMaxBytes)
            {
                var backupPath = CreateBackupWithRetry(mcpJsonPath);
                Console.WriteLine($"  ⚠ .vscode/mcp.json is {info.Length / 1024} KB — exceeds the 1 MB safety cap.");
                Console.WriteLine($"    Backed up to: {backupPath}");
                Console.WriteLine($"    Starting fresh — restore any other server entries from the backup manually.");
                root = new JsonObject();
            }
            else
            {
                var raw = await File.ReadAllTextAsync(mcpJsonPath);
                try
                {
                    root = JsonNode.Parse(raw, nodeOptions: null, documentOptions: JsoncOptions)!.AsObject();
                }
                catch
                {
                    var backupPath = CreateBackupWithRetry(mcpJsonPath);
                    Console.WriteLine($"  ⚠ .vscode/mcp.json exists but could not be parsed as JSON (even allowing // comments).");
                    Console.WriteLine($"    Backed up to: {backupPath}");
                    Console.WriteLine($"    Starting fresh — restore any other server entries from the backup manually.");
                    root = new JsonObject();
                }
            }
        }
        else
        {
            root = new JsonObject();
        }

        if (!root.ContainsKey("servers"))
            root["servers"] = new JsonObject();

        var servers = root["servers"]!.AsObject();

        // Server key + command are both "maf-doctor" (full rename). Recognize the
        // LEGACY "maf-autopilot" server key too, so re-running init on a repo
        // configured by a pre-rename build doesn't add a duplicate entry.
        if (servers.ContainsKey("maf-doctor") || servers.ContainsKey("maf-autopilot"))
        {
            Console.WriteLine("  ✓ .vscode/mcp.json — MAF Doctor server entry already present, no change");
            return;
        }

        // Add the global-tool entry (assumes `dotnet tool install -g maf-doctor`).
        servers["maf-doctor"] = new JsonObject
        {
            ["type"] = "stdio",
            ["command"] = "maf-doctor",
            ["args"] = new JsonArray(),
            ["env"] = new JsonObject()
        };

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(mcpJsonPath, json + Environment.NewLine);
        Console.WriteLine("  ✓ .vscode/mcp.json — added MAF Doctor (maf-doctor) global-tool server entry");
    }

    // ------------------------------------------------------------------ //
    //  .github/copilot-instructions.md                                     //
    // ------------------------------------------------------------------ //

    private static async Task WriteCopilotInstructionsAsync(string targetDir)
    {
        var githubDir = Path.Combine(targetDir, ".github");
        var instructionsPath = Path.Combine(githubDir, "copilot-instructions.md");

        if (File.Exists(instructionsPath))
        {
            Console.WriteLine("  ℹ .github/copilot-instructions.md already exists — not overwriting");
            Console.WriteLine("    (Add the constraint rules manually, or delete it and re-run init)");
            return;
        }

        Directory.CreateDirectory(githubDir);

        // Write a thin POINTER, not a copy. The previous version embedded the full
        // constraints content here, which drifted the moment the MCP server bundle
        // shipped a new revision. A pointer file is always current — Copilot fetches
        // `maf://constraints` from the live MCP server on every chat turn.
        //
        // Rationale: a fixed-at-install-time copy of a living document is technical
        // debt. See plan #66.
        var pointer = """
            # MAF Migration — Auto-Loaded Constraints

            > **Auto-generated by `maf-doctor init`. Do not hand-edit.**
            > This file is a stable pointer; the authoritative content lives in the MCP server.
            > **Regenerate:** delete this file and re-run `maf-doctor init`.

            ## How constraints work in this workspace

            The `maf-doctor` MCP server (configured in `.vscode/mcp.json`) exposes the
            current MAF best-practice constraints at the resource URI **`maf://constraints`**.
            GitHub Copilot Chat reads that resource on demand — the constraints stay current
            with whatever version of `maf-doctor` is installed, with no manual sync.

            To read the constraints right now, in Copilot Chat:
            > Read `maf://constraints` and summarise the hard rules.

            ## How constraints are enforced

            The `maf-doctor` MCP server exposes executable tools that verify each constraint:

            | Constraint                                     | Verify by calling                                  |
            |------------------------------------------------|----------------------------------------------------|
            | Instance state in `AIContextProvider`          | `MafScanAntiPatterns(repoPath)` → `MAF-AP-CONC-001` |
            | `DefaultAzureCredential` in production         | `MafScanAntiPatterns(repoPath)` → `MAF-AP-SEC-001`  |
            | `EnableSensitiveData = true` outside dev       | `MafScanAntiPatterns(repoPath)` → `MAF-AP-SEC-003`  |
            | `Instructions` outside `ChatOptions` (silent ignore) | `MafScanAntiPatterns(repoPath)` → `MAF-AP-AGENT-001` |
            | Executor class must be `sealed partial`        | `MafScanAntiPatterns(repoPath)` → `MAF-AP-WF-001`   |
            | Middleware `Use()` missing `runStreamingFunc`  | `MafScanAntiPatterns(repoPath)` → `MAF-AP-MID-001`  |
            | DevUI references must be `#if DEVUI_ENABLED`   | `MafScanAntiPatterns(repoPath)` → `MAF-AP-DEVUI-001` |
            | Fan-out handler must return `Task<T>`          | `MafValidateFanOut(repoPath)` (or `MafSimulateWorkflow`) |
            | `AddFanInBarrierEdge` argument order / removed attrs | `MafRunCs0618Hunt(projectPath)`               |
            | Agent prompt quality (injection, bloat, refusals)    | `MafLintAgentPrompt(repoPath)`                |
            | Quick "is this API safe in 1.3.0?"             | `MafApiSafety(apiName)`                             |
            | Explain any MAF snippet inline                 | `MafExplain(snippet)`                               |
            | Scaffold a new MAF agent / executor            | `MafNewAgent(projectPath, name)` / `MafNewExecutor` |
            | Workflow topology proof + Mermaid diagram      | `MafSimulateWorkflow(repoPath)`                     |
            | Single-command health letter (A/B/C/F)         | `MafDoctor(repoPath)` — or CLI `maf-doctor doctor` |

            For IDE-time enforcement (red squigglies as you type), add the `maf-doctor.Analyzers`
            NuGet package to your project — ships rules `MAF001` / `MAF002` / `MAF003`.
            """;

        await File.WriteAllTextAsync(instructionsPath, pointer);
        Console.WriteLine("  ✓ .github/copilot-instructions.md — pointer to maf://constraints installed");
        Console.WriteLine("    (Always-current — fetched from the live MCP server, not a stale copy)");
    }

    // ------------------------------------------------------------------ //
    //  Steering snippets (CLAUDE.md, AGENTS.md, .github/copilot-instructions.md, .cursorrules)
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Drops a steering snippet from embedded resources into the target repository.
    /// Merge-not-overwrite: if the file already exists AND already contains the
    /// steering snippet marker ("Using maf-doctor" section), we skip. If the file
    /// exists but does NOT contain the section, we append. If the file does not exist,
    /// we create it. The HTML comment header at the top of each snippet is stripped
    /// when appending to avoid duplicate meta-comments.
    /// </summary>
    internal static async Task DropSteeringFileAsync(
        string targetDir,
        string embeddedResourceName,
        string outputRelativePath)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resourceContent = ReadEmbeddedResource(asm, embeddedResourceName);
        if (resourceContent is null)
        {
            // Embedded resource not found — skip silently (may happen if csproj
            // didn't include the file yet during development).
            Console.WriteLine($"  ⚠ Steering resource '{embeddedResourceName}' not found in assembly — skipped");
            return;
        }

        var outputPath = Path.Combine(targetDir, outputRelativePath);
        var markerText = "## Using maf-doctor";

        if (File.Exists(outputPath))
        {
            var existing = await File.ReadAllTextAsync(outputPath);
            if (existing.Contains(markerText, StringComparison.OrdinalIgnoreCase)
                || existing.Contains("## Microsoft Agent Framework", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  ✓ {outputRelativePath} — maf-doctor section already present, no change");
                return;
            }

            // Append: strip the leading HTML comment block from the snippet content
            var body = StripLeadingHtmlComment(resourceContent);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.AppendAllTextAsync(outputPath, $"\n\n{body}");
            Console.WriteLine($"  ✓ {outputRelativePath} — merged maf-doctor steering section");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, resourceContent);
            Console.WriteLine($"  ✓ {outputRelativePath} — created");
        }
    }

    private static string? ReadEmbeddedResource(Assembly asm, string logicalName)
    {
        using var stream = asm.GetManifestResourceStream(logicalName);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Strips the leading HTML comment block (<!-- ... -->) from the content, if present.
    /// This avoids duplicate meta-comment headers when appending to an existing file.
    /// </summary>
    private static string StripLeadingHtmlComment(string content)
    {
        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("<!--", StringComparison.Ordinal)) return content;
        var endIndex = trimmed.IndexOf("-->", StringComparison.Ordinal);
        if (endIndex < 0) return content;
        return trimmed[(endIndex + 3)..].TrimStart('\r', '\n');
    }

}
