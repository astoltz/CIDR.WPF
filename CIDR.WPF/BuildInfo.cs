using System.Reflection;

namespace CIDR.WPF;

/// <summary>
/// Provides build-time metadata embedded in the assembly by MSBuild.
/// Values are populated by the CI pipeline; local builds use fallback defaults.
/// </summary>
internal static class BuildInfo
{
    private static readonly Assembly Assembly = typeof(BuildInfo).Assembly;

    /// <summary>Product version (e.g. "1.2.0+abc1234").</summary>
    public static string InformationalVersion =>
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>Semantic version without the commit suffix (e.g. "1.2.0").</summary>
    public static string Version =>
        InformationalVersion.Split('+')[0];

    /// <summary>Short or full git commit SHA, or "local" for non-CI builds.</summary>
    public static string GitCommit
    {
        get
        {
            var parts = InformationalVersion.Split('+');
            return parts.Length > 1 ? parts[1] : "local";
        }
    }

    /// <summary>Branch or tag name that triggered the build.</summary>
    public static string GitBranch =>
        GetMetadata("GitBranch") ?? "unknown";

    /// <summary>UTC date the build was produced (yyyy-MM-dd).</summary>
    public static string BuildDate =>
        GetMetadata("BuildDate") ?? "unknown";

    private static string? GetMetadata(string key) =>
        Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;
}
