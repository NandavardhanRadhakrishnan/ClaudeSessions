using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;

/// <summary>Finds the Claude CLI so the terminal tab doesn't depend on PowerToys' (possibly stale) PATH.</summary>
internal static class ClaudeCli
{
    /// <summary>Full path to <c>claude.exe</c> (or the npm <c>claude.cmd</c> shim); <c>"claude"</c> if not found.</summary>
    public static string Resolve()
    {
        var dirs = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin"),
        };

        foreach (var target in new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine })
        {
            var path = Environment.GetEnvironmentVariable("PATH", target);
            if (!string.IsNullOrEmpty(path))
            {
                dirs.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(Environment.ExpandEnvironmentVariables));
            }
        }

        foreach (var name in new[] { "claude.exe", "claude.cmd" })
        {
            foreach (var dir in dirs)
            {
                try
                {
                    var candidate = Path.Combine(dir, name);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry.
                }
            }
        }

        return "claude";
    }
}
