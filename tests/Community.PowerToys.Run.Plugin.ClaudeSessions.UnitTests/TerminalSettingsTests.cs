using Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class TerminalSettingsTests
{
    private const string Settings = """
        // Windows Terminal settings may contain comments and trailing commas
        {
            "defaultProfile": "{11111111-0000-0000-0000-000000000000}",
            "profiles":
            {
                "defaults": { "elevate": true, },
                "list":
                [
                    { "guid": "{11111111-0000-0000-0000-000000000000}", "name": "PowerShell" },
                    { "guid": "{22222222-0000-0000-0000-000000000000}", "name": "Ubuntu", "elevate": false },
                    { "guid": "{33333333-0000-0000-0000-000000000000}", "name": "Admin Cmd", "elevate": true },
                ]
            }
        }
        """;

    [TestMethod]
    public void DefaultProfile_InheritsDefaultsElevate()
    {
        Assert.IsTrue(TerminalSettings.ProfileRequiresElevation(Settings, string.Empty));
    }

    [TestMethod]
    public void NamedProfile_OverridesDefaults()
    {
        Assert.IsFalse(TerminalSettings.ProfileRequiresElevation(Settings, "ubuntu"));
        Assert.IsTrue(TerminalSettings.ProfileRequiresElevation(Settings, "Admin Cmd"));
    }

    [TestMethod]
    public void ProfileByGuid()
    {
        Assert.IsFalse(TerminalSettings.ProfileRequiresElevation(Settings, "{22222222-0000-0000-0000-000000000000}"));
    }

    [TestMethod]
    public void UnknownProfile_FallsBackToDefaults()
    {
        Assert.IsTrue(TerminalSettings.ProfileRequiresElevation(Settings, "Some dynamic profile"));
    }

    [TestMethod]
    public void NoElevateAnywhere_IsFalse()
    {
        const string json = """{ "profiles": [ { "guid": "{1}", "name": "A" } ] }""";
        Assert.IsFalse(TerminalSettings.ProfileRequiresElevation(json, string.Empty));
        Assert.IsFalse(TerminalSettings.ProfileRequiresElevation(json, "A"));
    }

    [TestMethod]
    public void DefaultProfileGivenByName()
    {
        const string json = """{ "defaultProfile": "Admin", "profiles": { "list": [ { "name": "Admin", "elevate": true } ] } }""";
        Assert.IsTrue(TerminalSettings.ProfileRequiresElevation(json, string.Empty));
    }

    private const string ShellSettings = """
        {
            "defaultProfile": "{0caa0dad-35be-5f56-a8ff-afceeeaa6101}",
            "profiles": { "list": [
                { "guid": "{0caa0dad-35be-5f56-a8ff-afceeeaa6101}", "name": "Command Prompt" },
                { "guid": "{61c54bbd-c2c6-5271-96e7-009a87ff44bf}", "name": "Windows PowerShell" },
                { "guid": "{574e775e-4f2a-5b96-ac1e-a2962a402336}", "name": "PowerShell", "source": "Windows.Terminal.PowershellCore" },
                { "name": "My pwsh", "commandline": "C:\\Program Files\\PowerShell\\7\\pwsh.exe -NoLogo" },
                { "name": "Old PS", "commandline": "%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe" },
                { "name": "Conda", "commandline": "%windir%\\System32\\cmd.exe /K activate.bat" },
                { "name": "Ubuntu", "source": "Windows.Terminal.Wsl" }
            ] }
        }
        """;

    [DataTestMethod]
    [DataRow("", TerminalShell.Cmd)]
    [DataRow("Command Prompt", TerminalShell.Cmd)]
    [DataRow("Windows PowerShell", TerminalShell.WindowsPowerShell)]
    [DataRow("PowerShell", TerminalShell.PowerShellCore)]
    [DataRow("My pwsh", TerminalShell.PowerShellCore)]
    [DataRow("Old PS", TerminalShell.WindowsPowerShell)]
    [DataRow("Conda", TerminalShell.Cmd)]
    [DataRow("Ubuntu", TerminalShell.Cmd)]
    [DataRow("Not a profile", TerminalShell.Cmd)]
    public void ProfileShell(string profile, TerminalShell expected)
    {
        Assert.AreEqual(expected, TerminalSettings.ProfileShell(ShellSettings, profile));
    }

    [TestMethod]
    public void ProfileShell_InvalidJson_IsCmd()
    {
        Assert.AreEqual(TerminalShell.Cmd, TerminalSettings.ProfileShell("{ nope", string.Empty));
    }

    [TestMethod]
    public void InvalidJson_IsFalse()
    {
        Assert.IsFalse(TerminalSettings.ProfileRequiresElevation("{ not json", string.Empty));
        Assert.IsFalse(TerminalSettings.ProfileRequiresElevation(string.Empty, string.Empty));
    }
}
