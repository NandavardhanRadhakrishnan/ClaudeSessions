namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;

/// <summary>Shell that hosts Claude in the Windows Terminal tab.</summary>
public enum TerminalShell
{
    /// <summary>Claude is the tab's command line; the tab closes when Claude exits.</summary>
    None,
    Cmd,
    WindowsPowerShell,
    PowerShellCore,
}
