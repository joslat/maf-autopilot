using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MafDoctor.Tools;

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
    internal const string InitVersionEnvName = "MAF_DOCTOR_INIT_VERSION";

    /// <summary>
    /// VS Code's mcp.json is JSONC — supports // line comments, /* block */ comments,
    /// and trailing commas. Use these options instead of strict JSON parsing.
    /// </summary>
    private static readonly JsonDocumentOptions JsoncOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private const long McpJsonMaxBytes = 1 * 1024 * 1024;
    private const long MarkerFileMaxBytes = 1 * 1024 * 1024;

    /// <summary>
    /// Copies the file to a backup path with a unique suffix, retrying on collision.
    /// Returns the path that was actually used. <paramref name="originalPath"/> must
    /// already be a validated, non-symlinked path (the caller resolves it via
    /// <see cref="PathGuard.ValidateContainment"/> before calling this). Each backup
    /// candidate is itself validated against <paramref name="targetDir"/> before the
    /// copy, so a symlinked directory placed inside the workspace can't redirect the
    /// backup outside it (F-02).
    /// </summary>
    private static string CreateBackupWithRetry(string targetDir, string originalPath)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"{originalPath}.bak.{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
            try
            {
                // Round-2 review fixup — routed through SafeWorkspaceWriter.CopyNew
                // instead of a hand-duplicated ValidateContainment + File.Copy pair,
                // so this is a genuine SafeWorkspaceWriter consumer like every other
                // write site, not a parallel implementation of the same guarantee.
                SafeWorkspaceWriter.CopyNew(targetDir, originalPath, candidate);
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

    internal static string CurrentVersion
    {
        get
        {
            var raw = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "unknown";
            var plus = raw.IndexOf('+');
            return plus >= 0 ? raw[..plus] : raw;
        }
    }

    public static async Task<int> RunAsync(string[]? args = null)
    {
        var withCursor = args != null && args.Contains("--with-cursor", StringComparer.OrdinalIgnoreCase);
        var targetDir = Directory.GetCurrentDirectory();

        Console.WriteLine("maf-doctor init");
        Console.WriteLine($"  Configuring: {targetDir}");
        if (withCursor) Console.WriteLine("  --with-cursor: .cursor/rules/maf-doctor.mdc will also be dropped");
        Console.WriteLine();

        await WriteMcpJsonAsync(targetDir);
        await WriteClaudeMcpJsonAsync(targetDir);

        // Steering — overwrite-on-reinit sidecars in each tool's auto-loaded
        // convention dir. We never merge into the user's own copilot-instructions.md
        // / CLAUDE.md, and re-running init refreshes the guidance (no duplicates).
        await WriteSidecarAsync(targetDir, "steering/copilot-instructions.md",
            ".github/instructions/maf-doctor.instructions.md",
            "---\napplyTo: '**'\n---\n\n" + ManagedSidecarNote(".github/copilot-instructions.md"));
        await WriteClaudeSteeringAsync(targetDir);
        await UpsertManagedBlockAsync(targetDir, "steering/agents.md", "AGENTS.md");
        if (withCursor)
            await WriteSidecarAsync(targetDir, "steering/cursor-rules.md",
                ".cursor/rules/maf-doctor.mdc",
                "---\ndescription: MAF Doctor — use the MCP tools for Microsoft Agent Framework code\nalwaysApply: true\n---\n\n"
                    + ManagedSidecarNote("a separate .cursor/rules/*.mdc file"));

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
        // VS Code reads .vscode/mcp.json with a top-level "servers" object and
        // expects an explicit "type": "stdio" on each entry.
        await WriteMcpServerEntryAsync(
            targetDir,
            Path.Combine(targetDir, ".vscode", "mcp.json"),
            serversKey: "servers",
            label: ".vscode/mcp.json (VS Code / Copilot)",
            includeType: true);
    }

    internal static async Task WriteClaudeMcpJsonAsync(string targetDir)
    {
        // Claude Code reads .mcp.json at the repo root with a top-level "mcpServers"
        // object. Its entries are {command, args, env} — stdio is the implied default,
        // so we OMIT the "type" field (older Claude Code builds don't recognize it).
        await WriteMcpServerEntryAsync(
            targetDir,
            Path.Combine(targetDir, ".mcp.json"),
            serversKey: "mcpServers",
            label: ".mcp.json (Claude Code)",
            includeType: false);
    }

    /// <summary>
    /// Adds the maf-doctor stdio server entry to an MCP config file, MERGING into any
    /// existing config rather than overwriting. <paramref name="serversKey"/> is the
    /// top-level object that holds server entries — "servers" for VS Code's
    /// .vscode/mcp.json, "mcpServers" for Claude Code's .mcp.json. Recognizes the legacy
    /// "maf-autopilot" server key so re-running init on a pre-rename repo never duplicates.
    /// </summary>
    private static async Task WriteMcpServerEntryAsync(string targetDir, string mcpJsonPath, string serversKey, string label, bool includeType)
    {
        // F-02 — a malicious repo can make `.vscode/mcp.json` / `.mcp.json` (or a parent
        // directory) a symlink/junction. Resolve and reject before any read or write so
        // we never follow it into reading or overwriting a file outside the workspace.
        string resolvedPath;
        try
        {
            resolvedPath = PathGuard.ValidateContainment(targetDir, mcpJsonPath, label);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"  ⚠ {label} — refused: {ex.Message}");
            return;
        }

        var dir = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Read existing file if present. The mcp.json spec allows // and /* */ comments
        // and trailing commas (JSONC), so use lenient parser options before deciding the
        // file is malformed. On a true parse failure, back up the original so we never
        // silently destroy other MCP-server entries.
        // Security: cap the JSON read at 1 MB. A malicious or corrupt config (from a
        // template repo, a stale upstream, or a paste accident) could otherwise OOM the
        // host on the very first `maf-doctor init`. Real config files are kilobytes at most.
        JsonObject root;
        if (File.Exists(resolvedPath))
        {
            var info = new FileInfo(resolvedPath);
            if (info.Length > McpJsonMaxBytes)
            {
                var backupPath = CreateBackupWithRetry(targetDir, resolvedPath);
                Console.WriteLine($"  ⚠ {label} is {info.Length / 1024} KB — exceeds the 1 MB safety cap.");
                Console.WriteLine($"    Backed up to: {backupPath}");
                Console.WriteLine($"    Starting fresh — restore any other server entries from the backup manually.");
                root = new JsonObject();
            }
            else
            {
                var raw = await File.ReadAllTextAsync(resolvedPath);
                try
                {
                    root = JsonNode.Parse(raw, nodeOptions: null, documentOptions: JsoncOptions)!.AsObject();
                }
                catch
                {
                    var backupPath = CreateBackupWithRetry(targetDir, resolvedPath);
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

        var changed = UpsertCanonicalMcpEntry(servers, includeType, targetDir, out var action);
        if (!changed)
        {
            Console.WriteLine($"  ✓ {label} — MAF Doctor server entry already current");
            return;
        }

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        try
        {
            SafeWorkspaceWriter.WriteAtomic(targetDir, resolvedPath, json + Environment.NewLine);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"  ⚠ {label} — refused: {ex.Message}");
            return;
        }
        Console.WriteLine($"  ✓ {label} — {action}");
    }

    internal static bool UpsertCanonicalMcpEntry(JsonObject servers, bool includeType, string targetDir, out string action)
    {
        var changed = false;
        action = "updated MAF Doctor (maf-doctor) global-tool server entry";

        JsonObject entry;
        if (servers.TryGetPropertyValue("maf-doctor", out var existing) && existing is JsonObject existingObject)
        {
            entry = existingObject;
        }
        else if (servers.TryGetPropertyValue("maf-autopilot", out var legacy) && legacy is JsonObject legacyObject)
        {
            servers.Remove("maf-autopilot");
            entry = legacyObject;
            servers["maf-doctor"] = entry;
            changed = true;
            action = "migrated legacy maf-autopilot entry to maf-doctor and refreshed it";
        }
        else
        {
            entry = new JsonObject();
            servers["maf-doctor"] = entry;
            changed = true;
            action = "added MAF Doctor (maf-doctor) global-tool server entry";
        }

        if (includeType)
        {
            changed |= SetString(entry, "type", "stdio");
        }
        else if (entry.Remove("type"))
        {
            changed = true;
        }

        changed |= SetString(entry, "command", "maf-doctor");
        changed |= SetEmptyArray(entry, "args");
        changed |= SetInitEnv(entry, targetDir);

        return changed;
    }

    private static bool SetString(JsonObject obj, string propertyName, string value)
    {
        if (obj.TryGetPropertyValue(propertyName, out var existing) &&
            existing is JsonValue existingValue &&
            existingValue.TryGetValue<string>(out var existingString) &&
            string.Equals(existingString, value, StringComparison.Ordinal))
        {
            return false;
        }

        obj[propertyName] = value;
        return true;
    }

    private static bool SetEmptyArray(JsonObject obj, string propertyName)
    {
        if (obj.TryGetPropertyValue(propertyName, out var existing) &&
            existing is JsonArray existingArray &&
            existingArray.Count == 0)
        {
            return false;
        }

        obj[propertyName] = new JsonArray();
        return true;
    }

    private static bool SetInitEnv(JsonObject entry, string targetDir)
    {
        var changed = false;
        JsonObject env;
        if (entry.TryGetPropertyValue("env", out var existing) && existing is JsonObject existingEnv)
        {
            env = existingEnv;
        }
        else
        {
            env = new JsonObject();
            entry["env"] = env;
            changed = true;
        }

        changed |= SetString(env, InitVersionEnvName, CurrentVersion);

        // F-04 — first-time-only: scope newly-initialized workspaces to
        // themselves by default. Deliberately does NOT overwrite an existing
        // value on re-init — a user may have broadened this to cover multiple
        // repositories, and re-running `init` must not silently narrow it back.
        if (!env.ContainsKey(WorkspacePolicy.WorkspaceRootsEnvName))
        {
            env[WorkspacePolicy.WorkspaceRootsEnvName] = targetDir;
            changed = true;
        }

        return changed;
    }

    internal static InitStatus GetWorkspaceInitStatus(string targetDir)
    {
        var findings = new List<string>();

        InspectMcpConfig(
            Path.Combine(targetDir, ".vscode", "mcp.json"),
            serversKey: "servers",
            includeType: true,
            label: ".vscode/mcp.json",
            findings);
        InspectMcpConfig(
            Path.Combine(targetDir, ".mcp.json"),
            serversKey: "mcpServers",
            includeType: false,
            label: ".mcp.json",
            findings);

        RequireFile(targetDir, ".github/instructions/maf-doctor.instructions.md", findings);
        RequireFile(targetDir, ".claude/maf-doctor.md", findings);
        RequireText(targetDir, "CLAUDE.md", "@.claude/maf-doctor.md", findings);
        RequireText(targetDir, "AGENTS.md", "<!-- BEGIN maf-doctor", findings);

        return new InitStatus(findings.Count == 0, findings);
    }

    internal static string FindInitializedWorkspaceRoot(string startDir)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDir));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ".mcp.json")) ||
                File.Exists(Path.Combine(current.FullName, ".vscode", "mcp.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(startDir);
    }

    private static void InspectMcpConfig(
        string path,
        string serversKey,
        bool includeType,
        string label,
        List<string> findings)
    {
        if (!File.Exists(path))
        {
            findings.Add($"{label} is missing");
            return;
        }

        var info = new FileInfo(path);
        if (info.Length > McpJsonMaxBytes)
        {
            findings.Add($"{label} exceeds the 1 MB safety cap and should be repaired by init");
            return;
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path), nodeOptions: null, documentOptions: JsoncOptions)!.AsObject();
        }
        catch
        {
            findings.Add($"{label} cannot be parsed and should be repaired by init");
            return;
        }

        if (root[serversKey] is not JsonObject servers)
        {
            findings.Add($"{label} has no {serversKey} object");
            return;
        }

        if (servers.ContainsKey("maf-autopilot"))
            findings.Add($"{label} still uses the legacy maf-autopilot server key");

        if (servers["maf-doctor"] is not JsonObject entry)
        {
            findings.Add($"{label} has no maf-doctor server entry");
            return;
        }

        if (includeType && !StringPropertyEquals(entry, "type", "stdio"))
            findings.Add($"{label} maf-doctor entry should declare type=stdio");
        if (!includeType && entry.ContainsKey("type"))
            findings.Add($"{label} maf-doctor entry should omit type for Claude Code");
        if (!StringPropertyEquals(entry, "command", "maf-doctor"))
            findings.Add($"{label} maf-doctor entry should run command=maf-doctor");
        if (entry["args"] is not JsonArray args || args.Count != 0)
            findings.Add($"{label} maf-doctor entry should use an empty args array");
        if (entry["env"] is not JsonObject env || !StringPropertyEquals(env, InitVersionEnvName, CurrentVersion))
            findings.Add($"{label} was initialized by an older maf-doctor; run init to refresh it");
    }

    private static bool StringPropertyEquals(JsonObject obj, string propertyName, string expected)
        => obj[propertyName] is JsonValue value &&
           value.TryGetValue<string>(out var actual) &&
           string.Equals(actual, expected, StringComparison.Ordinal);

    private static void RequireFile(string targetDir, string relativePath, List<string> findings)
    {
        if (!File.Exists(Path.Combine(targetDir, relativePath)))
            findings.Add($"{relativePath} is missing");
    }

    private static void RequireText(string targetDir, string relativePath, string expectedText, List<string> findings)
    {
        var path = Path.Combine(targetDir, relativePath);
        if (!File.Exists(path))
        {
            findings.Add($"{relativePath} is missing");
            return;
        }

        var info = new FileInfo(path);
        if (info.Length > MarkerFileMaxBytes)
        {
            findings.Add($"{relativePath} exceeds the 1 MB marker-scan safety cap; run init to refresh the managed marker manually");
            return;
        }

        var content = File.ReadAllText(path);
        if (!content.Contains(expectedText, StringComparison.Ordinal))
            findings.Add($"{relativePath} does not contain the maf-doctor managed marker");
    }

    internal sealed record InitStatus(bool IsCurrent, IReadOnlyList<string> Findings)
    {
        public bool NeedsRefresh => !IsCurrent;
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
    /// A short HTML-comment note warning that a sidecar is fully overwritten on every
    /// re-`init`, with a pointer to where to put durable hand-authored guidance instead.
    /// The embedded steering body has its own leading comment stripped before delivery
    /// (see <see cref="StripLeadingHtmlComment"/>), so without this the shipped file
    /// carries no warning at all — a hand-edit here is silently wiped on the next `init`.
    /// </summary>
    private static string ManagedSidecarNote(string durableAlternative) =>
        $"<!-- Managed by `maf-doctor init` — this file is fully overwritten on every " +
        $"re-run. For durable, hand-authored guidance, use {durableAlternative} instead " +
        $"(init never touches it). -->\n\n";

    /// <summary>
    /// Writes a steering sidecar from an embedded snippet, OVERWRITING any prior copy
    /// (re-running init refreshes the guidance). The snippet's leading HTML-comment
    /// header is stripped; optional convention-specific frontmatter is prepended.
    /// </summary>
    internal static Task WriteSidecarAsync(
        string targetDir, string embeddedResourceName, string outputRelativePath, string? frontmatter = null)
    {
        var body = ReadSteeringBody(embeddedResourceName);
        if (body is null)
        {
            Console.WriteLine($"  ⚠ Steering resource '{embeddedResourceName}' not found in assembly — skipped");
            return Task.CompletedTask;
        }

        var outputPath = Path.Combine(targetDir, outputRelativePath);
        var content = (frontmatter ?? string.Empty) + body;
        if (!content.EndsWith("\n", StringComparison.Ordinal)) content += "\n";

        try
        {
            SafeWorkspaceWriter.WriteAtomic(targetDir, outputPath, content);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"  ⚠ {outputRelativePath} — refused: {ex.Message}");
            return Task.CompletedTask;
        }
        Console.WriteLine($"  ✓ {outputRelativePath} — written (refreshed on each init)");
        return Task.CompletedTask;
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
        var sidecarContent = ManagedSidecarNote("your own CLAUDE.md body") + body;
        if (!sidecarContent.EndsWith("\n", StringComparison.Ordinal)) sidecarContent += "\n";
        try
        {
            SafeWorkspaceWriter.WriteAtomic(targetDir, sidecarPath, sidecarContent);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"  ⚠ .claude/maf-doctor.md — refused: {ex.Message}");
            return;
        }
        Console.WriteLine("  ✓ .claude/maf-doctor.md — written (refreshed on each init)");

        // Ensure CLAUDE.md imports it (one stable line; add only if absent).
        const string importLine = "@.claude/maf-doctor.md";
        const string managedComment = "<!-- MAF Doctor steering — managed by `maf-doctor init`; .claude/maf-doctor.md is overwritten on re-run -->";
        var claudePath = Path.Combine(targetDir, "CLAUDE.md");

        string resolvedClaudePath;
        try
        {
            resolvedClaudePath = PathGuard.ValidateContainment(targetDir, claudePath, "CLAUDE.md");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"  ⚠ CLAUDE.md — refused: {ex.Message}");
            return;
        }

        if (File.Exists(resolvedClaudePath))
        {
            var existing = await File.ReadAllTextAsync(resolvedClaudePath);
            if (existing.Contains(importLine, StringComparison.Ordinal))
            {
                Console.WriteLine("  ✓ CLAUDE.md — already imports MAF Doctor steering, no change");
                return;
            }
            var sep = existing.Length == 0 || existing.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : "\n";
            SafeWorkspaceWriter.WriteAtomic(targetDir, resolvedClaudePath, existing + $"{sep}\n{managedComment}\n{importLine}\n");
            Console.WriteLine("  ✓ CLAUDE.md — added @import for MAF Doctor steering");
        }
        else
        {
            SafeWorkspaceWriter.WriteAtomic(targetDir, resolvedClaudePath, $"{managedComment}\n{importLine}\n");
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
        string resolvedPath;
        try
        {
            resolvedPath = PathGuard.ValidateContainment(targetDir, outputPath, outputRelativePath);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"  ⚠ {outputRelativePath} — refused: {ex.Message}");
            return;
        }

        string finalContent;
        string verb;
        if (File.Exists(resolvedPath))
        {
            var existing = await File.ReadAllTextAsync(resolvedPath);
            var bi = existing.IndexOf(begin, StringComparison.Ordinal);
            var ei = existing.IndexOf(end, StringComparison.Ordinal);
            if (bi >= 0 && ei > bi)
            {
                finalContent = existing[..bi] + block + existing[(ei + end.Length)..];
                verb = "refreshed managed maf-doctor block";
            }
            else
            {
                var sep = existing.Length == 0 || existing.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : "\n";
                finalContent = existing + $"{sep}\n{block}\n";
                verb = "added managed maf-doctor block";
            }
        }
        else
        {
            finalContent = block + "\n";
            verb = "created with managed maf-doctor block";
        }

        SafeWorkspaceWriter.WriteAtomic(targetDir, resolvedPath, finalContent);
        Console.WriteLine($"  ✓ {outputRelativePath} — {verb}");
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
