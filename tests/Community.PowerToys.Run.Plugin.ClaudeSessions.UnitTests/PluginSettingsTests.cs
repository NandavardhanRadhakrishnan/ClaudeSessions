using System;
using System.Linq;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public class PluginSettingsTests
{
    [TestMethod]
    public void Defaults_MatchSpec()
    {
        var s = new PluginSettings();
        Assert.AreEqual(OpenTarget.Terminal, s.OpenTarget);
        Assert.AreEqual(WindowMode.NewWindow, s.WindowMode);
        Assert.AreEqual(ExitBehavior.StayInShell, s.OnExit);
        Assert.AreEqual(string.Empty, s.TerminalProfile);
        Assert.AreEqual(string.Empty, s.ExtraArgs);
        Assert.IsTrue(s.HideEmpty);
        Assert.AreEqual(90, s.MaxAgeDays);
        Assert.AreEqual(30, s.MaxResults);
    }

    [TestMethod]
    public void DefaultOptions_RoundTripToDefaults()
    {
        Assert.AreEqual(new PluginSettings(), PluginSettings.FromOptions(PluginSettings.DefaultOptions()));
    }

    [TestMethod]
    public void NullOptions_GiveDefaults()
    {
        Assert.AreEqual(new PluginSettings(), PluginSettings.FromOptions(null));
    }

    [TestMethod]
    public void ChangedOptions_AreRead()
    {
        var options = PluginSettings.DefaultOptions().ToList();
        options.Single(o => o.Key == "OpenTarget").ComboBoxValue = (int)OpenTarget.Desktop;
        options.Single(o => o.Key == "WindowMode").ComboBoxValue = (int)WindowMode.NewTab;
        options.Single(o => o.Key == "OnExit").ComboBoxValue = (int)ExitBehavior.CloseTab;
        options.Single(o => o.Key == "TerminalProfile").TextValue = " Ubuntu ";
        options.Single(o => o.Key == "ExtraArgs").TextValue = "--verbose";
        options.Single(o => o.Key == "HideEmpty").Value = false;
        options.Single(o => o.Key == "MaxAgeDays").NumberValue = 0;
        options.Single(o => o.Key == "MaxResults").NumberValue = 5;

        var s = PluginSettings.FromOptions(options);

        Assert.AreEqual(new PluginSettings
        {
            OpenTarget = OpenTarget.Desktop,
            WindowMode = WindowMode.NewTab,
            OnExit = ExitBehavior.CloseTab,
            TerminalProfile = "Ubuntu",
            ExtraArgs = "--verbose",
            HideEmpty = false,
            MaxAgeDays = 0,
            MaxResults = 5,
        }, s);
    }

    [TestMethod]
    public void OutOfRangeNumbers_AreClamped()
    {
        var options = PluginSettings.DefaultOptions().ToList();
        options.Single(o => o.Key == "MaxAgeDays").NumberValue = -3;
        options.Single(o => o.Key == "MaxResults").NumberValue = 0;

        var s = PluginSettings.FromOptions(options);

        Assert.AreEqual(0, s.MaxAgeDays);
        Assert.AreEqual(1, s.MaxResults);
    }
}
