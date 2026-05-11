using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MafAutopilot.Tools;

/// <summary>
/// Emits SARIF v2.1.0 documents for the maf-autopilot scanners. SARIF is the
/// format consumed by GitHub code-scanning / Advanced Security and the VS Code
/// Problems panel — unlocking these integrations is plan #58.
///
/// Schema: https://docs.oasis-open.org/sarif/sarif/v2.1.0/cs01/sarif-v2.1.0-cs01.html
/// </summary>
internal static class SarifEmitter
{
    public const string ToolName = "maf-autopilot";
    public const string ToolUri = "https://github.com/joslat/maf-autopilot";

    /// <summary>
    /// Builds a SARIF document for a collection of normalised findings.
    /// </summary>
    public static string Emit(
        string toolVersion,
        IEnumerable<SarifFinding> findings,
        IEnumerable<SarifRule>? ruleCatalog = null)
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
            });
        }
        return arr;
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
    int Line);

internal sealed record SarifRule(
    string Id,
    string Name,
    SarifSeverity Severity,
    string? FullDescription = null,
    string? HelpUri = null);
