using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;

/// <summary>Reads just enough of Windows Terminal's settings.json to know whether a profile runs elevated.</summary>
/// <remarks>
/// When a non-elevated app (PowerToys Run) opens a profile with <c>"elevate": true</c>, Windows Terminal hands the
/// request to an elevated instance and, on the way, quotes the whole command line as one file name, so it fails with
/// "The system cannot find the file specified". Launching <c>wt.exe</c> elevated ourselves avoids that hand-off.
/// </remarks>
internal static class TerminalSettings
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Full path of the <c>wt.exe</c> app execution alias. Shell-executing the bare name <c>wt.exe</c> from
    /// PowerToys Run starts a process that never opens a window; the alias path works.
    /// </summary>
    public static string ResolveWtPath()
    {
        var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        return File.Exists(alias) ? alias : "wt.exe";
    }

    // Built-in profiles that carry no "commandline" in settings.json.
    private const string CommandPromptGuid = "{0caa0dad-35be-5f56-a8ff-afceeeaa6101}";
    private const string WindowsPowerShellGuid = "{61c54bbd-c2c6-5271-96e7-009a87ff44bf}";
    private const string PowerShellCoreSource = "Windows.Terminal.PowershellCore";

    /// <summary>Contents of Windows Terminal's settings.json, or <c>null</c> if it can't be found or read.</summary>
    public static string? ReadSettings()
    {
        var path = FindSettingsFile();
        if (path == null)
        {
            return null;
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Gets a value indicating whether the profile Windows Terminal would use is set to run elevated.</summary>
    /// <param name="profileName">Profile name or GUID passed with <c>-p</c>; empty for the default profile.</param>
    public static bool ProfileRequiresElevation(string settingsJson, string profileName) =>
        Inspect(settingsJson, profileName, false, (profile, defaults) => GetBool(profile) ?? GetBool(defaults) ?? false);

    /// <summary>
    /// The shell the profile runs, used to host Claude so the tab survives <c>/exit</c>. Profiles that run something
    /// else (WSL, Git Bash, ...) get <c>cmd</c>, since the plugin replaces their command line anyway.
    /// </summary>
    public static TerminalShell ProfileShell(string settingsJson, string profileName) =>
        Inspect(settingsJson, profileName, TerminalShell.Cmd, (profile, defaults) =>
        {
            var commandLine = (GetString(profile, "commandline") ?? GetString(defaults, "commandline") ?? string.Empty).ToLowerInvariant();
            if (commandLine.Contains("pwsh", StringComparison.Ordinal))
            {
                return TerminalShell.PowerShellCore;
            }

            if (commandLine.Contains("powershell", StringComparison.Ordinal))
            {
                return TerminalShell.WindowsPowerShell;
            }

            if (commandLine.Length > 0)
            {
                return TerminalShell.Cmd;
            }

            if (string.Equals(GetString(profile, "source"), PowerShellCoreSource, StringComparison.OrdinalIgnoreCase))
            {
                return TerminalShell.PowerShellCore;
            }

            return string.Equals(GetString(profile, "guid"), WindowsPowerShellGuid, StringComparison.OrdinalIgnoreCase)
                ? TerminalShell.WindowsPowerShell
                : TerminalShell.Cmd;
        });

    /// <summary>Finds the profile Windows Terminal would use and hands it, with the profile defaults, to <paramref name="pick"/>.</summary>
    private static T Inspect<T>(string settingsJson, string profileName, T fallback, Func<JsonElement?, JsonElement?, T> pick)
    {
        try
        {
            using var doc = JsonDocument.Parse(settingsJson, JsonOptions);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("profiles", out var profiles))
            {
                return fallback;
            }

            // "profiles" is either a plain list or { "defaults": {...}, "list": [...] }.
            JsonElement? defaults = null;
            var list = profiles;
            if (profiles.ValueKind == JsonValueKind.Object)
            {
                defaults = profiles.TryGetProperty("defaults", out var d) && d.ValueKind == JsonValueKind.Object ? d : null;
                list = profiles.TryGetProperty("list", out var l) ? l : default;
            }

            var wanted = profileName.Length > 0 ? profileName : GetString(root, "defaultProfile") ?? CommandPromptGuid;
            var profile = list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Cast<JsonElement?>().FirstOrDefault(p =>
                    p!.Value.ValueKind == JsonValueKind.Object
                    && (string.Equals(GetString(p.Value, "name"), wanted, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(GetString(p.Value, "guid"), wanted, StringComparison.OrdinalIgnoreCase)))
                : null;

            return pick(profile, defaults);
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    private static string? FindSettingsFile()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        IEnumerable<string> candidates =
        [
            Path.Combine(localAppData, "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json"),
            Path.Combine(localAppData, "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json"),
            Path.Combine(localAppData, "Microsoft", "Windows Terminal", "settings.json"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static bool? GetBool(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty("elevate", out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean()
            : null;

    private static string? GetString(JsonElement? element, string name) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
