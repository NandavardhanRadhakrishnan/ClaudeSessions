using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;

/// <summary>
/// The desktop app's <c>claude://resume</c> import names the new desktop session only from a <c>custom-title</c>
/// record in the transcript (what <c>/rename</c> writes); Claude's automatic <c>ai-title</c> is ignored, so the
/// session shows up as "Untitled". These helpers give a transcript that record before it is imported.
/// </summary>
internal static class TranscriptTitle
{
    private const string CustomTitleMarker = "\"type\":\"custom-title\"";

    /// <summary>
    /// Appends <c>{"type":"custom-title",...}</c> unless the transcript already has one or the title is a placeholder.
    /// Returns whether anything was written.
    /// </summary>
    public static bool EnsureCustomTitle(string transcriptPath, string sessionId, string title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.StartsWith("(untitled)", StringComparison.Ordinal))
        {
            return false;
        }

        using var stream = new FileStream(transcriptPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024, leaveOpen: true))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains(CustomTitleMarker, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        var needsNewline = false;
        if (stream.Length > 0)
        {
            stream.Seek(-1, SeekOrigin.End);
            needsNewline = stream.ReadByte() != '\n';
        }

        var record = JsonSerializer.Serialize(new { type = "custom-title", customTitle = title, sessionId }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var bytes = Encoding.UTF8.GetBytes((needsNewline ? "\n" : string.Empty) + record + "\n");
        stream.Seek(0, SeekOrigin.End);
        stream.Write(bytes);
        return true;
    }

    /// <summary>Gets a value indicating whether a desktop Code session already wraps this CLI session.</summary>
    /// <param name="sessionsRoot">The desktop app's <c>claude-code-sessions</c> folder.</param>
    public static bool IsImportedIntoDesktop(string cliSessionId, string sessionsRoot)
    {
        if (!Directory.Exists(sessionsRoot))
        {
            return false;
        }

        var needle = $"\"cliSessionId\":\"{cliSessionId}\"";
        try
        {
            return Directory.EnumerateFiles(sessionsRoot, "local_*.json", SearchOption.AllDirectories)
                .Any(file =>
                {
                    try
                    {
                        return File.ReadAllText(file).Contains(needle, StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        return false;
                    }
                });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The desktop app's <c>claude-code-sessions</c> folder (it uses the real roaming profile, even as a Store app).</summary>
    public static string DesktopSessionsRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude-code-sessions");
}
