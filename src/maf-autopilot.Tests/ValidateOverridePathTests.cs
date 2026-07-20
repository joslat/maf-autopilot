using MafDoctor.Data;
using Xunit;

namespace MafDoctor.Tests;

/// <summary>
/// Tests for <see cref="RegistryService.ValidateOverridePath"/> — the
/// Phase 5.5 guard on the <c>MAF_REGISTRY_PATH</c> env-var override.
///
/// Phase 7.G fixup — surfaced by the final Opus review as the most
/// notable Phase 5 gap with zero test coverage. The guard rejects shell
/// metacharacters, NUL/CR/LF, reparse-point files, and (when
/// <c>MAF_REGISTRY_PATH_ROOTS</c> is set) paths outside the allowlist.
/// </summary>
public sealed class ValidateOverridePathTests
{
    // -------------------------------------------------------------------------
    // Char rejection: shell metacharacters + control chars.
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("/path/to/registry;rm -rf /")]
    [InlineData("/path|with-pipe.yaml")]
    [InlineData("/path&with-amp.yaml")]
    [InlineData("/path\"with-quote.yaml")]
    [InlineData("/path\rwith-cr.yaml")]
    [InlineData("/path\nwith-lf.yaml")]
    [InlineData("/path\0with-nul.yaml")]
    public void ValidateOverridePath_ShellMetachar_Throws(string envPath)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => RegistryService.ValidateOverridePath(envPath));
        Assert.Contains("invalid characters", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateOverridePath_NoMetachars_Accepts()
    {
        // A clean path that doesn't exist on disk passes — the guard only
        // probes attributes when File.Exists is true. (The 5 MB cap and
        // YAML deserialization are downstream of ValidateOverridePath and
        // not its concern.)
        RegistryService.ValidateOverridePath("/tmp/maf-registry-clean-path.yaml");
        // No exception = accepted.
    }

    // -------------------------------------------------------------------------
    // Reparse-point rejection on extant file.
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateOverridePath_SymlinkAtCandidate_Throws()
    {
        var target = Path.Combine(Path.GetTempPath(), "vop-target-" + Guid.NewGuid().ToString("N") + ".yaml");
        var link = Path.Combine(Path.GetTempPath(), "vop-link-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(target, "schema_version: 1.0\nentries: []\n");

        try
        {
            // Symlink creation on Windows requires admin / Developer Mode.
            // Gracefully skip if unavailable.
            try { File.CreateSymbolicLink(link, target); }
            catch (UnauthorizedAccessException) { return; }
            catch (IOException) { return; }
            catch (PlatformNotSupportedException) { return; }

            var ex = Assert.Throws<InvalidOperationException>(
                () => RegistryService.ValidateOverridePath(link));
            Assert.Contains("symlink", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { File.Delete(link); } catch { /* best effort */ }
            try { File.Delete(target); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void ValidateOverridePath_RegularFile_Accepts()
    {
        var path = Path.Combine(Path.GetTempPath(), "vop-regular-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, "schema_version: 1.0\nentries: []\n");
        try
        {
            RegistryService.ValidateOverridePath(path);
            // No exception = accepted.
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    // -------------------------------------------------------------------------
    // MAF_REGISTRY_PATH_ROOTS allowlist.
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateOverridePath_OutsideAllowlist_Throws()
    {
        var temp = Path.GetTempPath().TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var allowedRoot = Path.Combine(temp, "vop-allowed-" + Guid.NewGuid().ToString("N"));
        var outsidePath = Path.Combine(temp, "vop-outside-" + Guid.NewGuid().ToString("N") + ".yaml");
        Directory.CreateDirectory(allowedRoot);

        var prior = Environment.GetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS");
        try
        {
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", allowedRoot);
            var ex = Assert.Throws<InvalidOperationException>(
                () => RegistryService.ValidateOverridePath(outsidePath));
            Assert.Contains("allowlist", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", prior);
            try { Directory.Delete(allowedRoot, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void ValidateOverridePath_InsideAllowlist_Accepts()
    {
        var temp = Path.GetTempPath().TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var allowedRoot = Path.Combine(temp, "vop-allowed-" + Guid.NewGuid().ToString("N"));
        var insidePath = Path.Combine(allowedRoot, "registry.yaml");
        Directory.CreateDirectory(allowedRoot);
        File.WriteAllText(insidePath, "schema_version: 1.0\nentries: []\n");

        var prior = Environment.GetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS");
        try
        {
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", allowedRoot);
            RegistryService.ValidateOverridePath(insidePath);
            // No exception = accepted.
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", prior);
            try { Directory.Delete(allowedRoot, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void ValidateOverridePath_MultipleAllowlistEntries_AcceptsAnyMatch()
    {
        // Allowlist accepts a ";"-separated list of acceptable parent dirs.
        var temp = Path.GetTempPath().TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootA = Path.Combine(temp, "vop-A-" + Guid.NewGuid().ToString("N"));
        var rootB = Path.Combine(temp, "vop-B-" + Guid.NewGuid().ToString("N"));
        var insideB = Path.Combine(rootB, "registry.yaml");
        Directory.CreateDirectory(rootA);
        Directory.CreateDirectory(rootB);
        File.WriteAllText(insideB, "schema_version: 1.0\nentries: []\n");

        var prior = Environment.GetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS");
        try
        {
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", rootA + ";" + rootB);
            RegistryService.ValidateOverridePath(insideB);
            // No exception = accepted because match exists.
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", prior);
            try { Directory.Delete(rootA, recursive: true); } catch { /* best effort */ }
            try { Directory.Delete(rootB, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void ValidateOverridePath_AllowlistUnset_AcceptsAnyClean()
    {
        // When MAF_REGISTRY_PATH_ROOTS is unset (or empty), the allowlist
        // check is bypassed — only the metachar + reparse-point checks fire.
        var prior = Environment.GetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS");
        try
        {
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", null);
            RegistryService.ValidateOverridePath("/anywhere/clean/path.yaml");
            // No exception = accepted.
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", prior);
        }
    }

    // -------------------------------------------------------------------------
    // F-22 — Path.GetFullPath only collapses `.`/`..` syntactically; it never
    // resolves symlinks. "<allowed-root>/link/registry.yaml", where `link` is a
    // symlinked directory pointing outside the root and the leaf file itself is
    // regular, satisfied both the leaf check and the prefix check above while
    // physically resolving outside the declared root. Symlink creation on
    // Windows requires admin/Developer Mode — skip gracefully if unavailable.
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateOverridePath_SymlinkedParentInsideAllowlist_Throws()
    {
        var temp = Path.GetTempPath().TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var allowedRoot = Path.Combine(temp, "vop-allowed-" + Guid.NewGuid().ToString("N"));
        var outsideDir = Path.Combine(temp, "vop-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(allowedRoot);
        Directory.CreateDirectory(outsideDir);
        var outsideRegistry = Path.Combine(outsideDir, "registry.yaml");
        File.WriteAllText(outsideRegistry, "schema_version: 1.0\nentries: []\n");
        var linkedDir = Path.Combine(allowedRoot, "link");
        var prior = Environment.GetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS");

        try
        {
            try { Directory.CreateSymbolicLink(linkedDir, outsideDir); }
            catch (UnauthorizedAccessException) { return; }
            catch (IOException) { return; }
            catch (PlatformNotSupportedException) { return; }

            var candidateThroughLink = Path.Combine(linkedDir, "registry.yaml");
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", allowedRoot);
            var ex = Assert.Throws<InvalidOperationException>(
                () => RegistryService.ValidateOverridePath(candidateThroughLink));
            Assert.Contains("symlink", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MAF_REGISTRY_PATH_ROOTS", prior);
            try { Directory.Delete(linkedDir); } catch { /* best effort */ }
            try { Directory.Delete(allowedRoot, recursive: true); } catch { /* best effort */ }
            try { Directory.Delete(outsideDir, recursive: true); } catch { /* best effort */ }
        }
    }
}
