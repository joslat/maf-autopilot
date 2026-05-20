using MafAutopilot.Tools;
using Xunit;

namespace MafAutopilot.Tests;

public class PathGuardTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyOrWhitespace_Rejected(string? path)
    {
        var err = PathGuard.ValidateRepoPath(path);
        Assert.NotNull(err);
        Assert.Contains("must not be empty", err, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("/var/repo/../../etc/passwd")]
    [InlineData("C:\\repo\\..\\..\\Windows\\System32")]
    [InlineData("repo/sub/../escape")]
    public void TraversalSegments_Rejected(string path)
    {
        var err = PathGuard.ValidateRepoPath(path);
        Assert.NotNull(err);
        Assert.Contains("traversal", err, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/path\"with quote")]
    [InlineData("/path; rm -rf /")]
    [InlineData("/path | cat")]
    [InlineData("/path && evil")]
    [InlineData("/path\nnewline")]
    public void ShellMetacharacters_Rejected(string path)
    {
        var err = PathGuard.ValidateRepoPath(path);
        Assert.NotNull(err);
        Assert.Contains("invalid characters", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonexistentDirectory_Rejected()
    {
        var err = PathGuard.ValidateRepoPath("/definitely/does/not/exist/anywhere");
        Assert.NotNull(err);
        Assert.Contains("does not exist", err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingDirectory_Accepted()
    {
        var err = PathGuard.ValidateRepoPath(Path.GetTempPath());
        Assert.Null(err);
    }

    [Fact]
    public void ParameterName_AppearsInErrorMessage()
    {
        var err = PathGuard.ValidateRepoPath("", parameterName: "projectPath");
        Assert.NotNull(err);
        Assert.Contains("projectPath", err);
    }

    // -------------------------------------------------------------------------
    // ValidateContainment — Phase 1.1 / C1
    //
    // Repo root is created under the temp dir; candidate paths exercise both
    // legitimate-inside and adversarial-outside cases.
    // -------------------------------------------------------------------------

    [Fact]
    public void Containment_LegitRelativePath_ReturnsCanonicalPath()
    {
        var root = CreateTempRoot();
        try
        {
            var resolved = PathGuard.ValidateContainment(root, "sub/Foo.cs");
            Assert.StartsWith(Path.GetFullPath(root), resolved, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("Foo.cs", resolved);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Containment_AbsolutePathInsideRoot_ReturnsCanonicalPath()
    {
        var root = CreateTempRoot();
        try
        {
            var insideAbsolute = Path.Combine(root, "Foo.cs");
            var resolved = PathGuard.ValidateContainment(root, insideAbsolute);
            Assert.Equal(Path.GetFullPath(insideAbsolute), resolved, ignoreCase: true);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Containment_AbsolutePathOutsideRoot_Throws()
    {
        var root = CreateTempRoot();
        try
        {
            var outside = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts")
                : "/etc/hosts";
            var ex = Assert.Throws<ArgumentException>(() => PathGuard.ValidateContainment(root, outside));
            Assert.Contains("repository root", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Containment_DotDotEscape_Throws()
    {
        var root = CreateTempRoot();
        try
        {
            var ex = Assert.Throws<ArgumentException>(
                () => PathGuard.ValidateContainment(root, "../../../etc/hosts"));
            Assert.Contains("..", ex.Message);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Containment_ShellMetacharacter_Throws()
    {
        var root = CreateTempRoot();
        try
        {
            var ex = Assert.Throws<ArgumentException>(
                () => PathGuard.ValidateContainment(root, "Foo.cs; rm -rf /"));
            Assert.Contains("invalid characters", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Containment_NullEmptyCandidate_Throws()
    {
        var root = CreateTempRoot();
        try
        {
            Assert.Throws<ArgumentException>(() => PathGuard.ValidateContainment(root, ""));
            Assert.Throws<ArgumentException>(() => PathGuard.ValidateContainment(root, "   "));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Containment_DoesNotEchoInputPath()
    {
        // Defense-in-depth: error messages must not echo the candidate path so
        // future telemetry doesn't leak filesystem layout.
        var root = CreateTempRoot();
        var outside = OperatingSystem.IsWindows()
            ? @"C:\Windows\System32\drivers\etc\hosts-SECRET-VALUE"
            : "/etc/hosts-SECRET-VALUE";
        try
        {
            var ex = Assert.Throws<ArgumentException>(() => PathGuard.ValidateContainment(root, outside));
            Assert.DoesNotContain("SECRET-VALUE", ex.Message, StringComparison.Ordinal);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "pathguard-containment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
