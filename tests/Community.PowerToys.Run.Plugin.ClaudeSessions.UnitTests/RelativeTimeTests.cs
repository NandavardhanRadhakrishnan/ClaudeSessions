using System;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests.TestSessions;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class RelativeTimeTests
{
    [DataTestMethod]
    [DataRow(0.0, "just now")]
    [DataRow(-5.0, "just now")]
    [DataRow(5.0, "5m ago")]
    [DataRow(120.0, "2h ago")]
    [DataRow(60.0 * 24 * 3, "3d ago")]
    [DataRow(60.0 * 24 * 29, "29d ago")]
    public void Format(double minutesAgo, string expected)
    {
        Assert.AreEqual(expected, RelativeTime.Format(Now.AddMinutes(-minutesAgo), Now));
    }

    [TestMethod]
    public void OlderThanAMonth_ShowsDate()
    {
        Assert.AreEqual("2026-08-01", RelativeTime.Format(new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc), Now));
    }
}
