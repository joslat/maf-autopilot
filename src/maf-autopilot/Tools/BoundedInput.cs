using System.Text;

namespace MafDoctor.Tools;

/// <summary>
/// Length-cap guard for user-controlled string inputs entering an MCP tool.
/// Without bounds, an LLM-supplied 1 GB <c>snippet</c> or <c>instructions</c>
/// parameter is a trivial DoS lane against the MCP server process.
///
/// <para>Anchored to:</para>
/// <list type="bullet">
///   <item>OWASP LLM10 — Unbounded Consumption.</item>
///   <item>v1.1 security hardening plan §5.6 (the cap table below).</item>
/// </list>
///
/// <para><b>Cap reference table</b> (use the named constants, not raw numbers):</para>
/// <list type="bullet">
///   <item><see cref="PathBytes"/> = 4 KB — OS MAX_PATH + slack.</item>
///   <item><see cref="IdentifierBytes"/> = 256 B — package id, version, type
///         name, agent / executor name, etc.</item>
///   <item><see cref="ShortTextBytes"/> = 16 KB — symptom, error message,
///         expected/actual one-screen fields.</item>
///   <item><see cref="SnippetBytes"/> = 256 KB — largest sensible single-file
///         code fragment a user would paste.</item>
///   <item><see cref="InstructionsBytes"/> = 64 KB — system-prompt-class text
///         (agent <c>instructions</c>).</item>
///   <item><see cref="AggregateBytes"/> = 500 MB — authoritative multi-file
///         repo-scan aggregate ceiling; <c>SourceFileWalker.ScanBudget.MaxTotalBytes</c>
///         defaults to this constant so the cap has a single source of truth.</item>
/// </list>
///
/// <para>Usage: call <see cref="Validate"/> at the top of every MCP tool
/// method body, before any I/O or string manipulation:</para>
/// <code>
/// public string MafDraftIssue(string symptom, string? snippet = null)
/// {
///     BoundedInput.Validate(symptom,  BoundedInput.ShortTextBytes, nameof(symptom));
///     BoundedInput.Validate(snippet,  BoundedInput.SnippetBytes,   nameof(snippet));
///     // ... rest of the method
/// }
/// </code>
///
/// <para>Behavior: on exceeding the cap, throws <see cref="ArgumentException"/>
/// with a parameter-named message that does NOT echo the input (consistent
/// with <see cref="PathGuard.ValidateContainment"/> — error reporting telemetry,
/// if added later, should not leak input data). Null / empty values are
/// permitted and pass through unchanged; the caller decides whether
/// emptiness is a separate error condition.</para>
/// </summary>
internal static class BoundedInput
{
    public const int PathBytes         =          4 * 1024;
    public const int IdentifierBytes   =                256;
    public const int ShortTextBytes    =         16 * 1024;
    public const int SnippetBytes      =        256 * 1024;
    public const int InstructionsBytes =         64 * 1024;
    // WM-40: was 100 MB and referenced by nothing but a value-assert test — the
    // real repo-scan aggregate cap lived in ScanBudget.MaxTotalBytes (500 MB) with a
    // different value. Now this is the one authoritative constant that ScanBudget reads.
    public const long AggregateBytes   = 500L * 1024 * 1024;

    /// <summary>
    /// Validate that <paramref name="value"/> is at most <paramref name="maxBytes"/>
    /// when encoded as UTF-8. Returns the input unchanged on success; throws
    /// <see cref="ArgumentException"/> on excess.
    ///
    /// Null / empty values are permitted (return immediately) — emptiness is
    /// a separate concern the caller handles per parameter.
    /// </summary>
    public static string? Validate(string? value, int maxBytes, string paramName)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (maxBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxBytes), "maxBytes must be positive.");

        // UTF-8 byte count, not char count. Char count would underbound on
        // multi-byte glyphs (CJK, emoji); byte count is what bounds the
        // downstream memory footprint and any future LLM context budget.
        var byteLen = Encoding.UTF8.GetByteCount(value);
        if (byteLen > maxBytes)
        {
            // Non-leaky error: name the parameter + cap, not the input.
            throw new ArgumentException(
                $"{paramName} exceeds the maximum allowed length of {maxBytes} bytes ({byteLen} bytes supplied).",
                paramName);
        }
        return value;
    }
}
