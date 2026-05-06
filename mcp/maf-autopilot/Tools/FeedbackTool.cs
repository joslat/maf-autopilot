using MafAutopilot.Data;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MafAutopilot.Tools;

/// <summary>
/// Feedback tool — closes the reflexive learning loop.
///
///   maf_open_feedback_issue — after a migration, analyzes the migration log via LLM sampling
///                             and generates a structured GitHub Issue draft containing new
///                             CS0618 patterns, guide corrections, and surprises to feed back
///                             into the maf-autopilot registry and guide.
/// </summary>
[McpServerToolType]
public sealed class FeedbackTool
{
    private readonly RegistryService _registry;

    public FeedbackTool(RegistryService registry)
    {
        _registry = registry;
    }

    [McpServerTool(Name = "maf_open_feedback_issue")]
    [Description("""
        Post-migration feedback loop. Analyzes the completed migration (via migration-plan.md or
        a description of surprises) and uses the host LLM to generate a structured GitHub Issue
        for the maf-autopilot repository.

        The issue captures:
          - New CS0618 patterns not yet in the registry (registry.yaml candidates)
          - Official documentation that was wrong or misleading
          - Silent runtime failures encountered (fan-out, fan-in starvation)
          - Patterns that compiled but behaved incorrectly
          - Guide section corrections or additions needed

        Output options:
          1. (Default) Returns the formatted issue markdown — paste it at
             https://github.com/joslat/maf-autopilot/issues/new
          2. (With githubToken) Posts the issue automatically via the GitHub API.

        Requires the MCP host to support sampling (VS Code Copilot with agent mode).
        """)]
    public async Task<string> MafOpenFeedbackIssue(
        RequestContext<CallToolRequestParams> context,
        [Description("Path to the completed migration-plan.md file. Used to extract what was migrated and any notes.")] string? migrationPlanPath = null,
        [Description("Free-form description of surprises, undocumented patterns, wrong fixes, or silent failures encountered during the migration.")] string? surprises = null,
        [Description("Optional GitHub personal access token (PAT) with 'issues:write' scope. If provided, posts the issue automatically. If omitted, returns the formatted issue text.")] string? githubToken = null,
        CancellationToken ct = default)
    {
        var planContent = string.Empty;
        if (migrationPlanPath is not null)
        {
            if (!File.Exists(migrationPlanPath))
                return $"Error: migration-plan.md not found at: {migrationPlanPath}";
            planContent = File.ReadAllText(migrationPlanPath);
        }

        if (string.IsNullOrWhiteSpace(planContent) && string.IsNullOrWhiteSpace(surprises))
        {
            return """
                Error: provide at least one of:
                  - migrationPlanPath: path to your completed migration-plan.md
                  - surprises: a description of what went wrong or was undocumented

                Both can be provided together for the most complete feedback issue.
                """;
        }

        var prompt = BuildFeedbackPrompt(planContent, surprises);

        string issueBody;
        try
        {
            var result = await context.Server.SampleAsync(
                new CreateMessageRequestParams
                {
                    Messages = [new SamplingMessage { Role = Role.User, Content = [new TextContentBlock { Text = prompt }] }],
                    MaxTokens = 2048,
                    SystemPrompt = FeedbackSystemPrompt,
                },
                ct);

            issueBody = ExtractText(result);
        }
        catch (Exception ex)
        {
            // Fallback: generate a template without LLM
            issueBody = BuildFallbackIssueBody(planContent, surprises);
            issueBody += $"\n\n---\n*Note: LLM sampling unavailable ({ex.Message}). This is a template — please fill in specifics.*";
        }

        // Auto-post if a GitHub token was provided
        if (!string.IsNullOrWhiteSpace(githubToken))
        {
            var (posted, url) = await PostGitHubIssueAsync(issueBody, githubToken, ct);
            if (posted)
                return $"✅ Issue posted: {url}\n\n{issueBody}";
            else
                return $"⚠️ Auto-post failed: {url}\n\nIssue content (paste manually):\n\n{issueBody}";
        }

        return $"""
            ## Feedback Issue Draft

            Paste this at: https://github.com/joslat/maf-autopilot/issues/new

            ---

            {issueBody}

            ---

            *To post automatically, re-run with a GitHub PAT (issues:write scope):*
            *  maf_open_feedback_issue ... githubToken="ghp_..."*
            """;
    }

    // -------------------------------------------------------------------------
    // Prompt builders
    // -------------------------------------------------------------------------

    private string BuildFeedbackPrompt(string planContent, string? surprises)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## MAF Migration Feedback Issue Generation");
        sb.AppendLine();
        sb.AppendLine("A MAF migration to 1.3.0 has completed. Generate a structured GitHub Issue for the");
        sb.AppendLine("maf-autopilot repository to capture learnings and improve the toolkit.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(planContent))
        {
            // Trim plan to relevant sections to stay within token limits
            var trimmed = planContent.Length > 4000
                ? planContent[..4000] + "\n...[truncated]"
                : planContent;
            sb.AppendLine("### Completed Migration Plan");
            sb.AppendLine("```markdown");
            sb.AppendLine(trimmed);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(surprises))
        {
            sb.AppendLine("### Surprises / Undocumented Issues");
            sb.AppendLine(surprises);
            sb.AppendLine();
        }

        sb.AppendLine("### Existing Registry Entries (for reference — do NOT duplicate these)");
        foreach (var id in _registry.AllIds.Take(20))
        {
            var e = _registry.FindById(id)!;
            sb.AppendLine($"  - {id}: `{e.ObsoleteSignature}`");
        }
        if (_registry.AllIds.Count > 20)
            sb.AppendLine($"  ... and {_registry.AllIds.Count - 20} more entries — full list at maf://registry");
        sb.AppendLine();

        sb.AppendLine("### Task");
        sb.AppendLine("Generate a GitHub Issue with the following structure:");
        sb.AppendLine();
        sb.AppendLine("**Title**: Post-migration feedback: [brief description of key findings]");
        sb.AppendLine();
        sb.AppendLine("**Body sections:**");
        sb.AppendLine("1. `## Summary` — 2-3 sentences summarizing what was migrated and the main learnings");
        sb.AppendLine("2. `## New CS0618 Registry Candidates` — any obsolete APIs found that are NOT in the existing registry above");
        sb.AppendLine("   For each: obsolete signature, replacement, fix description, example before/after");
        sb.AppendLine("3. `## Documentation Corrections` — where the official docs were wrong or the guide needs updating");
        sb.AppendLine("4. `## Silent Failures` — any patterns that compiled clean but failed at runtime");
        sb.AppendLine("5. `## Suggested Labels` — e.g. `registry-update`, `guide-correction`, `silent-failure`");
        sb.AppendLine();
        sb.AppendLine("Only include sections where there is actual content. Skip empty sections.");
        sb.AppendLine("Use concrete API names and code snippets where possible.");
        return sb.ToString();
    }

    private static string BuildFallbackIssueBody(string planContent, string? surprises)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("Post-migration feedback from a MAF 1.3.0 migration. [Fill in summary here]");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(surprises))
        {
            sb.AppendLine("## Surprises / Undocumented Issues");
            sb.AppendLine();
            sb.AppendLine(surprises);
            sb.AppendLine();
        }

        sb.AppendLine("## New CS0618 Registry Candidates");
        sb.AppendLine();
        sb.AppendLine("<!-- List any obsolete APIs not yet in the registry. Format:");
        sb.AppendLine("  - Obsolete: `TypeName.MethodName(params)`");
        sb.AppendLine("  - Replacement: `TypeName.NewMethod(params)`");
        sb.AppendLine("  - Fix: description of the change");
        sb.AppendLine("-->");
        sb.AppendLine();
        sb.AppendLine("## Documentation Corrections");
        sb.AppendLine();
        sb.AppendLine("<!-- Note any places where the official docs or migration guide were wrong -->");
        sb.AppendLine();
        sb.AppendLine("## Suggested Labels");
        sb.AppendLine();
        sb.AppendLine("`registry-update` `guide-correction`");

        if (!string.IsNullOrWhiteSpace(planContent))
        {
            sb.AppendLine();
            sb.AppendLine("<details><summary>Migration Plan (reference)</summary>");
            sb.AppendLine();
            sb.AppendLine("```markdown");
            var trimmed = planContent.Length > 2000 ? planContent[..2000] + "\n...[truncated]" : planContent;
            sb.AppendLine(trimmed);
            sb.AppendLine("```");
            sb.AppendLine("</details>");
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // GitHub API
    // -------------------------------------------------------------------------

    private static async Task<(bool Success, string UrlOrError)> PostGitHubIssueAsync(
        string issueBody, string token, CancellationToken ct)
    {
        // Extract title from first "**Title**:" or "# " line
        var title = "Post-migration feedback: MAF 1.3.0 migration learnings";
        var titleLine = issueBody.Split('\n')
            .FirstOrDefault(l => l.TrimStart().StartsWith("**Title**:") || (l.TrimStart().StartsWith("#") && !l.TrimStart().StartsWith("##")));
        if (titleLine is not null)
        {
            var t = titleLine.Replace("**Title**:", "").Replace("#", "").Trim();
            if (!string.IsNullOrWhiteSpace(t)) title = t;
        }

        // Strip the title line from the body if present
        var body = issueBody;

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        http.DefaultRequestHeaders.Add("User-Agent", "maf-autopilot/1.3.0");
        http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        try
        {
            var payload = new { title, body, labels = new[] { "registry-update" } };
            var response = await http.PostAsJsonAsync(
                "https://api.github.com/repos/joslat/maf-autopilot/issues",
                payload,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(ct);
                var url = json?.RootElement.GetProperty("html_url").GetString() ?? "unknown URL";
                return (true, url);
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            return (false, $"HTTP {(int)response.StatusCode}: {error}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string ExtractText(CreateMessageResult result)
    {
        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(b => b.Text ?? string.Empty));
    }

    private const string FeedbackSystemPrompt =
        "You are a MAF migration expert helping improve the maf-autopilot toolkit. " +
        "Generate a structured, actionable GitHub issue from the provided migration feedback. " +
        "Focus on concrete API names, code snippets, and specific corrections. " +
        "Do NOT include patterns already in the registry. Be concise and specific.";
}
