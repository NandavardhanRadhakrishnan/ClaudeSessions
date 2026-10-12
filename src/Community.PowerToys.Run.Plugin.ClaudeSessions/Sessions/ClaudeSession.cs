using System;
using System.IO;
using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;

/// <summary>One Claude Code session transcript, as shown in the Run dropdown.</summary>
/// <param name="Id">Session ID (the transcript file name without extension).</param>
/// <param name="Title">Display title, see <see cref="SessionScanner"/> for precedence.</param>
/// <param name="Cwd">Normalised folder the session was started from; empty when unknown.</param>
/// <param name="LastActive">Transcript last write time (UTC).</param>
/// <param name="UserPromptCount">Number of real prompts typed by the user.</param>
public sealed record ClaudeSession(string Id, string Title, string Cwd, DateTime LastActive, int UserPromptCount)
{
    [JsonIgnore]
    public string FolderName => PathNormalizer.FolderName(Cwd);

    /// <summary>Gets a value indicating whether <see cref="Cwd"/> exists. Hits the disk on every read.</summary>
    [JsonIgnore]
    public bool FolderExists => PathNormalizer.IsWindowsPath(Cwd) && Directory.Exists(Cwd);
}
