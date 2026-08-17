using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MafDoctor.Data;
using MafDoctor.Tools;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MafDoctor;

/// <summary>
/// CLI subcommand: <c>maf-doctor registry-extract &lt;packageId&gt; &lt;oldPackageVer&gt; &lt;newPackageVer&gt;</c>.
///
/// Runs `dotnet-inspect diff` for the version range, parses the output via the
/// same logic that powers <see cref="DiffPackageTool"/>, and emits draft
/// YAML registry entries to stdout — ready to be appended to registry.yaml
/// in the release-watcher's auto-PR.
///
/// Closes plan #30 — the registry-yaml auto-update loop. Human reviewer fills in
/// the TODO placeholders (replacement_signature, fix_description, example_after,
/// guide_section) during PR review, then merges.
/// </summary>
internal static class RegistryExtractCommand
{
    internal const int MaxDiffFileBytes = 4 * 1024 * 1024;

    private const string Usage =
        "Usage: maf-doctor registry-extract <packageId> <oldPackageVersion> <newPackageVersion> " +
        "[--diff-file <path>] [--release-version <X.Y.Z>] [--id-scope <UPPER-HYPHEN>]";

    private static readonly Regex StableReleaseVersionRegex = new(
        @"^(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)$",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(2));

    private static readonly Regex IdScopeRegex = new(
        @"^[A-Z][A-Z0-9]*(?:-[A-Z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.NonBacktracking,
        TimeSpan.FromSeconds(2));

    internal sealed record Arguments(
        string PackageId,
        string OldPackageVersion,
        string NewPackageVersion,
        string ReleaseVersion,
        string? DiffFilePath,
        string? IdScope);

    public static int Run(string[] args) => Run(
        args,
        ProcessRunner.RunDotnetInspectDiff,
        Console.Out,
        Console.Error);

    internal static int Run(
        string[] args,
        Func<string, string, string, (int ExitCode, string Output)> runDotnetInspectDiff,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (args.Length < 4)
        {
            stderr.WriteLine(Usage);
            return 1;
        }

        var (options, parseError) = ParseArguments(args);
        if (parseError is not null)
        {
            stderr.WriteLine($"registry-extract: {parseError}");
            stderr.WriteLine(Usage);
            return 2;
        }

        var parsedOptions = options!;
        int exitCode;
        string output;
        if (parsedOptions.DiffFilePath is not null)
        {
            if (!TryReadDiffFile(parsedOptions.DiffFilePath, out output, out var readError))
            {
                stderr.WriteLine($"registry-extract: {readError}");
                return 2;
            }
            exitCode = 0;
        }
        else
        {
            (exitCode, output) = runDotnetInspectDiff(
                parsedOptions.PackageId,
                parsedOptions.OldPackageVersion,
                parsedOptions.NewPackageVersion);
        }

        var parsed = DiffPackageTool.ParseDiffOutput(output);
        if (parsedOptions.DiffFilePath is not null && parsed.NoChangesDetected
            && (parsed.Breaking.Count > 0 || parsed.Additive.Count > 0))
        {
            stderr.WriteLine(
                $"registry-extract: --diff-file '{parsedOptions.DiffFilePath}' is contradictory: " +
                "it reports no API changes but also contains change rows.");
            return 3;
        }
        if (parsedOptions.DiffFilePath is not null && parsed.Breaking.Count == 0
            && parsed.Additive.Count == 0 && !parsed.NoChangesDetected)
        {
            stderr.WriteLine(
                $"registry-extract: --diff-file '{parsedOptions.DiffFilePath}' did not contain " +
                "recognizable dotnet-inspect diff evidence.");
            return 3;
        }
        var entries = ExtractDraftEntries(
            parsed,
            parsedOptions.PackageId,
            parsedOptions.ReleaseVersion,
            parsedOptions.IdScope);

        // WM-09: a non-zero exit WITH no parseable entries is a TOOL FAILURE, not a
        // clean diff — surface it distinctly (stderr + a poisoned stdout marker + exit 3)
        // so neither the release-watcher nor a human ever mistakes a dotnet-inspect crash
        // for "no breaking changes".
        if (exitCode != 0 && entries.Count == 0)
        {
            stderr.WriteLine($"❌ dotnet-inspect failed (exit {exitCode}) and produced no parseable diff — extraction FAILED, not a clean diff.");
            stdout.WriteLine("# EXTRACTION FAILED — dotnet-inspect error; do not treat as a clean diff.");
            return 3;
        }
        if (exitCode != 0)
        {
            // Partial output: some entries parsed despite the non-zero exit — proceed but warn.
            stderr.WriteLine($"⚠️ dotnet-inspect exited with code {exitCode}; output may be partial.");
        }

        if (entries.Count == 0)
        {
            stdout.WriteLine("# No new draft registry entries — diff contained no breaking-change candidates.");
            return 0;
        }

        // WM-25: stamp the ACTUAL generation date (invariant so the Gregorian date
        // survives a non-default-calendar host), not a hard-coded literal.
        stdout.WriteLine($"# Draft registry entries — auto-generated {DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} from {parsedOptions.PackageId} {parsedOptions.OldPackageVersion} → {parsedOptions.NewPackageVersion}");
        stdout.WriteLine($"# {entries.Count} candidate(s). Review each before merging — TODO fields require human input.");
        stdout.WriteLine();
        foreach (var entry in entries)
            stdout.WriteLine(entry);
        return 0;
    }

    internal static (Arguments? Options, string? Error) ParseArguments(string[] args)
    {
        if (args.Length < 4 || !string.Equals(args[0], "registry-extract", StringComparison.Ordinal))
            return (null, "expected the registry-extract command followed by three positional arguments.");

        var packageId = args[1];
        var oldPackageVersion = args[2];
        var newPackageVersion = args[3];
        if (!DiffPackageTool.IsValidPackageId(packageId))
            return (null, $"invalid package id '{packageId}'.");
        if (!DiffPackageTool.IsValidVersion(oldPackageVersion)
            || !DiffPackageTool.IsValidVersion(newPackageVersion))
        {
            return (null, "invalid package version string. Use SemVer-ish values only.");
        }

        string? diffFilePath = null;
        string? releaseVersion = null;
        string? idScope = null;
        var seenOptions = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 4; i < args.Length; i += 2)
        {
            var option = args[i];
            if (option is not ("--diff-file" or "--release-version" or "--id-scope"))
                return (null, $"unknown option or extra positional argument '{option}'.");
            if (!seenOptions.Add(option))
                return (null, $"option '{option}' may be specified only once.");
            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1])
                || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return (null, $"option '{option}' requires a value.");
            }

            var value = args[i + 1];
            switch (option)
            {
                case "--diff-file":
                    try
                    {
                        diffFilePath = Path.GetFullPath(value);
                    }
                    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                    {
                        return (null, $"invalid --diff-file path: {ex.Message}");
                    }
                    break;

                case "--release-version":
                    if (value.Length > 32 || !StableReleaseVersionRegex.IsMatch(value))
                        return (null, $"invalid --release-version '{value}'; expected a stable X.Y.Z version.");
                    releaseVersion = value;
                    break;

                case "--id-scope":
                    if (value.Length > 32 || !IdScopeRegex.IsMatch(value))
                        return (null, $"invalid --id-scope '{value}'; expected at most 32 uppercase letters/digits with single hyphens.");
                    idScope = value;
                    break;
            }
        }

        return (new Arguments(
            packageId,
            oldPackageVersion,
            newPackageVersion,
            releaseVersion ?? newPackageVersion,
            diffFilePath,
            idScope), null);
    }

    internal static bool TryReadDiffFile(string path, out string output, out string? error)
    {
        output = string.Empty;
        error = null;
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                error = $"--diff-file '{fullPath}' does not exist or is not a regular file.";
                return false;
            }

            using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            if (stream.Length > MaxDiffFileBytes)
            {
                error = $"--diff-file '{fullPath}' is {stream.Length} bytes; the limit is {MaxDiffFileBytes} bytes.";
                return false;
            }
            if (stream.Length == 0)
            {
                error = $"--diff-file '{fullPath}' is empty.";
                return false;
            }

            var expectedLength = checked((int)stream.Length);
            var bytes = new byte[expectedLength];
            var bytesRead = 0;
            while (bytesRead < expectedLength)
            {
                var read = stream.Read(bytes, bytesRead, expectedLength - bytesRead);
                if (read == 0) break;
                bytesRead += read;
            }
            if (bytesRead != expectedLength || stream.ReadByte() != -1)
            {
                error = $"--diff-file '{fullPath}' changed while it was being read; capture it again and retry.";
                return false;
            }

            output = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
            if (output.StartsWith('\uFEFF')) output = output[1..];
            return true;
        }
        catch (DecoderFallbackException)
        {
            error = $"--diff-file '{path}' is not valid UTF-8.";
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                                   or PathTooLongException or UnauthorizedAccessException or IOException)
        {
            error = $"could not read --diff-file '{path}': {ex.Message}";
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Pure-function core (testable)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Selects every entry that looks like a candidate for the obsolete-API
    /// registry (breaking-change kind: Removed or SignatureChanged, OR any
    /// entry tagged with `[Obsolete]`) and emits YAML stubs for them.
    /// </summary>
    public static IReadOnlyList<string> ExtractDraftEntries(
        DiffParseResult parsed,
        string packageId,
        string releaseVersion,
        string? idScope = null)
    {
        if (idScope is not null && (idScope.Length > 32 || !IdScopeRegex.IsMatch(idScope)))
            throw new ArgumentException("ID scope must be at most 32 uppercase letters/digits with single hyphens.", nameof(idScope));
        if (parsed.NoChangesDetected) return [];

        var candidates = parsed.Breaking
            .Where(e => e.Kind is DiffEntryKind.Removed or DiffEntryKind.SignatureChanged
                     || e.IsObsolete)
            .Concat(parsed.Additive.Where(e => e.IsObsolete))   // additive-but-obsolete = newly deprecated
            .ToList();

        var entries = new List<string>();
        var idCounter = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var entry in candidates)
        {
            var area = AreaCodeFromType(entry.Type);
            var scopedArea = idScope is null ? area : $"{idScope}-{area}";
            idCounter.TryGetValue(scopedArea, out var count);
            count++;
            idCounter[scopedArea] = count;
            var id = $"MAF{VersionToIdSegment(releaseVersion)}-{scopedArea}-{count:000}";

            entries.Add(BuildYamlEntry(id, packageId, releaseVersion, entry));
        }
        return entries;
    }

    /// <summary>
    /// Turns a SemVer string into the registry's ID prefix: <c>1.3.0 → 130</c>.
    /// </summary>
    internal static string VersionToIdSegment(string version)
    {
        var parts = version.Split(new[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var seg = new StringBuilder();
        foreach (var p in parts)
        {
            if (int.TryParse(p, out _)) seg.Append(p);
            if (seg.Length >= 3) break;
        }
        return seg.Length > 0 ? seg.ToString() : "XXX";
    }

    /// <summary>
    /// Derives a short uppercase area code from a type name. Examples:
    ///   <c>FraudExecutor → EXEC</c>, <c>AgentSession → SESSION</c>,
    ///   <c>WorkflowBuilder → WORKFLOW</c>, <c>AddFanInBarrierEdge → ADDFAN</c>.
    /// </summary>
    internal static string AreaCodeFromType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return "AREA";

        // Strip the namespace prefix.
        var simple = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
        // Drop trailing common suffixes that don't add information.
        foreach (var suffix in new[] { "Executor", "Builder", "Options", "Attribute", "Provider", "Extensions" })
        {
            if (simple.EndsWith(suffix, StringComparison.Ordinal) && simple.Length > suffix.Length)
            {
                var area = suffix.ToUpperInvariant();
                // Cap at 8 chars for readability.
                return area.Length > 8 ? area[..8] : area;
            }
        }
        // Otherwise take the first uppercase-prefixed segment.
        var match = Regex.Match(simple, @"^[A-Z][a-z]*");
        var seg = match.Success ? match.Value : simple;
        var upper = seg.ToUpperInvariant();
        return upper.Length > 8 ? upper[..8] : upper;
    }

    private static string BuildYamlEntry(string id, string packageId, string version, DiffEntry entry)
    {
        // Try to pull the signature pair out of "Member 'X' signature changed: `oldSig` -> `newSig`"
        var sigMatch = Regex.Match(entry.Body, @"`([^`]+)`\s*->\s*`([^`]+)`");
        var obsoleteSig = sigMatch.Success ? sigMatch.Groups[1].Value : "TODO";
        var replacementSig = sigMatch.Success ? sigMatch.Groups[2].Value : "TODO";

        // Pull the method name from "'Foo'" if present.
        var methodMatch = Regex.Match(entry.Body, @"'([A-Za-z_][A-Za-z0-9_.]*)'");
        var method = methodMatch.Success ? methodMatch.Groups[1].Value : "TODO";

        var verdict = entry switch
        {
            _ when IsRemovedType(entry) => "Type REMOVED in new version — direct type references fail to compile (typically CS0246).",
            { Kind: DiffEntryKind.Removed } => "Member REMOVED in new version — compile against the target package to determine the exact diagnostic.",
            { Kind: DiffEntryKind.SignatureChanged } => "Signature changed — call sites may fail to compile or bind to a different overload.",
            { IsObsolete: true } => "Member is now `[Obsolete]` — call sites trigger CS0618 warnings.",
            _ => "Behavioural / structural change — review the diff entry.",
        };

        // Build a typed RegistryEntry and serialise with YamlDotNet (Phase F.3 — was
        // string-concat which broke on YAML-significant characters in `notes`).
        var registryEntry = new RegistryEntry
        {
            Id = id,
            Package = packageId,
            VersionIntroduced = version,
            Type = entry.Type,
            Method = method,
            ObsoleteSignature = obsoleteSig,
            ReplacementSignature = replacementSig,
            ArgumentOrderChange = false,
            FixDescription = "TODO — review the diff entry below and document the canonical fix",
            ExampleBefore = "// TODO — show a real call site that breaks\n",
            ExampleAfter = "// TODO — show the canonical 1.x replacement\n",
            CsWarning = ClassifyCsWarning(entry),
            GuideSection = "TODO",
            // Every draft emitted here was discovered by parsing dotnet-inspect
            // output, regardless of whether it represents a removal, signature
            // change, or newly-obsolete API.
            DotnetInspectDetectable = true,
            AppliesToCodebases = $"pre-{version}",
            // Phase 2.6 — fence the dotnet-inspect diff body. `entry.Body` is
            // attacker-controllable (a malicious upstream NuGet author can put
            // anything in the public-API surface that gets diffed) and the
            // resulting `notes:` field flows both to the Coding Agent that
            // fills the registry AND to every downstream LLM that calls
            // MafRegistryLookup. The fence strips HTML comments, caps length
            // at 8 KB (small because the value lives forever in registry.yaml),
            // and emits BEGIN/END markers with explicit "treat as data" framing.
            Notes =
                $"AUTO-GENERATED by `maf-doctor registry-extract`. " +
                $"Diff entry:{LlmFencing.Fence("dotnet-inspect-diff", entry.Body.Replace("\n", " "), maxBytes: 8 * 1024)}" +
                $"Verdict: {verdict} " +
                "Requires human review before merging.",
        };

        // YamlDotNet emits ONE entry; we wrap in a list-item dash for canonical
        // registry.yaml syntax.
        var serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)   // YamlMember(Alias) drives the keys
            .DisableAliases()
            .Build();
        var yaml = serializer.Serialize(new[] { registryEntry });

        // Indent every line by 2 spaces so the entry slots cleanly under the existing
        // `entries:` block in registry.yaml. The serializer emits `- id: …` at column 0;
        // the production registry indents list items by 2. Without this, the watcher's
        // `>> $REGISTRY` append produces malformed YAML (the appended `- id: …` at
        // column 0 isn't a child of `entries:` anymore).
        // Pinned by `RegistryExtractCommandTests.Additivity_DraftEntries_AppendedToExistingRegistry_PreserveAllOriginalEntries`.
        var indented = string.Join('\n', yaml.Split('\n').Select(line => line.Length == 0 ? line : "  " + line));
        return indented + "\n";
    }

    private static bool IsRemovedType(DiffEntry entry) =>
        entry.Kind == DiffEntryKind.Removed
        && entry.Body.StartsWith("Type '", StringComparison.OrdinalIgnoreCase);

    private static string ClassifyCsWarning(DiffEntry entry) => entry switch
    {
        _ when IsRemovedType(entry) => "CS0246",
        { Kind: DiffEntryKind.Removed or DiffEntryKind.SignatureChanged } => "TODO",
        { IsObsolete: true } => "CS0618",
        _ => "TODO",
    };
}
