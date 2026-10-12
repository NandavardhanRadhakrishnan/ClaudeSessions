using System;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;

/// <summary>Turns the <c>cwd</c> recorded in a transcript into a Windows path where possible.</summary>
public static partial class PathNormalizer
{
    [GeneratedRegex(@"^[A-Za-z]:([\\/]|$)")]
    private static partial Regex DrivePath();

    [GeneratedRegex(@"^/([A-Za-z])(/.*)?$")]
    private static partial Regex MsysDrivePath();

    /// <summary>
    /// <c>C:\x</c> and <c>C:/x</c> become <c>C:\x</c>; Git Bash / MSYS <c>/c/x</c> becomes <c>C:\x</c>;
    /// anything else (e.g. WSL paths) is kept as-is. Trailing separators are trimmed, except on a drive root.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var path = raw.Trim();

        var msys = MsysDrivePath().Match(path);
        if (msys.Success)
        {
            path = char.ToUpperInvariant(msys.Groups[1].Value[0]) + ":" + msys.Groups[2].Value;
        }

        if (!IsWindowsPath(path))
        {
            return path.Length > 1 ? path.TrimEnd('/') : path;
        }

        path = path.Replace('/', '\\').TrimEnd('\\');
        if (path.Length == 2 && path[1] == ':')
        {
            path += "\\";
        }

        return path;
    }

    /// <summary>Gets a value indicating whether the path is a drive or UNC path.</summary>
    public static bool IsWindowsPath(string? path) =>
        !string.IsNullOrEmpty(path) && (DrivePath().IsMatch(path) || path.StartsWith(@"\\", StringComparison.Ordinal));

    /// <summary>Last path segment (<c>acme-api</c> for <c>C:\code\acme-api</c>); a drive root is returned whole.</summary>
    public static string FolderName(string? cwd)
    {
        if (string.IsNullOrEmpty(cwd))
        {
            return string.Empty;
        }

        var trimmed = cwd.TrimEnd('\\', '/');
        var index = trimmed.LastIndexOfAny(['\\', '/']);
        var name = index >= 0 ? trimmed[(index + 1)..] : trimmed;
        return name.Length == 0 || name.EndsWith(':') ? cwd : name;
    }
}
