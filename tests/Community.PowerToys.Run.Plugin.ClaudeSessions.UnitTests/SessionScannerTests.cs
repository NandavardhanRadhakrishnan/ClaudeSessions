using System;
using System.IO;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class SessionScannerTests
{
    private const string Id = "11111111-1111-1111-1111-111111111111";

    private static string _tempDir = string.Empty;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cs-scanner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [ClassCleanup]
    public static void ClassCleanup() => Directory.Delete(_tempDir, recursive: true);

    // Copies a fixture to <guid>.jsonl, since the session ID is taken from the file name.
    private static ClaudeSession Scan(string fixture, string id = Id)
    {
        var path = Path.Combine(_tempDir, id + ".jsonl");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), path, overwrite: true);
        var session = SessionScanner.Scan(path);
        Assert.IsNotNull(session);
        return session;
    }

    [TestMethod]
    public void CustomTitle_WinsOverEverything_AndLastOneWins()
    {
        var s = Scan("titled.jsonl");
        Assert.AreEqual("Fix login bug", s.Title);
        Assert.AreEqual(Id, s.Id);
        Assert.AreEqual(@"C:\code\acme-api", s.Cwd);
        Assert.AreEqual("acme-api", s.FolderName);
        Assert.AreEqual(2, s.UserPromptCount);
    }

    [TestMethod]
    public void AgentName_WinsOverAiTitle_AndLastOneWins()
    {
        Assert.AreEqual("agent two", Scan("agent-name.jsonl").Title);
    }

    [TestMethod]
    public void AiTitle_WinsOverFirstPrompt_AndLastOneWins()
    {
        Assert.AreEqual("AI two", Scan("ai-title.jsonl").Title);
    }

    [TestMethod]
    public void FirstPrompt_SkipsMetaWrappersAndToolResults_AndIsSingleLineAndTruncated()
    {
        var s = Scan("first-prompt.jsonl");
        Assert.IsTrue(s.Title.StartsWith("Refactor the payment module so that it uses", StringComparison.Ordinal), s.Title);
        Assert.IsFalse(s.Title.Contains('\n'));
        Assert.IsTrue(s.Title.Length <= 80, s.Title.Length.ToString());
        Assert.IsTrue(s.Title.EndsWith('\u2026'));
        Assert.AreEqual(2, s.UserPromptCount);
        Assert.AreEqual(@"C:\code\proj", s.Cwd);
    }

    [TestMethod]
    public void CorruptLines_AreSkipped()
    {
        var s = Scan("corrupt.jsonl");
        Assert.AreEqual("valid prompt", s.Title);
        Assert.AreEqual(1, s.UserPromptCount);
        Assert.AreEqual(@"C:\code\x", s.Cwd);
    }

    [TestMethod]
    public void EmptyFile_IsUntitledWithZeroPrompts()
    {
        var s = Scan("empty.jsonl");
        Assert.AreEqual("(untitled) 11111111", s.Title);
        Assert.AreEqual(0, s.UserPromptCount);
        Assert.AreEqual(string.Empty, s.Cwd);
    }

    [TestMethod]
    public void NoPrompts_IsUntitled_ButKeepsCwd()
    {
        var s = Scan("no-prompts.jsonl");
        Assert.AreEqual("(untitled) 11111111", s.Title);
        Assert.AreEqual(0, s.UserPromptCount);
        Assert.AreEqual(@"C:\code\y", s.Cwd);
    }

    [TestMethod]
    public void LastActive_IsFileWriteTime()
    {
        var s = Scan("ai-title.jsonl");
        var expected = File.GetLastWriteTimeUtc(Path.Combine(_tempDir, Id + ".jsonl"));
        Assert.AreEqual(expected, s.LastActive);
    }

    [TestMethod]
    public void NonGuidFileName_ReturnsNull()
    {
        var path = Path.Combine(_tempDir, "not-a-guid.jsonl");
        File.WriteAllText(path, string.Empty);
        Assert.IsNull(SessionScanner.Scan(path));
    }
}
