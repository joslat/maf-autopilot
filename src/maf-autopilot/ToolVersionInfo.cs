using System.Reflection;

namespace MafDoctor;

/// <summary>
/// REP-20: the single source of truth for the installed tool version — computed once
/// from the assembly's <see cref="AssemblyInformationalVersionAttribute"/> (build
/// metadata after '+' trimmed). Shared by <c>--version</c>, the MCP
/// <c>ServerInfo.Version</c>, <c>InitCommand.CurrentVersion</c>, and the SARIF
/// <c>tool.driver.version</c>, so every surface reports the same real version on every
/// release automatically instead of a per-surface hard-coded string (the SARIF version
/// was previously frozen at the retired "1.3.0-alpha-3").
/// </summary>
internal static class ToolVersionInfo
{
    /// <summary>The tool version, e.g. "1.13.0". "0.0.0" only when no version attribute is present.</summary>
    public static string Current { get; } = Compute();

    private static string Compute()
    {
        var raw = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.0.0";
        var plus = raw.IndexOf('+');
        return plus >= 0 ? raw[..plus] : raw;
    }
}
