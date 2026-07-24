using System.Text;

namespace MafDoctor.Tools;

/// <summary>
/// Shared atomic-write primitive for filesystem writers that run inside a
/// repository or managed directory whose layout is not trusted. Every write
/// site previously staged through a predictable name (e.g. <c>&lt;path&gt;.autofix.tmp</c>)
/// via <c>File.WriteAllText</c>, which follows an attacker-precreated symlink
/// at that path before the final move. This primitive closes that class of
/// bug in one place:
///
/// <list type="number">
///   <item>Validates the destination against <see cref="PathGuard.ValidateContainment"/>
///         (rejects escapes, and rejects a symlink/reparse point on the leaf or
///         any parent segment up to the workspace root).</item>
///   <item>Stages through an unpredictable temp filename inside the already-
///         validated destination directory.</item>
///   <item>Opens the temp file with <see cref="FileMode.CreateNew"/>, which
///         refuses to open a pre-existing path (including one an attacker
///         raced into place) rather than following it.</item>
///   <item>Revalidates immediately before the atomic replace, narrowing the
///         window between the initial check and the swap.</item>
/// </list>
///
/// Anchored to the 2026-07-19 maf-doctor security assessment, findings
/// F-01 (AutoFixTool), F-02 (InitCommand), F-03 (scaffolders), and F-24
/// (update cache) — all four shared this same root cause and are the actual
/// callers of this primitive. F-22 (registry override, in
/// <c>RegistryService.ValidateOverridePath</c>) is a related but distinct
/// READ-path containment check — round-2 review fixup: it validates an
/// env-supplied path before *loading* the registry, never writes, and does
/// not call this primitive. It has its own independent parent-chain
/// reparse-point walker (<c>RegistryService.ParentChainHasReparsePoint</c>),
/// deliberately not unified with this one.
/// </summary>
internal static class SafeWorkspaceWriter
{
    /// <summary>
    /// Atomically writes <paramref name="contents"/> to <paramref name="destinationPath"/>,
    /// which must resolve inside <paramref name="workspaceRoot"/> with no
    /// symlink/junction on the leaf or any parent segment. Creates parent
    /// directories as needed. Overwrites an existing destination.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The destination escapes the workspace root, traverses a symlink, or is
    /// itself a symlink/reparse point.
    /// </exception>
    public static void WriteAtomic(string workspaceRoot, string destinationPath, string contents)
        // Default encoding: UTF-8 without a BOM — the historical behaviour of the
        // bare `new StreamWriter(fs)` this overload replaces. New-file writers
        // (scaffolders, init) rely on it.
        => WriteAtomic(workspaceRoot, destinationPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    /// <summary>
    /// Encoding-preserving variant of <see cref="WriteAtomic(string,string,string)"/>.
    /// Writes <paramref name="contents"/> using <paramref name="encoding"/> so a
    /// rewriter that read a file's original encoding (UTF-8-with-BOM, UTF-16, …)
    /// can round-trip it byte-faithfully instead of silently normalising to
    /// UTF-8-no-BOM. Same atomic-replace + symlink-containment guarantees.
    /// </summary>
    public static void WriteAtomic(string workspaceRoot, string destinationPath, string contents, Encoding encoding)
    {
        var resolved = PathGuard.ValidateContainment(workspaceRoot, destinationPath, nameof(destinationPath));

        var dir = Path.GetDirectoryName(resolved)
            ?? throw new ArgumentException("destinationPath has no parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(dir);

        // Unpredictable staging name inside the already-validated directory — an
        // attacker cannot pre-create this path because it isn't chosen until
        // we're already inside the call. CreateNew refuses to open a pre-existing
        // path (including a symlink raced into place after we chose the name but
        // before we opened it), closing the predictable-tmp TOCTOU window.
        var tmp = Path.Combine(dir, $".{Guid.NewGuid():N}.mafdoctor.tmp");
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, encoding))
            {
                writer.Write(contents);
                writer.Flush();
                fs.Flush(flushToDisk: true);
            }

            // Revalidate immediately before the replace — narrows (does not
            // eliminate; see F-21) the window between the initial containment
            // check and the swap.
            PathGuard.ValidateContainment(workspaceRoot, destinationPath, nameof(destinationPath));

            File.Move(tmp, resolved, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>
    /// Creates a brand-new file inside <paramref name="workspaceRoot"/> and
    /// returns <c>true</c>, or returns <c>false</c> without touching the
    /// filesystem if the destination already exists. Never overwrites and
    /// never follows an existing symlink at the destination. Used by
    /// scaffolders that intentionally skip files that already exist.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The destination escapes the workspace root or traverses a symlink.
    /// </exception>
    public static bool TryCreateNew(string workspaceRoot, string destinationPath, string contents)
    {
        var resolved = PathGuard.ValidateContainment(workspaceRoot, destinationPath, nameof(destinationPath));

        var dir = Path.GetDirectoryName(resolved)
            ?? throw new ArgumentException("destinationPath has no parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(dir);

        if (File.Exists(resolved)) return false;

        try
        {
            using var fs = new FileStream(resolved, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fs);
            writer.Write(contents);
            return true;
        }
        catch (IOException) when (File.Exists(resolved))
        {
            // Lost a create-new race to a concurrent writer — treat as skip,
            // not failure.
            return false;
        }
    }

    /// <summary>
    /// Copies <paramref name="sourcePath"/> to a brand-new file at
    /// <paramref name="destinationPath"/> inside <paramref name="workspaceRoot"/>.
    /// Never overwrites and never follows an existing symlink at the destination —
    /// throws <see cref="IOException"/> if the destination already exists (the
    /// caller is expected to retry with a fresh, unpredictable destination name
    /// on collision, as in <c>InitCommand.CreateBackupWithRetry</c>).
    ///
    /// Round-2 review fixup — <c>CreateBackupWithRetry</c> previously called
    /// <c>PathGuard.ValidateContainment</c> + raw <c>File.Copy(overwrite: false)</c>
    /// directly instead of going through this class, contradicting docs that
    /// claimed every write site did. Functionally close to equivalent (an
    /// unpredictable candidate name plus an OS-level exists-and-fail-atomically
    /// copy resists the same predictable-path symlink race this class exists to
    /// close), but a hand-duplicated variant is exactly the drift this
    /// primitive was extracted to prevent.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The destination escapes the workspace root or traverses a symlink.
    /// </exception>
    public static void CopyNew(string workspaceRoot, string sourcePath, string destinationPath)
    {
        var resolved = PathGuard.ValidateContainment(workspaceRoot, destinationPath, nameof(destinationPath));

        var dir = Path.GetDirectoryName(resolved)
            ?? throw new ArgumentException("destinationPath has no parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(dir);

        // Round-3 review fixup — open the source BEFORE creating the destination.
        // The prior ordering opened the destination (FileMode.CreateNew, which
        // creates the file immediately) first, so a missing/unreadable
        // sourcePath threw only after an orphaned, zero-byte file already
        // existed at the destination. Opening the source first means that
        // failure now propagates before the destination is ever touched.
        using var srcStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var destStream = new FileStream(resolved, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            using (destStream)
            {
                srcStream.CopyTo(destStream);
            }
        }
        catch
        {
            // destStream is only non-null and open past this point once
            // FileMode.CreateNew has actually succeeded — i.e. we know we
            // created this file ourselves, not that it pre-existed (CreateNew
            // throws before creating anything if the destination already
            // exists, and that throw happens outside this try). Safe to clean
            // up an entry that failed mid-copy without risking a pre-existing
            // file that was never ours to touch.
            try { if (File.Exists(resolved)) File.Delete(resolved); } catch { /* best-effort cleanup */ }
            throw;
        }
    }
}
