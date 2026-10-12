using System.Linq;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Search;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests.TestSessions;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class SessionMatcherTests
{
    private static readonly MatchOptions NoLimits = new(HideEmpty: false, MaxAgeDays: 0, MaxResults: 100);

    private static string[] Titles(ClaudeSession[] sessions, string query, MatchOptions? options = null) =>
        SessionMatcher.Match(sessions, QueryParser.Parse(query), options ?? NoLimits, Now)
            .Select(m => m.Session.Title)
            .ToArray();

    [TestMethod]
    public void FolderFilter_MatchesFolderName_CaseInsensitive()
    {
        var sessions = new[]
        {
            Make("a", @"C:\code\acme-api"),
            Make("b", @"C:\code\other"),
        };
        CollectionAssert.AreEqual(new[] { "a" }, Titles(sessions, "@ACME"));
    }

    [TestMethod]
    public void FolderFilter_MatchesFullPath()
    {
        var sessions = new[]
        {
            Make("a", @"C:\work\client\api"),
            Make("b", @"C:\code\api"),
        };
        CollectionAssert.AreEqual(new[] { "a" }, Titles(sessions, "@client"));
    }

    [TestMethod]
    public void EveryFolderFilter_MustMatch()
    {
        var sessions = new[]
        {
            Make("a", @"C:\work\client\api"),
            Make("b", @"C:\work\client\web"),
        };
        CollectionAssert.AreEqual(new[] { "a" }, Titles(sessions, "@client @api"));
    }

    [TestMethod]
    public void HideEmpty_DropsSessionsWithoutPrompts()
    {
        var sessions = new[] { Make("a", prompts: 0), Make("b", prompts: 3) };
        CollectionAssert.AreEqual(new[] { "b" }, Titles(sessions, string.Empty, NoLimits with { HideEmpty = true }));
        Assert.AreEqual(2, Titles(sessions, string.Empty).Length);
    }

    [TestMethod]
    public void MaxAge_DropsOldSessions_ZeroMeansNoLimit()
    {
        var sessions = new[] { Make("new", hoursAgo: 24 * 2), Make("old", hoursAgo: 24 * 10) };
        CollectionAssert.AreEqual(new[] { "new" }, Titles(sessions, string.Empty, NoLimits with { MaxAgeDays = 7 }));
        Assert.AreEqual(2, Titles(sessions, string.Empty, NoLimits with { MaxAgeDays = 0 }).Length);
    }

    [TestMethod]
    public void MaxResults_Truncates()
    {
        var sessions = Enumerable.Range(0, 10).Select(i => Make("s" + i, hoursAgo: i)).ToArray();
        CollectionAssert.AreEqual(new[] { "s0", "s1", "s2" }, Titles(sessions, string.Empty, NoLimits with { MaxResults = 3 }));
    }

    [TestMethod]
    public void EmptyQuery_OrdersByRecency()
    {
        var sessions = new[] { Make("mid", hoursAgo: 5), Make("newest", hoursAgo: 1), Make("oldest", hoursAgo: 9) };
        CollectionAssert.AreEqual(new[] { "newest", "mid", "oldest" }, Titles(sessions, string.Empty));
    }

    [TestMethod]
    public void FuzzyQuery_FiltersNonMatches_AndOrdersByScore()
    {
        var sessions = new[]
        {
            Make("Update payment docs", hoursAgo: 1),
            Make("Fix login bug", hoursAgo: 50),
            Make("Fix the flaky login integration test", hoursAgo: 2),
        };
        var titles = Titles(sessions, "login");
        Assert.AreEqual(2, titles.Length);
        CollectionAssert.DoesNotContain(titles, "Update payment docs");
        Assert.AreEqual("Fix login bug", Titles(sessions, "fix login bug")[0]);
    }

    [TestMethod]
    public void FuzzyQuery_EqualScores_FallBackToRecency()
    {
        var sessions = new[] { Make("deploy", hoursAgo: 9), Make("deploy", hoursAgo: 1, id: "newer") };
        var matches = SessionMatcher.Match(sessions, QueryParser.Parse("deploy"), NoLimits, Now);
        Assert.AreEqual("newer", matches[0].Session.Id);
    }

    [TestMethod]
    public void FuzzyQuery_ReturnsTitleHighlights()
    {
        var sessions = new[] { Make("Fix login bug") };
        var match = SessionMatcher.Match(sessions, QueryParser.Parse("login"), NoLimits, Now).Single();
        Assert.IsTrue(match.TitleHighlight.Count > 0);
    }
}
