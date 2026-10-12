using System;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

internal static class TestSessions
{
    public static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    public static ClaudeSession Make(string title, string cwd = @"C:\code\acme-api", double hoursAgo = 1, int prompts = 1, string? id = null) =>
        new(id ?? Guid.NewGuid().ToString(), title, cwd, Now.AddHours(-hoursAgo), prompts);
}
