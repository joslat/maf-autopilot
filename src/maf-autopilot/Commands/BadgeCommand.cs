using System.Text.Json;
using System.Text.RegularExpressions;
using MafDoctor.Tools;

namespace MafDoctor.Commands;

/// <summary>
/// Phase W.13 — `maf-doctor badge &lt;repoPath&gt;` subcommand.
///
/// Runs <see cref="DoctorTool.MafDoctor"/>, extracts the grade, and emits
/// a JSON document conforming to the
/// <a href="https://shields.io/badges/endpoint-badge">shields.io endpoint
/// badge schema</a> so consumers can show a live "MAF Health: A" badge
/// in their README.
///
/// **Hosting the JSON** is Phase X.4 (out of scope here — needs an
/// account / Cloudflare Worker / Gist). The subcommand just emits the
/// JSON to stdout; the maintainer wires it into whatever endpoint they
/// want shields.io to fetch.
///
/// Output (example):
/// <code>
/// { "schemaVersion": 1, "label": "MAF Health", "message": "A", "color": "brightgreen" }
/// </code>
/// </summary>
public static class BadgeCommand
{
    /// <summary>
    /// Run the doctor against <paramref name="repoPath"/>, extract the
    /// grade, and return the shields.io endpoint-badge JSON.
    /// </summary>
    public static string Build(string repoPath)
    {
        // REP-39: consume the machine-readable `verdict` from the schema_version-1 JSON
        // contract instead of regex-scraping the markdown headline — the badge no longer
        // breaks when the report prose changes, and it rides the single-source grade.
        var json = new DoctorTool().Run(repoPath, "json", excludes: null);
        var grade = ExtractVerdict(json) ?? "?";
        var color = ColorForGrade(grade);

        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            label = "MAF Health",
            message = grade,
            color,
        });
    }

    /// <summary>
    /// Read the single-source grade from the doctor's <c>--json</c> <c>verdict</c> field.
    /// Returns <see langword="null"/> when the doctor reported an error (no verdict) or the
    /// JSON is unparseable — the caller then renders "?" (lightgrey).
    /// </summary>
    internal static string? ExtractVerdict(string doctorJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(doctorJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (root.TryGetProperty("error", out _)) return null;
            return root.TryGetProperty("verdict", out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Legacy markdown grade extractor, retained for callers/tests that still parse the
    /// headline. Production <see cref="Build"/> now uses <see cref="ExtractVerdict"/>.
    /// "## 🟢 MAF health grade: **A**" → "A".
    /// </summary>
    internal static string? ExtractGrade(string doctorReport)
    {
        var match = Regex.Match(doctorReport, @"MAF health grade: \*\*([A-F?])\*\*");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Map the grade to a shields.io named colour, aligned with the doctor header's
    /// grade-emoji scale (REP-38): one colour semantic per grade across every surface.
    /// Grade D is unreachable (the rubric only emits A/B/C/F), so there is no D arm —
    /// anything unrecognized falls to grey.
    /// </summary>
    internal static string ColorForGrade(string grade) => grade switch
    {
        "A" => "brightgreen",
        "B" => "yellow",
        "C" => "orange",
        "F" => "red",
        _   => "lightgrey",
    };
}
