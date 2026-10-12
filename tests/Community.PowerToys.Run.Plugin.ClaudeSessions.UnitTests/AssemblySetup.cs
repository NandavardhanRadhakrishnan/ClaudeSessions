using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Infrastructure;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests;

[TestClass]
public static class AssemblySetup
{
    // PowerToys Run sets the shared matcher at startup through a non-public setter;
    // the test host has to do the same.
    [AssemblyInitialize]
    public static void Init(TestContext context)
    {
        var instance = typeof(StringMatcher).GetProperty(nameof(StringMatcher.Instance), BindingFlags.Public | BindingFlags.Static)!;
        if (instance.GetValue(null) == null)
        {
            instance.SetMethod!.Invoke(null, [new StringMatcher(null)]);
        }
    }
}
