using System.Reflection;

namespace ErateWorkbench.Api.Utilities;

public static class AppVersionProvider
{
    public static string Version { get; } = BuildVersion();

    private static string BuildVersion()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "unknown";

        var commit = Environment.GetEnvironmentVariable("GIT_COMMIT");
        if (!string.IsNullOrWhiteSpace(commit))
            version += $" ({commit.Trim()[..Math.Min(7, commit.Trim().Length)]})";

        return version;
    }
}
