using System.Reflection;
using MafAutopilot.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace MafAutopilot.Tests;

/// <summary>
/// Locks the <c>[McpServerTool]</c> annotation contract for tools whose hints
/// gate auto-invocation in MCP-spec-compliant clients (Claude Code, VS Code
/// Copilot). A wrong annotation is a security bug, not a style issue:
///
///   - <c>ReadOnly = true</c> permits clients to invoke without user consent.
///     Setting it on a tool that spawns a subprocess (e.g. <c>dotnet build</c>)
///     is a privilege escalation.
///   - <c>Destructive = false</c> tells clients "this won't change the
///     workspace." Setting it on a tool that writes new files is misleading.
///   - <c>OpenWorld = false</c> tells clients "this doesn't reach the network."
///     Setting it on a tool that hits NuGet is misleading.
///
/// See <c>docs/security.md</c> "Known MCP attack classes" and the v1.1
/// hardening plan §3.1 for the per-tool rationale.
/// </summary>
public sealed class McpToolAnnotationTests
{
    // -------------------------------------------------------------------------
    // Phase 1.2 — MafRunCs0618Hunt: ReadOnly was true; corrected to false.
    // OpenWorld was unset; corrected to true (NuGet restore via dotnet build).
    // -------------------------------------------------------------------------

    [Fact]
    public void MafRunCs0618Hunt_IsNotReadOnly()
    {
        var attr = GetToolAttribute<Cs0618HuntTool>(nameof(Cs0618HuntTool.MafRunCs0618Hunt));
        Assert.False(attr.ReadOnly, "MafRunCs0618Hunt spawns `dotnet build` — must not be ReadOnly=true.");
    }

    [Fact]
    public void MafRunCs0618Hunt_IsOpenWorld()
    {
        var attr = GetToolAttribute<Cs0618HuntTool>(nameof(Cs0618HuntTool.MafRunCs0618Hunt));
        Assert.True(attr.OpenWorld, "MafRunCs0618Hunt triggers NuGet restore — must be OpenWorld=true.");
    }

    // -------------------------------------------------------------------------
    // Phase 1.4 — MafNewAgent / MafNewExecutor: Destructive was false despite
    // writing new files into the user's project tree.
    // -------------------------------------------------------------------------

    [Fact]
    public void MafNewAgent_IsDestructive()
    {
        var attr = GetToolAttribute<NewAgentTool>(nameof(NewAgentTool.MafNewAgent));
        Assert.True(attr.Destructive, "MafNewAgent writes new .cs files — must be Destructive=true.");
    }

    [Fact]
    public void MafNewAgent_IsIdempotent()
    {
        var attr = GetToolAttribute<NewAgentTool>(nameof(NewAgentTool.MafNewAgent));
        Assert.True(attr.Idempotent, "MafNewAgent skips-on-exist; second invocation is observably equivalent.");
    }

    [Fact]
    public void MafNewExecutor_IsDestructive()
    {
        var attr = GetToolAttribute<NewAgentTool>(nameof(NewAgentTool.MafNewExecutor));
        Assert.True(attr.Destructive, "MafNewExecutor writes new .cs files — must be Destructive=true.");
    }

    [Fact]
    public void MafNewExecutor_IsIdempotent()
    {
        var attr = GetToolAttribute<NewAgentTool>(nameof(NewAgentTool.MafNewExecutor));
        Assert.True(attr.Idempotent, "MafNewExecutor skips-on-exist; second invocation is observably equivalent.");
    }

    // -------------------------------------------------------------------------
    // Phase 1.5 — DiffPackage / PreUpgradeDryRun: OpenWorld was unset despite
    // reaching api.nuget.org via dotnet-inspect.
    // -------------------------------------------------------------------------

    [Fact]
    public void MafDiffPackage_IsOpenWorld()
    {
        var attr = GetToolAttribute<DiffPackageTool>(nameof(DiffPackageTool.MafDiffPackage));
        Assert.True(attr.OpenWorld, "MafDiffPackage shells dotnet-inspect against api.nuget.org — must be OpenWorld=true.");
    }

    [Fact]
    public void MafDiffPackage_IsReadOnly()
    {
        var attr = GetToolAttribute<DiffPackageTool>(nameof(DiffPackageTool.MafDiffPackage));
        Assert.True(attr.ReadOnly, "MafDiffPackage writes nothing to the workspace — ReadOnly=true is correct.");
    }

    [Fact]
    public void MafPreUpgradeDryRun_IsOpenWorld()
    {
        var attr = GetToolAttribute<PreUpgradeDryRunTool>(nameof(PreUpgradeDryRunTool.MafPreUpgradeDryRun));
        Assert.True(attr.OpenWorld, "MafPreUpgradeDryRun shells dotnet-inspect against api.nuget.org — must be OpenWorld=true.");
    }

    [Fact]
    public void MafPreUpgradeDryRun_IsReadOnly()
    {
        var attr = GetToolAttribute<PreUpgradeDryRunTool>(nameof(PreUpgradeDryRunTool.MafPreUpgradeDryRun));
        Assert.True(attr.ReadOnly, "MafPreUpgradeDryRun emits a report only; ReadOnly=true is correct.");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static McpServerToolAttribute GetToolAttribute<TTool>(string methodName)
    {
        var method = typeof(TTool).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {typeof(TTool).Name}.");
        var attr = method.GetCustomAttribute<McpServerToolAttribute>()
            ?? throw new InvalidOperationException($"[McpServerTool] not present on {typeof(TTool).Name}.{methodName}.");
        return attr;
    }
}
