using System;
using System.Globalization;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Properties;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Search;

public static class RelativeTime
{
    /// <summary>"just now", "5m ago", "2h ago", "3d ago", or a date once older than 30 days.</summary>
    public static string Format(DateTime utc, DateTime nowUtc)
    {
        var age = nowUtc - utc;
        if (age.TotalMinutes < 1)
        {
            return Resources.time_just_now;
        }

        if (age.TotalHours < 1)
        {
            return string.Format(CultureInfo.CurrentCulture, Resources.time_minutes_ago, (int)age.TotalMinutes);
        }

        if (age.TotalDays < 1)
        {
            return string.Format(CultureInfo.CurrentCulture, Resources.time_hours_ago, (int)age.TotalHours);
        }

        if (age.TotalDays < 30)
        {
            return string.Format(CultureInfo.CurrentCulture, Resources.time_days_ago, (int)age.TotalDays);
        }

        return utc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
