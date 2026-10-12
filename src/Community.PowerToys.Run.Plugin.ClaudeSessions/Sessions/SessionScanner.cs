using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;

/// <summary>Reads one <c>&lt;session-id&gt;.jsonl</c> transcript into a <see cref="ClaudeSession"/>.</summary>
public static class SessionScanner
{
    private const int MaxPromptTitleLength = 80;

    /// <summary>
    /// Scans a transcript. Returns <c>null</c> when the file name is not a session ID.
    /// Throws <see cref="IOException"/> / <see cref="UnauthorizedAccessException"/> when the file can't be read.
    /// </summary>
    public static ClaudeSession? Scan(string path)
    {
        var id = Path.GetFileNameWithoutExtension(path);
        if (!Guid.TryParse(id, out _))
        {
            return null;
        }

        string? cwd = null;
        string? customTitle = null;
        string? agentName = null;
        string? aiTitle = null;
        string? firstPrompt = null;
        var promptCount = 0;

        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024))
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // Most lines are large assistant/tool records; only parse the few kinds we need.
                var wantCwd = cwd == null && line.Contains("\"cwd\":\"", StringComparison.Ordinal);
                var maybeTitle = line.Contains("\"type\":\"custom-title\"", StringComparison.Ordinal)
                    || line.Contains("\"type\":\"agent-name\"", StringComparison.Ordinal)
                    || line.Contains("\"type\":\"ai-title\"", StringComparison.Ordinal);
                var maybeUser = line.Contains("\"type\":\"user\"", StringComparison.Ordinal);
                if (!wantCwd && !maybeTitle && !maybeUser)
                {
                    continue;
                }

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(line);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (cwd == null && TryGetString(root, "cwd") is { } rawCwd)
                    {
                        cwd = PathNormalizer.Normalize(rawCwd);
                    }

                    switch (TryGetString(root, "type"))
                    {
                        case "custom-title":
                            customTitle = NonBlank(TryGetString(root, "customTitle")) ?? customTitle;
                            break;
                        case "agent-name":
                            agentName = NonBlank(TryGetString(root, "agentName")) ?? agentName;
                            break;
                        case "ai-title":
                            aiTitle = NonBlank(TryGetString(root, "aiTitle")) ?? aiTitle;
                            break;
                        case "user":
                            if (GetUserPrompt(root) is { } prompt)
                            {
                                promptCount++;
                                firstPrompt ??= ToSingleLineTitle(prompt);
                            }

                            break;
                    }
                }
            }
        }

        var title = customTitle ?? agentName ?? aiTitle ?? NonBlank(firstPrompt) ?? $"(untitled) {id[..8]}";
        return new ClaudeSession(id, title, cwd ?? string.Empty, File.GetLastWriteTimeUtc(path), promptCount);
    }

    /// <summary>A typed prompt: string content, not meta, not a <c>&lt;command-…&gt;</c>-style wrapper.</summary>
    private static string? GetUserPrompt(JsonElement root)
    {
        if (root.TryGetProperty("isMeta", out var isMeta) && isMeta.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        if (!root.TryGetProperty("message", out var message)
            || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = content.GetString()!.TrimStart();
        return text.Length == 0 || text.StartsWith('<') ? null : text;
    }

    private static string ToSingleLineTitle(string text)
    {
        var builder = new StringBuilder(Math.Min(text.Length, MaxPromptTitleLength + 1));
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
            if (builder.Length > MaxPromptTitleLength)
            {
                break;
            }
        }

        if (builder.Length <= MaxPromptTitleLength)
        {
            return builder.ToString();
        }

        return builder.ToString(0, MaxPromptTitleLength - 1).TrimEnd() + "…";
    }

    private static string? TryGetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? NonBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
