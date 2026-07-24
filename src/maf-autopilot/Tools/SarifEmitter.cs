using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MafDoctor.Tools;

/// <summary>
/// Emits SARIF v2.1.0 documents for the maf-doctor scanners. SARIF is the
/// format consumed by GitHub code-scanning / Advanced Security and the VS Code
/// Problems panel — unlocking these integrations is plan #58.
///
/// Schema: https://docs.oasis-open.org/sarif/sarif/v2.1.0/cs01/sarif-v2.1.0-cs01.html
/// </summary>
internal static class SarifEmitter
{
    public const string ToolName = "maf-doctor";
    public const string ToolUri = "https://github.com/joslat/maf-doctor";

    /// <summary>
    /// Builds a SARIF document for a collection of normalised findings.
    /// </summary>
    public static string Emit(
        string toolVersion,
        IEnumerable<SarifFinding> findings,
        IEnumerable<SarifRule>? ruleCatalog = null,
        string? invocationError = null)
    {
        var rules = (ruleCatalog ?? []).ToList();
        var results = findings.ToList();

        var run = new JsonObject
        {
            ["tool"] = new JsonObject
            {
                ["driver"] = new JsonObject
                {
                    ["name"] = ToolName,
                    ["version"] = toolVersion,
                    ["informationUri"] = ToolUri,
                    ["rules"] = BuildRules(rules),
                },
            },
            ["results"] = BuildResults(results),
        };

        // REP-22: carry a validation/processing error through SARIF's own channel
        // (an invocation with executionSuccessful:false + a toolExecutionNotification)
        // so a machine consumer requesting SARIF still receives parseable SARIF — the
        // same error-shape guarantee the doctor's JSON formats already provide — rather
        // than a bare plain-text error string that breaks their parser.
        if (invocationError is not null)
        {
            run["invocations"] = new JsonArray(new JsonObject
            {
                ["executionSuccessful"] = false,
                ["toolExecutionNotifications"] = new JsonArray(new JsonObject
                {
                    ["level"] = "error",
                    ["message"] = new JsonObject { ["text"] = invocationError },
                }),
            });
        }

        var doc = new JsonObject
        {
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"] = new JsonArray(run),
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        return doc.ToJsonString(options);
    }

    private static JsonArray BuildRules(IReadOnlyList<SarifRule> rules)
    {
        var arr = new JsonArray();
        foreach (var r in rules)
        {
            var node = new JsonObject
            {
                ["id"] = r.Id,
                ["name"] = r.Name,
                ["shortDescription"] = new JsonObject { ["text"] = r.Name },
                ["fullDescription"] = new JsonObject { ["text"] = r.FullDescription ?? r.Name },
                ["defaultConfiguration"] = new JsonObject { ["level"] = LevelFromSeverity(r.Severity) },
            };
            if (!string.IsNullOrWhiteSpace(r.HelpUri))
                node["helpUri"] = r.HelpUri;
            // REP-11: GitHub renders help.markdown in the alert detail pane (help.text
            // is the plain-text fallback). Carries the same Why/Fix guidance the CLI prints.
            if (!string.IsNullOrWhiteSpace(r.HelpMarkdown))
                node["help"] = new JsonObject
                {
                    ["text"] = r.HelpMarkdown,
                    ["markdown"] = r.HelpMarkdown,
                };
            arr.Add(node);
        }
        return arr;
    }

    private static JsonArray BuildResults(IReadOnlyList<SarifFinding> findings)
    {
        var arr = new JsonArray();
        foreach (var f in findings)
        {
            arr.Add(new JsonObject
            {
                ["ruleId"] = f.RuleId,
                ["level"] = LevelFromSeverity(f.Severity),
                ["message"] = new JsonObject { ["text"] = f.Message },
                ["locations"] = new JsonArray(new JsonObject
                {
                    ["physicalLocation"] = new JsonObject
                    {
                        ["artifactLocation"] = new JsonObject
                        {
                            ["uri"] = NormaliseUri(f.File),
                        },
                        ["region"] = new JsonObject
                        {
                            ["startLine"] = f.Line,
                        },
                    },
                }),
                // REP-12: a stable per-result identity so code-scanning alerts don't churn
                // when line numbers shift. Keyed by a custom versioned name (non-GitHub SARIF
                // consumers can dedup on it too); the hash covers ruleId + file + the trimmed
                // match/line text, so a line MOVE keeps the fingerprint while a content edit
                // changes it. Falls back to the line number when no text is available.
                ["partialFingerprints"] = new JsonObject
                {
                    ["mafDoctorFingerprint/v1"] = Fingerprint(f),
                },
            });
        }
        return arr;
    }

    private static string Fingerprint(SarifFinding f)
    {
        var tail = string.IsNullOrWhiteSpace(f.LineText)
            ? f.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Join(' ', f.LineText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{f.RuleId}|{NormaliseUri(f.File)}|{tail}"));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant(); // 16 hex chars
    }

    private static string LevelFromSeverity(SarifSeverity severity) => severity switch
    {
        SarifSeverity.Error => "error",
        SarifSeverity.Warning => "warning",
        SarifSeverity.Note => "note",
        _ => "none",
    };

    /// <summary>
    /// SARIF expects forward-slashed relative URIs.
    /// </summary>
    private static string NormaliseUri(string path) =>
        path.Replace('\\', '/');
}

internal enum SarifSeverity { Error, Warning, Note }

internal sealed record SarifFinding(
    string RuleId,
    SarifSeverity Severity,
    string Message,
    string File,
    int Line,
    // REP-12: the offending source/match text, used to compute a drift-stable
    // partialFingerprint. Null → the fingerprint falls back to ruleId+file+line.
    string? LineText = null);

internal sealed record SarifRule(
    string Id,
    string Name,
    SarifSeverity Severity,
    string? FullDescription = null,
    string? HelpUri = null,
    // REP-11: markdown Why/Fix guidance rendered in the code-scanning alert detail pane.
    string? HelpMarkdown = null);
