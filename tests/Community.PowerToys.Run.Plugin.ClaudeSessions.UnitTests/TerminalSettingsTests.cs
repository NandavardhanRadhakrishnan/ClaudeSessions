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

    [TestMethod]
    public void InvalidJson_IsFalse()
    {
        Assert.IsFalse(TerminalSettings.ProfileRequiresElevation("{ not json", string.Empty));
        Assert.IsFalse(TerminalSettings.ProfileRequiresElevation(string.Empty, string.Empty));
    }
}
