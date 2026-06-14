using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MafDoctor;

/// <summary>
/// CLI init subcommand: `maf-doctor init [--with-cursor]`
///
/// Configures a target repository for MAF migration:
///   1. Writes (or merges) the maf-doctor global-tool server entry into both
///      .vscode/mcp.json (VS Code / Copilot) and .mcp.json (Claude Code).
///   2. Drops overwrite-on-reinit steering sidecars in each tool's auto-loaded
///      convention location (never merging into the user's own files):
///        • .github/instructions/maf-doctor.instructions.md   (Copilot)
///        • .claude/maf-doctor.md + an @import line in CLAUDE.md (Claude Code)
///        • AGENTS.md — a sentinel-delimited managed block (no sidecar convention)
///   3. With --with-cursor: also .cursor/rules/maf-doctor.mdc.
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
        await WriteClaudeMcpJsonAsync(targetDir);

        // Steering — overwrite-on-reinit sidecars in each tool's auto-loaded
        // convention dir. We never merge into the user's own copilot-instructions.md
        // / CLAUDE.md, and re-running init refreshes the guidance (no duplicates).
        await WriteSidecarAsync(targetDir, "steering/copilot-instructions.md",
            ".github/instructions/maf-doctor.instructions.md", "---\napplyTo: '**'\n---\n\n");
        await WriteClaudeSteeringAsync(targetDir);
        await UpsertManagedBlockAsync(targetDir, "steering/agents.md", "AGENTS.md");
        if (withCursor)
            await WriteSidecarAsync(targetDir, "steering/cursor-rules.md",
                ".cursor/rules/maf-doctor.mdc",
                "---\ndescription: MAF Doctor — use the MCP tools for Microsoft Agent Framework code\nalwaysApply: true\n---\n\n");

        Console.WriteLine();
        Console.WriteLine("Done. ⚡ Try these three commands first:");
        Console.WriteLine();
        Console.WriteLine("  maf-doctor doctor .            # instant A/B/C/F health letter");
        Console.WriteLine("  maf-doctor new agent ChatBot   # scaffold a clean MAF agent");
        Console.WriteLine();
        Console.WriteLine("The MCP server is now wired for both VS Code (.vscode/mcp.json) and");
        Console.WriteLine("Claude Code (.mcp.json). In Copilot Chat or Claude Code:");
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
        // VS Code reads .vscode/mcp.json with a top-level "servers" object.
        await WriteMcpServerEntryAsync(
            Path.Combine(targetDir, ".vscode", "mcp.json"),
            serversKey: "servers",
            label: ".vscode/mcp.json (VS Code / Copilot)");
    }

    internal static async Task WriteClaudeMcpJsonAsync(string targetDir)
    {
        // Claude Code reads .mcp.json at the repo root with a top-level "mcpServers" object.
        await WriteMcpServerEntryAsync(
            Path.Combine(targetDir, ".mcp.json"),
            serversKey: "mcpServers",
            label: ".mcp.json (Claude Code)");
    }

    /// <summary>
    /// Adds the maf-doctor stdio server entry to an MCP config file, MERGING into any
    /// existing config rather than overwriting. <paramref name="serversKey"/> is the
    /// top-level object that holds server entries — "servers" for VS Code's
    /// .vscode/mcp.json, "mcpServers" for Claude Code's .mcp.json. Recognizes the legacy
    /// "maf-autopilot" server key so re-running init on a pre-rename repo never duplicates.
    /// </summary>
    private static async Task WriteMcpServerEntryAsync(string mcpJsonPath, string serversKey, string label)
    {
        var dir = Path.GetDirectoryName(mcpJsonPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Read existing file if present. The mcp.json spec allows // and /* */ comments
        // and trailing commas (JSONC), so use lenient parser options before deciding the
        // file is malformed. On a true parse failure, back up the original so we never
        // silently destroy other MCP-server entries.
        // Security: cap the JSON read at 1 MB. A malicious or corrupt config (from a
        // template repo, a stale upstream, or a paste accident) could otherwise OOM the
        // host on the very first `maf-doctor init`. Real config files are kilobytes at most.
        const long McpJsonMaxBytes = 1 * 1024 * 1024;
        JsonObject root;
        if (File.Exists(mcpJsonPath))
        {
            var info = new FileInfo(mcpJsonPath);
            if (info.Length > McpJsonMaxBytes)
            {
                var backupPath = CreateBackupWithRetry(mcpJsonPath);
                Console.WriteLine($"  ⚠ {label} is {info.Length / 1024} KB — exceeds the 1 MB safety cap.");
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
                    Console.WriteLine($"  ⚠ {label} exists but could not be parsed as JSON (even allowing // comments).");
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

        if (!root.ContainsKey(serversKey))
            root[serversKey] = new JsonObject();

        var servers = root[serversKey]!.AsObject();

        // Server key + command are both "maf-doctor" (full rename). Recognize the
        // LEGACY "maf-autopilot" server key too, so re-running init on a repo
        // configured by a pre-rename build doesn't add a duplicate entry.
        if (servers.ContainsKey("maf-doctor") || servers.ContainsKey("maf-autopilot"))
        {
            Console.WriteLine($"  ✓ {label} — MAF Doctor server entry already present, no change");
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
        Console.WriteLine($"  ✓ {label} — added MAF Doctor (maf-doctor) global-tool server entry");
    }

    // ------------------------------------------------------------------ //
    //  Steering — overwrite-on-reinit sidecars in each tool's convention dir
    // ------------------------------------------------------------------ //
    //
    //  We do NOT merge into the user's own copilot-instructions.md / CLAUDE.md.
    //  Each tool auto-loads a convention-specific location; we own a dedicated file
    //  there and overwrite it on every init, so the guidance refreshes with the
    //  installed maf-doctor version and can never stack duplicate sections.

    /// <summary>
    /// Writes a steering sidecar from an embedded snippet, OVERWRITING any prior copy
    /// (re-running init refreshes the guidance). The snippet's leading HTML-comment
    /// header is stripped; optional convention-specific frontmatter is prepended.
    /// </summary>
    internal static async Task WriteSidecarAsync(
        string targetDir, string embeddedResourceName, string outputRelativePath, string? frontmatter = null)
    {
        var body = ReadSteeringBody(embeddedResourceName);
        if (body is null)
        {
            Console.WriteLine($"  ⚠ Steering resource '{embeddedResourceName}' not found in assembly — skipped");
            return;
        }

        var outputPath = Path.Combine(targetDir, outputRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var content = (frontmatter ?? string.Empty) + body;
        if (!content.EndsWith("\n", StringComparison.Ordinal)) content += "\n";
        await File.WriteAllTextAsync(outputPath, content);
        Console.WriteLine($"  ✓ {outputRelativePath} — written (refreshed on each init)");
    }

    /// <summary>
    /// Claude Code auto-loads CLAUDE.md. We keep the steering body in a sidecar
    /// (.claude/maf-doctor.md, overwritten each init) and ensure CLAUDE.md pulls it in
    /// via a single stable `@.claude/maf-doctor.md` import — added once, never
    /// duplicated; the rest of the user's CLAUDE.md is never touched.
    /// </summary>
    internal static async Task WriteClaudeSteeringAsync(string targetDir)
    {
        var body = ReadSteeringBody("steering/claude-instructions.md");
        if (body is null)
        {
            Console.WriteLine("  ⚠ Steering resource 'steering/claude-instructions.md' not found — skipped");
            return;
        }

        // Sidecar — overwrite-always.
        var sidecarPath = Path.Combine(targetDir, ".claude", "maf-doctor.md");
        Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
        await File.WriteAllTextAsync(sidecarPath, body.EndsWith("\n", StringComparison.Ordinal) ? body : body + "\n");
        Console.WriteLine("  ✓ .claude/maf-doctor.md — written (refreshed on each init)");

        // Ensure CLAUDE.md imports it (one stable line; add only if absent).
        const string importLine = "@.claude/maf-doctor.md";
        const string managedComment = "<!-- MAF Doctor steering — managed by `maf-doctor init`; .claude/maf-doctor.md is overwritten on re-run -->";
        var claudePath = Path.Combine(targetDir, "CLAUDE.md");
        if (File.Exists(claudePath))
        {
            var existing = await File.ReadAllTextAsync(claudePath);
            if (existing.Contains(importLine, StringComparison.Ordinal))
            {
                Console.WriteLine("  ✓ CLAUDE.md — already imports MAF Doctor steering, no change");
                return;
            }
            var sep = existing.Length == 0 || existing.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : "\n";
            await File.AppendAllTextAsync(claudePath, $"{sep}\n{managedComment}\n{importLine}\n");
            Console.WriteLine("  ✓ CLAUDE.md — added @import for MAF Doctor steering");
        }
        else
        {
            await File.WriteAllTextAsync(claudePath, $"{managedComment}\n{importLine}\n");
            Console.WriteLine("  ✓ CLAUDE.md — created with @import for MAF Doctor steering");
        }
    }

    /// <summary>
    /// Upserts a sentinel-delimited managed block into a shared file that has no
    /// auto-loaded sidecar convention (AGENTS.md). The block between the BEGIN/END
    /// markers is REPLACED on re-init (refreshes, never duplicates); appended if the
    /// markers are absent; the file is created if missing. Content outside the block
    /// is left untouched.
    /// </summary>
    internal static async Task UpsertManagedBlockAsync(
        string targetDir, string embeddedResourceName, string outputRelativePath)
    {
        var body = ReadSteeringBody(embeddedResourceName);
        if (body is null)
        {
            Console.WriteLine($"  ⚠ Steering resource '{embeddedResourceName}' not found — skipped");
            return;
        }

        const string begin = "<!-- BEGIN maf-doctor (managed by `maf-doctor init` — overwritten on re-run) -->";
        const string end = "<!-- END maf-doctor -->";
        var block = $"{begin}\n{body.TrimEnd()}\n{end}";

        var outputPath = Path.Combine(targetDir, outputRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (File.Exists(outputPath))
        {
            var existing = await File.ReadAllTextAsync(outputPath);
            var bi = existing.IndexOf(begin, StringComparison.Ordinal);
            var ei = existing.IndexOf(end, StringComparison.Ordinal);
            if (bi >= 0 && ei > bi)
            {
                var updated = existing[..bi] + block + existing[(ei + end.Length)..];
                await File.WriteAllTextAsync(outputPath, updated);
                Console.WriteLine($"  ✓ {outputRelativePath} — refreshed managed maf-doctor block");
            }
            else
            {
                var sep = existing.Length == 0 || existing.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : "\n";
                await File.AppendAllTextAsync(outputPath, $"{sep}\n{block}\n");
                Console.WriteLine($"  ✓ {outputRelativePath} — added managed maf-doctor block");
            }
        }
        else
        {
            await File.WriteAllTextAsync(outputPath, block + "\n");
            Console.WriteLine($"  ✓ {outputRelativePath} — created with managed maf-doctor block");
        }
    }

    private static string? ReadSteeringBody(string embeddedResourceName)
    {
        var raw = ReadEmbeddedResource(Assembly.GetExecutingAssembly(), embeddedResourceName);
        return raw is null ? null : StripLeadingHtmlComment(raw);
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
