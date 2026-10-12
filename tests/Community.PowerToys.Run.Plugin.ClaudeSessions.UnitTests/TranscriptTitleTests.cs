using System;
using System.IO;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class TranscriptTitleTests
{
    private const string Id = "11111111-1111-1111-1111-111111111111";

    private string _dir = string.Empty;
    private string _path = string.Empty;

    [TestInitialize]
    public void Init()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cs-title-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, Id + ".jsonl");
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_dir, recursive: true);

    [TestMethod]
    public void AppendsCustomTitle_WhenTranscriptHasNone()
    {
        File.WriteAllText(_path, "{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":\"hi\"},\"cwd\":\"C:\\\\x\"}\n{\"type\":\"ai-title\",\"aiTitle\":\"Train MFD\"}\n");

        Assert.IsTrue(TranscriptTitle.EnsureCustomTitle(_path, Id, "Train \"MFD\" system"));

        var lines = File.ReadAllLines(_path);
        Assert.AreEqual(3, lines.Length);
        Assert.AreEqual($"{{\"type\":\"custom-title\",\"customTitle\":\"Train \\\"MFD\\\" system\",\"sessionId\":\"{Id}\"}}", lines[2]);
        Assert.AreEqual("Train \"MFD\" system", SessionScanner.Scan(_path)!.Title);
    }

    [TestMethod]
    public void AddsMissingNewlineBeforeAppending()
    {
        File.WriteAllText(_path, "{\"type\":\"ai-title\",\"aiTitle\":\"t\"}");

        TranscriptTitle.EnsureCustomTitle(_path, Id, "t");

        var lines = File.ReadAllLines(_path);
        Assert.AreEqual(2, lines.Length);
        StringAssert.StartsWith(lines[1], "{\"type\":\"custom-title\"");
    }

    [TestMethod]
    public void LeavesTranscriptAlone_WhenItAlreadyHasCustomTitle()
    {
        var original = "{\"type\":\"custom-title\",\"customTitle\":\"mine\"}\n";
        File.WriteAllText(_path, original);

        Assert.IsFalse(TranscriptTitle.EnsureCustomTitle(_path, Id, "other"));
        Assert.AreEqual(original, File.ReadAllText(_path));
    }

    [TestMethod]
    public void SkipsPlaceholderTitles()
    {
        File.WriteAllText(_path, string.Empty);

        Assert.IsFalse(TranscriptTitle.EnsureCustomTitle(_path, Id, "(untitled) 11111111"));
        Assert.IsFalse(TranscriptTitle.EnsureCustomTitle(_path, Id, "  "));
        Assert.AreEqual(0, new FileInfo(_path).Length);
    }

    [TestMethod]
    public void IsImportedIntoDesktop_FindsRecordByCliSessionId()
    {
        var records = Path.Combine(_dir, "claude-code-sessions", "acct", "org");
        Directory.CreateDirectory(records);
        File.WriteAllText(Path.Combine(records, "local_abc.json"), $"{{\"sessionId\":\"local_abc\",\"cliSessionId\":\"{Id}\"}}");

        Assert.IsTrue(TranscriptTitle.IsImportedIntoDesktop(Id, Path.Combine(_dir, "claude-code-sessions")));
        Assert.IsFalse(TranscriptTitle.IsImportedIntoDesktop("22222222-2222-2222-2222-222222222222", Path.Combine(_dir, "claude-code-sessions")));
        Assert.IsFalse(TranscriptTitle.IsImportedIntoDesktop(Id, Path.Combine(_dir, "missing")));
    }
}
