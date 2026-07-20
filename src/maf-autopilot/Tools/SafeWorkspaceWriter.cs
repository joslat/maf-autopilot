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
/// F-01 (AutoFixTool), F-02 (InitCommand), F-03 (scaffolders), F-22 (registry
/// override), and F-24 (update cache) — all five shared this same root cause.
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
            using (var writer = new StreamWriter(fs))
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
}
