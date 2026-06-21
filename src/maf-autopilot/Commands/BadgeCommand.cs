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
        var doctorReport = new DoctorTool().MafDoctor(repoPath);
        var grade = ExtractGrade(doctorReport) ?? "?";
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
    /// Extract the bold grade letter from the doctor's markdown:
    /// "## 🟢 MAF health grade: **A**" → "A".
    /// </summary>
    internal static string? ExtractGrade(string doctorReport)
    {
        var match = Regex.Match(doctorReport, @"MAF health grade: \*\*([A-F?])\*\*");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Map A/B/C/D/F → shields.io named colours. Anything else → grey.
    /// </summary>
    internal static string ColorForGrade(string grade) => grade switch
    {
        "A" => "brightgreen",
        "B" => "green",
        "C" => "yellow",
        "D" => "orange",
        "F" => "red",
        _   => "lightgrey",
    };
}
