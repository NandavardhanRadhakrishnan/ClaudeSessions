using System.Linq;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Search;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class QueryParserTests
{
    [TestMethod]
    public void PlainText()
    {
        var q = QueryParser.Parse("fix login");
        Assert.AreEqual("fix login", q.Text);
        Assert.AreEqual(0, q.FolderFilters.Count);
    }

    [TestMethod]
    public void FolderOnly()
    {
        var q = QueryParser.Parse("@acme");
        Assert.AreEqual(string.Empty, q.Text);
        CollectionAssert.AreEqual(new[] { "acme" }, q.FolderFilters.ToArray());
    }

    [TestMethod]
    public void Mixed()
    {
        var q = QueryParser.Parse("login @acme bug");
        Assert.AreEqual("login bug", q.Text);
        CollectionAssert.AreEqual(new[] { "acme" }, q.FolderFilters.ToArray());
    }

    [TestMethod]
    public void LoneAt_IsText()
    {
        var q = QueryParser.Parse("login @");
        Assert.AreEqual("login @", q.Text);
        Assert.AreEqual(0, q.FolderFilters.Count);
    }

    [TestMethod]
    public void MultipleFolderFilters()
    {
        var q = QueryParser.Parse("@code @api x");
        Assert.AreEqual("x", q.Text);
        CollectionAssert.AreEqual(new[] { "code", "api" }, q.FolderFilters.ToArray());
    }

    [TestMethod]
    public void ExtraWhitespace_IsCollapsed()
    {
        var q = QueryParser.Parse("   fix \t  login   @acme   ");
        Assert.AreEqual("fix login", q.Text);
        CollectionAssert.AreEqual(new[] { "acme" }, q.FolderFilters.ToArray());
    }

    [TestMethod]
    public void NullOrEmpty()
    {
        Assert.AreEqual(string.Empty, QueryParser.Parse(null).Text);
        Assert.AreEqual(string.Empty, QueryParser.Parse("  ").Text);
    }
}
