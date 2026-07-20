using MafDoctor.Tools;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// F-04 — MCP-only workspace containment. Most tests exercise
/// <see cref="WorkspacePolicy.ValidateCore"/> directly (mode-independent) to
/// avoid touching the process-global `_mcpMode` flag, which
/// <see cref="PathGuard.ValidateRepoPath"/> reads on every call from every
/// concurrently-running test class. The one integration test that does flip
/// the flag lives in <see cref="PathGuardWorkspacePolicyIntegrationTests"/>.
///
/// Shares the "WorkspacePolicy-McpMode" collection with that class (rather
/// than xUnit's default of one implicit collection per class) because both
/// mutate the process-global MAF_DOCTOR_WORKSPACE_ROOTS env var — different
/// collections are eligible to run in parallel with each other by default,
/// which would otherwise race two classes over the same env var.
/// </summary>
[Collection("WorkspacePolicy-McpMode")]
public sealed class WorkspacePolicyTests : IDisposable
{
    private readonly string _tempRoot;

    public WorkspacePolicyTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "maf-doctor-wp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, null);
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void NoRootsConfigured_OrdinaryPath_Allowed()
    {
        Assert.Null(WorkspacePolicy.ValidateCore(_tempRoot));
    }

    [Fact]
    public void FilesystemRoot_AlwaysRejected_EvenWithoutConfiguredRoots()
    {
        if (!OperatingSystem.IsWindows()) return;
        // Derived from the temp path's drive rather than hardcoded "C:\" so this
        // passes regardless of which drive the test environment checks out to.
        var root = Path.GetPathRoot(_tempRoot)!;
        var err = WorkspacePolicy.ValidateCore(root);
        Assert.NotNull(err);
        Assert.Contains("filesystem root", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PosixFilesystemRoot_AlwaysRejected()
    {
        if (OperatingSystem.IsWindows()) return;
        var err = WorkspacePolicy.ValidateCore("/");
        Assert.NotNull(err);
        Assert.Contains("filesystem root", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeDirectory_AlwaysRejected_EvenWithoutConfiguredRoots()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) return;

        var err = WorkspacePolicy.ValidateCore(home);
        Assert.NotNull(err);
        Assert.Contains("home directory", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfiguredRoot_ExactMatch_Allowed()
    {
        Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, _tempRoot);
        Assert.Null(WorkspacePolicy.ValidateCore(_tempRoot));
    }

    [Fact]
    public void ConfiguredRoot_ChildPath_Allowed()
    {
        var child = Path.Combine(_tempRoot, "sub", "dir");
        Directory.CreateDirectory(child);
        Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, _tempRoot);
        Assert.Null(WorkspacePolicy.ValidateCore(child));
    }

    [Fact]
    public void ConfiguredRoot_SiblingDirectory_Rejected()
    {
        // Prefix-collision guard: a root of ".../maf-doctor-wp-tests-XYZ" must not
        // accept ".../maf-doctor-wp-tests-XYZ-evil" via a naive StartsWith.
        var sibling = _tempRoot + "-evil";
        Directory.CreateDirectory(sibling);
        try
        {
            Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, _tempRoot);
            var err = WorkspacePolicy.ValidateCore(sibling);
            Assert.NotNull(err);
            Assert.Contains("outside the configured", err, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(sibling, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ConfiguredRoot_UnrelatedPath_Rejected()
    {
        var unrelated = Path.Combine(Path.GetTempPath(), "maf-doctor-wp-tests-unrelated-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(unrelated);
        try
        {
            Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, _tempRoot);
            var err = WorkspacePolicy.ValidateCore(unrelated);
            Assert.NotNull(err);
            Assert.Contains("outside the configured", err, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(unrelated, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void MultipleConfiguredRoots_SemicolonSeparated_AnyMatchAllowed()
    {
        var second = Path.Combine(Path.GetTempPath(), "maf-doctor-wp-tests-second-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(second);
        try
        {
            Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, $"{_tempRoot};{second}");
            Assert.Null(WorkspacePolicy.ValidateCore(_tempRoot));
            Assert.Null(WorkspacePolicy.ValidateCore(second));
        }
        finally
        {
            try { Directory.Delete(second, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ConfiguredRoots_ReflectedInProperty()
    {
        Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, _tempRoot);
        Assert.Single(WorkspacePolicy.ConfiguredRoots);
        Assert.Equal(Path.GetFullPath(_tempRoot).TrimEnd(Path.DirectorySeparatorChar), WorkspacePolicy.ConfiguredRoots[0]);
    }

    [Fact]
    public void NoRootsConfigured_ConfiguredRootsIsEmpty()
    {
        Assert.Empty(WorkspacePolicy.ConfiguredRoots);
    }
}

/// <summary>
/// The one test that actually flips the process-global MCP-mode flag, to prove
/// PathGuard.ValidateRepoPath really consults WorkspacePolicy when MCP mode is
/// on. Isolated in its own non-parallel collection and always restores the
/// flag to its default (off) in a finally block — see
/// <see cref="WorkspacePolicyTests"/>'s remarks for why this is kept minimal.
/// </summary>
[Collection("WorkspacePolicy-McpMode")]
public sealed class PathGuardWorkspacePolicyIntegrationTests : IDisposable
{
    private readonly string _tempRoot;

    public PathGuardWorkspacePolicyIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "maf-doctor-wp-integ-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        WorkspacePolicy.ResetForTests(mcpMode: false);
        Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, null);
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    [Fact]
    public void McpMode_Off_PathGuard_IgnoresWorkspacePolicy()
    {
        // Default state: CLI-mode semantics. A directory outside any
        // configuration must still be accepted by ValidateRepoPath.
        WorkspacePolicy.ResetForTests(mcpMode: false);
        Assert.Null(PathGuard.ValidateRepoPath(_tempRoot));
    }

    [Fact]
    public void McpMode_On_NoRootsConfigured_OrdinaryPathStillAllowed()
    {
        WorkspacePolicy.ResetForTests(mcpMode: true);
        try
        {
            Assert.Null(PathGuard.ValidateRepoPath(_tempRoot));
        }
        finally
        {
            WorkspacePolicy.ResetForTests(mcpMode: false);
        }
    }

    [Fact]
    public void McpMode_On_RootsConfigured_OutsidePath_RejectedByPathGuard()
    {
        var outside = Path.Combine(Path.GetTempPath(), "maf-doctor-wp-integ-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, _tempRoot);
            WorkspacePolicy.ResetForTests(mcpMode: true);

            var err = PathGuard.ValidateRepoPath(outside);
            Assert.NotNull(err);
            Assert.Contains("outside the configured", err, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            WorkspacePolicy.ResetForTests(mcpMode: false);
            Environment.SetEnvironmentVariable(WorkspacePolicy.WorkspaceRootsEnvName, null);
            try { Directory.Delete(outside, recursive: true); } catch { /* best-effort */ }
        }
    }
}

[CollectionDefinition("WorkspacePolicy-McpMode", DisableParallelization = true)]
public sealed class WorkspacePolicyMcpModeCollection;
