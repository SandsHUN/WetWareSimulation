using System.Reflection;

namespace WetWare.App.Services;

/// <summary>Version and copyright stamped into the assembly at build time (see the StampBuildVersion target).</summary>
public static class AppInfo
{
    private static readonly Assembly Assembly = typeof(AppInfo).Assembly;

    public static string Version { get; } =
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetName().Version?.ToString(3)
        ?? "?";

    public static string VersionLabel { get; } = "v" + Version;

    public static string Copyright { get; } =
        Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

    public static string FooterLabel { get; } = $"{VersionLabel} · .NET 8 · WPF";
}
