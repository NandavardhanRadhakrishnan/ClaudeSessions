using Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests.TestSessions;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class LaunchCommandTests
{
    private const string Id = "11111111-1111-1111-1111-111111111111";

    private static readonly PluginSettings Defaults = new();

    [TestMethod]
    public void NewWindow_DefaultProfile()
    {
        var psi = LaunchCommand.Terminal(Make("fix login bug", id: Id), Defaults, fork: false);
        Assert.AreEqual("wt.exe", psi.FileName);
        Assert.AreEqual($"-w new -d \"C:\\code\\acme-api\" --title \"fix login bug\" claude --resume {Id}", psi.Arguments);
        Assert.IsTrue(psi.UseShellExecute);
    }

    [TestMethod]
    public void NewTab_InMostRecentWindow()
    {
        var psi = LaunchCommand.Terminal(Make("t", id: Id), Defaults with { WindowMode = WindowMode.NewTab }, fork: false);
        Assert.AreEqual($"-w 0 nt -d \"C:\\code\\acme-api\" --title \"t\" claude --resume {Id}", psi.Arguments);
    }

    [TestMethod]
    public void WithProfile()
    {
        var psi = LaunchCommand.Terminal(Make("t", id: Id), Defaults with { TerminalProfile = "PowerShell 7" }, fork: false);
        Assert.AreEqual($"-w new -p \"PowerShell 7\" -d \"C:\\code\\acme-api\" --title \"t\" claude --resume {Id}", psi.Arguments);
    }

    [TestMethod]
    public void Fork_AddsForkSessionFlag()
    {
        var psi = LaunchCommand.Terminal(Make("t", id: Id), Defaults, fork: true);
        Assert.IsTrue(psi.Arguments.EndsWith($"claude --resume {Id} --fork-session", System.StringComparison.Ordinal), psi.Arguments);
    }

    [TestMethod]
    public void ExtraArgs_AreAppendedLast()
    {
        var psi = LaunchCommand.Terminal(Make("t", id: Id), Defaults with { ExtraArgs = "  --dangerously-skip-permissions --model opus " }, fork: true);
        Assert.IsTrue(
            psi.Arguments.EndsWith($"claude --resume {Id} --fork-session --dangerously-skip-permissions --model opus", System.StringComparison.Ordinal),
            psi.Arguments);
    }

    [TestMethod]
    public void PathWithSpaces_IsQuoted()
    {
        var psi = LaunchCommand.Terminal(Make("t", cwd: @"C:\my code\x y", id: Id), Defaults, fork: false);
        StringAssert.Contains(psi.Arguments, "-d \"C:\\my code\\x y\" ");
    }

    [TestMethod]
    public void DriveRoot_TrailingBackslashDoesNotEscapeQuote()
    {
        var psi = LaunchCommand.Terminal(Make("t", cwd: @"C:\", id: Id), Defaults, fork: false);
        StringAssert.Contains(psi.Arguments, "-d \"C:\\\\\" ");
    }

    [TestMethod]
    public void Title_SemicolonsEscaped_QuotesRemoved()
    {
        var psi = LaunchCommand.Terminal(Make("say \"hi\"; then bye", id: Id), Defaults, fork: false);
        StringAssert.Contains(psi.Arguments, "--title \"say hi\\; then bye\" ");
    }

    [TestMethod]
    public void ExtraArgs_SemicolonsEscaped()
    {
        var psi = LaunchCommand.Terminal(Make("t", id: Id), Defaults with { ExtraArgs = "--append-system-prompt \"a;b\"" }, fork: false);
        Assert.IsTrue(psi.Arguments.EndsWith("--append-system-prompt \"a\\;b\"", System.StringComparison.Ordinal), psi.Arguments);
    }

    [TestMethod]
    public void ClaudeCommand_WithSpaces_IsQuoted()
    {
        var psi = LaunchCommand.Terminal(Make("t", id: Id), Defaults, fork: false, claudeCommand: @"C:\Program Files\claude\claude.exe");
        StringAssert.Contains(psi.Arguments, $"\"C:\\Program Files\\claude\\claude.exe\" --resume {Id}");
    }

    [TestMethod]
    public void Elevated_UsesRunAsVerb()
    {
        Assert.AreEqual("runas", LaunchCommand.Terminal(Make("t", id: Id), Defaults, fork: false, elevate: true).Verb);
        Assert.AreEqual(string.Empty, LaunchCommand.Terminal(Make("t", id: Id), Defaults, fork: false).Verb);
    }

    [TestMethod]
    public void ResumeCommandText()
    {
        Assert.AreEqual(
            $"cd \"C:\\my code\\x\"; claude --resume {Id}",
            LaunchCommand.ResumeCommandText(Make("t", cwd: @"C:\my code\x", id: Id)));
    }

    [TestMethod]
    public void DesktopApp_OpensClaudeProtocol()
    {
        var psi = LaunchCommand.DesktopApp();
        Assert.AreEqual("claude://", psi.FileName);
        Assert.IsTrue(psi.UseShellExecute);
    }

    [TestMethod]
    public void Explorer_OpensFolder()
    {
        var psi = LaunchCommand.Explorer(@"C:\my code\x");
        Assert.AreEqual("explorer.exe", psi.FileName);
        Assert.AreEqual("\"C:\\my code\\x\"", psi.Arguments);
    }
}
