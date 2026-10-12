using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class SessionsApiTests
{
    private const string IdA = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string IdB = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    private string _home = string.Empty;
    private string _cacheFile = string.Empty;
    private List<string> _scanned = new();

    [TestInitialize]
    public void Init()
    {
        _home = Path.Combine(Path.GetTempPath(), "cs-api-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_home, "projects"));
        _cacheFile = Path.Combine(_home, "cache", "session-cache.json");
        _scanned = new List<string>();
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_home, recursive: true);

    private SessionsApi NewApi() => new(_home, _cacheFile, path =>
    {
        lock (_scanned)
        {
            _scanned.Add(Path.GetFileNameWithoutExtension(path));
        }

        return SessionScanner.Scan(path);
    });

    private string WriteSession(string project, string id, string prompt = "hello")
    {
        var dir = Path.Combine(_home, "projects", project);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, id + ".jsonl");
        File.WriteAllText(path, $"{{\"type\":\"user\",\"message\":{{\"role\":\"user\",\"content\":\"{prompt}\"}},\"cwd\":\"C:\\\\code\\\\{project}\"}}\n");
        return path;
    }

    [TestMethod]
    public void FindsSessions_IgnoresSubagentsAndNonGuidFiles()
    {
        WriteSession("p1", IdA);
        WriteSession("p2", IdB);
        var sub = Path.Combine(_home, "projects", "p1", IdA, "subagents");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "cccccccc-cccc-cccc-cccc-cccccccccccc.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(_home, "projects", "p1", "notes.jsonl"), "{}\n");

        var ids = NewApi().GetSessions().Select(s => s.Id).OrderBy(x => x).ToArray();

        CollectionAssert.AreEqual(new[] { IdA, IdB }, ids);
    }

    [TestMethod]
    public void UnchangedFile_IsNotRescanned_InMemoryOrAcrossInstances()
    {
        WriteSession("p1", IdA);
        var api = NewApi();
        api.GetSessions();
        api.GetSessions();
        Assert.AreEqual(1, _scanned.Count);

        var second = NewApi();
        second.Load();
        Assert.AreEqual(1, second.GetSessions().Count);
        Assert.AreEqual(1, _scanned.Count, "cache file should have been reused");
    }

    [TestMethod]
    public void ChangedSize_IsRescanned()
    {
        var path = WriteSession("p1", IdA, "first");
        var api = NewApi();
        Assert.AreEqual("first", api.GetSessions().Single().Title);

        var mtime = File.GetLastWriteTimeUtc(path);
        File.AppendAllText(path, "{\"type\":\"custom-title\",\"customTitle\":\"renamed\"}\n");
        File.SetLastWriteTimeUtc(path, mtime);

        Assert.AreEqual("renamed", api.GetSessions().Single().Title);
        Assert.AreEqual(2, _scanned.Count);
    }

    [TestMethod]
    public void ChangedMtime_IsRescanned()
    {
        var path = WriteSession("p1", IdA);
        var api = NewApi();
        api.GetSessions();

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
        var session = api.GetSessions().Single();

        Assert.AreEqual(2, _scanned.Count);
        Assert.AreEqual(File.GetLastWriteTimeUtc(path), session.LastActive);
    }

    [TestMethod]
    public void DeletedFile_IsDropped()
    {
        WriteSession("p1", IdA);
        var b = WriteSession("p1", IdB);
        var api = NewApi();
        Assert.AreEqual(2, api.GetSessions().Count);

        File.Delete(b);

        Assert.AreEqual(IdA, api.GetSessions().Single().Id);
    }

    [TestMethod]
    public void CorruptCache_IsIgnoredAndRebuilt()
    {
        WriteSession("p1", IdA);
        Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);
        File.WriteAllText(_cacheFile, "{ this is not json");

        var api = NewApi();
        api.Load();

        Assert.AreEqual(1, api.GetSessions().Count);
        Assert.AreEqual(1, _scanned.Count);
        StringAssert.Contains(File.ReadAllText(_cacheFile), IdA);
    }

    [TestMethod]
    public void WrongVersionCache_IsIgnored()
    {
        WriteSession("p1", IdA);
        NewApi().GetSessions();
        var json = File.ReadAllText(_cacheFile).Replace("\"version\":1", "\"version\":999", StringComparison.Ordinal);
        File.WriteAllText(_cacheFile, json);
        _scanned.Clear();

        var api = NewApi();
        api.Load();
        api.GetSessions();

        Assert.AreEqual(1, _scanned.Count);
    }

    [TestMethod]
    public void GetTranscriptPath_ReturnsScannedFile()
    {
        var path = WriteSession("p1", IdA);
        var api = NewApi();
        api.GetSessions();

        Assert.AreEqual(path, api.GetTranscriptPath(IdA), ignoreCase: true);
        Assert.IsNull(api.GetTranscriptPath(IdB));
    }

    [TestMethod]
    public void MissingProjectsDirectory_ReturnsEmpty()
    {
        Directory.Delete(Path.Combine(_home, "projects"));
        Assert.AreEqual(0, NewApi().GetSessions().Count);
    }

    [TestMethod]
    public void ConcurrentQueries_AreSafe()
    {
        for (var i = 0; i < 20; i++)
        {
            WriteSession("p" + i, Guid.NewGuid().ToString());
        }

        var api = NewApi();
        var threads = Enumerable.Range(0, 8).Select(_ => new Thread(() => Assert.AreEqual(20, api.GetSessions().Count))).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.AreEqual(20, _scanned.Count);
    }

    [DataTestMethod]
    [DataRow(@"D:\cfg", @"C:\Users\u", @"D:\cfg")]
    [DataRow(null, @"C:\Users\u", @"C:\Users\u\.claude")]
    [DataRow("", @"C:\Users\u", @"C:\Users\u\.claude")]
    public void ResolveClaudeHome(string? configDir, string profile, string expected)
    {
        Assert.AreEqual(expected, SessionsApi.ResolveClaudeHome(configDir, profile));
    }
}
