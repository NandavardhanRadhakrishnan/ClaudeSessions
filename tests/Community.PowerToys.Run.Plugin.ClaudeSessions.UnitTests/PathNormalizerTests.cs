using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class PathNormalizerTests
{
    [DataTestMethod]
    [DataRow(@"C:\code\x", @"C:\code\x")]
    [DataRow("C:/code/x", @"C:\code\x")]
    [DataRow("/c/code/x", @"C:\code\x")]
    [DataRow("/d/projects/x", @"D:\projects\x")]
    [DataRow("/home/u/x", "/home/u/x")]
    [DataRow(@"C:\code\x\", @"C:\code\x")]
    [DataRow("C:/code/x/", @"C:\code\x")]
    [DataRow("/c/code/x/", @"C:\code\x")]
    [DataRow("/home/u/x/", "/home/u/x")]
    [DataRow(@"C:\", @"C:\")]
    [DataRow("/c", @"C:\")]
    [DataRow("/c/", @"C:\")]
    [DataRow(@"\\server\share\x\", @"\\server\share\x")]
    [DataRow("", "")]
    public void Normalize(string raw, string expected)
    {
        Assert.AreEqual(expected, PathNormalizer.Normalize(raw));
    }

    [DataTestMethod]
    [DataRow(@"C:\code\acme-api", "acme-api")]
    [DataRow("/home/u/x", "x")]
    [DataRow(@"C:\", @"C:\")]
    [DataRow("", "")]
    public void FolderName(string cwd, string expected)
    {
        Assert.AreEqual(expected, PathNormalizer.FolderName(cwd));
    }

    [DataTestMethod]
    [DataRow(@"C:\code\x", true)]
    [DataRow(@"\\server\share", true)]
    [DataRow("/home/u/x", false)]
    [DataRow("", false)]
    public void IsWindowsPath(string cwd, bool expected)
    {
        Assert.AreEqual(expected, PathNormalizer.IsWindowsPath(cwd));
    }
}
