using System;
using System.Collections.Generic;
using System.Linq;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;
using Wox.Infrastructure;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Search;

/// <param name="HideEmpty">Hide sessions with no user prompts.</param>
/// <param name="MaxAgeDays">Hide sessions older than this many days; 0 = no limit.</param>
/// <param name="MaxResults">Maximum number of matches returned.</param>
public sealed record MatchOptions(bool HideEmpty, int MaxAgeDays, int MaxResults);

/// <param name="Session">The matched session.</param>
/// <param name="TitleHighlight">Indexes of title characters that matched the search text.</param>
public sealed record SessionMatch(ClaudeSession Session, IReadOnlyList<int> TitleHighlight);

public static class SessionMatcher
{
    /// <summary>Filters and orders sessions for a query; the result order is the display order.</summary>
    public static IReadOnlyList<SessionMatch> Match(IEnumerable<ClaudeSession> sessions, ParsedQuery query, MatchOptions options, DateTime nowUtc)
    {
        var minActive = options.MaxAgeDays > 0 ? nowUtc.AddDays(-options.MaxAgeDays) : DateTime.MinValue;

        var candidates = sessions.Where(s =>
            (!options.HideEmpty || s.UserPromptCount > 0)
            && s.LastActive >= minActive
            && query.FolderFilters.All(f =>
                s.FolderName.Contains(f, StringComparison.OrdinalIgnoreCase)
                || s.Cwd.Contains(f, StringComparison.OrdinalIgnoreCase)));

        if (query.Text.Length == 0)
        {
            return candidates
                .OrderByDescending(s => s.LastActive)
                .Take(options.MaxResults)
                .Select(s => new SessionMatch(s, Array.Empty<int>()))
                .ToList();
        }

        return candidates
            .Select(s => (Session: s, Result: StringMatcher.FuzzySearch(query.Text, s.Title)))
            .Where(m => m.Result.Score > 0)
            .OrderByDescending(m => m.Result.Score)
            .ThenByDescending(m => m.Session.LastActive)
            .Take(options.MaxResults)
            .Select(m => new SessionMatch(m.Session, (IReadOnlyList<int>?)m.Result.MatchData ?? Array.Empty<int>()))
            .ToList();
    }
}
