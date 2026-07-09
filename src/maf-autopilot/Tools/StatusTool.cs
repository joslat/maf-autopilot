using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MafDoctor.Tools;

/// <summary>
/// MCP tool: MafDoctorStatus
///
/// Reports whether the installed global tool package is current and whether the
/// current workspace's MCP init/steering files match this installed binary.
/// </summary>
[McpServerToolType]
public sealed class StatusTool
{
    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = true)]
    [Description("""
        Check maf-doctor package freshness and workspace init freshness. Use this
        when the user asks whether the MCP server itself needs an update or whether
        they only need to rerun `maf-doctor init` for this workspace.

        Input:
          - repoPath: repository/workspace root to inspect. Defaults to the MCP
            server current directory.

        Returns markdown with exact update/init commands when action is needed.
        """)]
    public async Task<string> MafDoctorStatus(
        [Description("Repository/workspace root to inspect. Defaults to the MCP server current directory.")]
        string? repoPath = null)
    {
        var rawPath = string.IsNullOrWhiteSpace(repoPath)
            ? Directory.GetCurrentDirectory()
            : repoPath;

        if (PathGuard.ValidateRepoPath(rawPath) is { } error)
            return error;

        var targetDir = Path.GetFullPath(rawPath);

        var update = await UpdateAdvisor.CheckForUpdateAsync(timeout: TimeSpan.FromSeconds(3));
        var init = InitCommand.GetWorkspaceInitStatus(targetDir);
        return UpdateAdvisor.BuildMarkdown(update, init, targetDir);
    }
}