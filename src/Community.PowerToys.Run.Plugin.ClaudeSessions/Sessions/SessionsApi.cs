using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;

/// <summary>
/// Lists the sessions under <c>&lt;claude home&gt;\projects</c>, re-scanning only transcripts whose
/// size or write time changed, and persists the scan results so later starts stay cheap.
/// </summary>
public sealed class SessionsApi
{
    private const int CacheVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _projectsDir;
    private readonly string _cacheFile;
    private readonly Func<string, ClaudeSession?> _scan;
    private readonly object _lock = new();

    // Keyed by full transcript path (case-insensitive, as on NTFS).
    private Dictionary<string, CacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ClaudeSession> _sessions = Array.Empty<ClaudeSession>();

    public SessionsApi(string claudeHome, string cacheFile, Func<string, ClaudeSession?>? scan = null)
    {
        _projectsDir = Path.Combine(claudeHome, "projects");
        _cacheFile = cacheFile;
        _scan = scan ?? SessionScanner.Scan;
    }

    /// <summary><c>%CLAUDE_CONFIG_DIR%</c> if set, else <c>%USERPROFILE%\.claude</c>.</summary>
    public static string ResolveClaudeHome(string? configDir, string userProfile) =>
        string.IsNullOrWhiteSpace(configDir) ? Path.Combine(userProfile, ".claude") : configDir;

    public static string ResolveClaudeHome() =>
        ResolveClaudeHome(
            Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>Loads the persisted cache. A missing, corrupt or wrong-version file is ignored.</summary>
    public void Load()
    {
        CacheFile? file = null;
        try
        {
            if (File.Exists(_cacheFile))
            {
                file = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(_cacheFile), JsonOptions);
            }
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log.Warn($"Ignoring unreadable session cache {_cacheFile}: {e.Message}", typeof(SessionsApi));
        }

        if (file?.Version != CacheVersion || file.Entries == null)
        {
            return;
        }

        var entries = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in file.Entries)
        {
            if (entry?.Path != null && entry.Session?.Id != null)
            {
                entries[entry.Path] = entry;
            }
        }

        lock (_lock)
        {
            _entries = entries;
            _sessions = entries.Values.Select(e => e.Session).ToArray();
        }
    }

    /// <summary>Returns all sessions, refreshing changed transcripts first. Safe to call concurrently.</summary>
    public IReadOnlyList<ClaudeSession> GetSessions()
    {
        lock (_lock)
        {
            if (Refresh())
            {
                _sessions = _entries.Values.Select(e => e.Session).ToArray();
                Save();
            }

            return _sessions;
        }
    }

    /// <summary>Brings <see cref="_entries"/> up to date with the disk. Returns whether anything changed.</summary>
    private bool Refresh()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = new List<FileInfo>();

        foreach (var file in EnumerateTranscripts())
        {
            seen.Add(file.FullName);
            if (!_entries.TryGetValue(file.FullName, out var entry)
                || entry.Size != file.Length
                || entry.MtimeTicks != file.LastWriteTimeUtc.Ticks)
            {
                changed.Add(file);
            }
        }

        var removed = _entries.Keys.Where(path => !seen.Contains(path)).ToList();
        foreach (var path in removed)
        {
            _entries.Remove(path);
        }

        if (changed.Count == 0)
        {
            return removed.Count > 0;
        }

        var scanned = new ConcurrentBag<CacheEntry>();
        var failed = new ConcurrentBag<string>();
        Parallel.ForEach(changed, file =>
        {
            try
            {
                // Keyed by the stat taken *before* reading: if Claude appends while we read,
                // the next refresh sees a different size/mtime and scans again.
                var session = _scan(file.FullName);
                if (session != null)
                {
                    scanned.Add(new CacheEntry(file.FullName, file.Length, file.LastWriteTimeUtc.Ticks, session));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Skipping unreadable transcript {file.FullName}: {e.Message}", typeof(SessionsApi));
                failed.Add(file.FullName);
            }
        });

        foreach (var entry in scanned)
        {
            _entries[entry.Path] = entry;
        }

        foreach (var path in failed)
        {
            _entries.Remove(path);
        }

        return true;
    }

    /// <summary><c>projects\*\*.jsonl</c> whose stem is a GUID. Deeper files (subagents) are not sessions.</summary>
    private IEnumerable<FileInfo> EnumerateTranscripts()
    {
        var projects = new DirectoryInfo(_projectsDir);
        if (!projects.Exists)
        {
            yield break;
        }

        IEnumerable<DirectoryInfo> dirs;
        try
        {
            dirs = projects.EnumerateDirectories().ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Cannot list {_projectsDir}: {e.Message}", typeof(SessionsApi));
            yield break;
        }

        foreach (var dir in dirs)
        {
            List<FileInfo> files;
            try
            {
                files = dir.EnumerateFiles("*.jsonl").ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Cannot list {dir.FullName}: {e.Message}", typeof(SessionsApi));
                continue;
            }

            foreach (var file in files)
            {
                if (Guid.TryParse(Path.GetFileNameWithoutExtension(file.Name), out _))
                {
                    yield return file;
                }
            }
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);
            var temp = _cacheFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new CacheFile(CacheVersion, _entries.Values.ToList()), JsonOptions));
            File.Move(temp, _cacheFile, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Cannot write session cache {_cacheFile}: {e.Message}", typeof(SessionsApi));
        }
    }

    private sealed record CacheFile(int Version, List<CacheEntry>? Entries);

    private sealed record CacheEntry(string Path, long Size, long MtimeTicks, ClaudeSession Session);
}
