using System;
using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Search;

/// <param name="Text">Title search text, whitespace-collapsed; empty for "show recent".</param>
/// <param name="FolderFilters">Folder qualifiers from <c>@name</c> tokens, without the <c>@</c>.</param>
public sealed record ParsedQuery(string Text, IReadOnlyList<string> FolderFilters);

public static class QueryParser
{
    public static ParsedQuery Parse(string? search)
    {
        var text = new List<string>();
        var folders = new List<string>();

        foreach (var token in (search ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length > 1 && token[0] == '@')
            {
                folders.Add(token[1..]);
            }
            else
            {
                text.Add(token);
            }
        }

        return new ParsedQuery(string.Join(' ', text), folders);
    }
}
