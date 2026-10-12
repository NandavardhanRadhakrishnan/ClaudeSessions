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

    /// <summary>Gets a value indicating whether the profile Windows Terminal would use is set to run elevated.</summary>
    /// <param name="profileName">Profile name or GUID passed with <c>-p</c>; empty for the default profile.</param>
    public static bool ProfileRequiresElevation(string profileName)
    {
        var path = FindSettingsFile();
        if (path == null)
        {
            return false;
        }

        try
        {
            return ProfileRequiresElevation(File.ReadAllText(path), profileName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool ProfileRequiresElevation(string settingsJson, string profileName)
    {
        try
        {
            using var doc = JsonDocument.Parse(settingsJson, JsonOptions);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("profiles", out var profiles))
            {
                return false;
            }

            // "profiles" is either a plain list or { "defaults": {...}, "list": [...] }.
            JsonElement? defaults = null;
            var list = profiles;
            if (profiles.ValueKind == JsonValueKind.Object)
            {
                defaults = profiles.TryGetProperty("defaults", out var d) && d.ValueKind == JsonValueKind.Object ? d : null;
                list = profiles.TryGetProperty("list", out var l) ? l : default;
            }

            var wanted = profileName.Length > 0 ? profileName : GetString(root, "defaultProfile") ?? string.Empty;
            var profile = list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Cast<JsonElement?>().FirstOrDefault(p =>
                    p!.Value.ValueKind == JsonValueKind.Object
                    && (string.Equals(GetString(p.Value, "name"), wanted, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(GetString(p.Value, "guid"), wanted, StringComparison.OrdinalIgnoreCase)))
                : null;

            return GetBool(profile) ?? GetBool(defaults) ?? false;
        }
        catch (JsonException)
        {
            return false;
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

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
