using System;
using System.Diagnostics;
using System.Text;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Settings;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;

/// <summary>Builds the processes that open a session. No side effects.</summary>
public static class LaunchCommand
{
    /// <summary>
    /// <c>wt.exe &lt;window&gt; [-p "&lt;profile&gt;"] -d "&lt;cwd&gt;" --title "&lt;title&gt;" &lt;tab command&gt;</c>, where the tab
    /// command is <c>claude --resume &lt;id&gt; [--fork-session] [&lt;extra args&gt;]</c>, either run directly (the tab
    /// closes with Claude) or inside <paramref name="shell"/> so the tab stays at a prompt after <c>/exit</c>.
    /// </summary>
    /// <param name="claudeCommand">Path or name of the Claude CLI; quoted if it contains spaces.</param>
    /// <param name="elevate">Start <c>wt.exe</c> elevated, for profiles set to run as administrator (see <see cref="TerminalSettings"/>).</param>
    /// <param name="wtPath">Path to <c>wt.exe</c>; see <see cref="TerminalSettings.ResolveWtPath"/>.</param>
    /// <param name="shell">Shell that hosts Claude; <see cref="TerminalShell.None"/> runs Claude as the tab's command line.</param>
    public static ProcessStartInfo Terminal(
        ClaudeSession session,
        PluginSettings settings,
        bool fork,
        string claudeCommand = "claude",
        bool elevate = false,
        string wtPath = "wt.exe",
        TerminalShell shell = TerminalShell.None)
    {
        var args = new StringBuilder(settings.WindowMode == WindowMode.NewTab ? "-w 0 nt" : "-w new");

        if (settings.TerminalProfile.Length > 0)
        {
            args.Append(" -p ").Append(Quote(EscapeForWt(settings.TerminalProfile.Replace("\"", string.Empty))));
        }

        args.Append(" -d ").Append(Quote(session.Cwd));
        args.Append(" --title ").Append(Quote(EscapeForWt(session.Title.Replace("\"", string.Empty))));

        var claudeArgs = $"--resume {session.Id}" + (fork ? " --fork-session" : string.Empty);
        var extraArgs = settings.ExtraArgs.Trim();

        switch (shell)
        {
            case TerminalShell.WindowsPowerShell or TerminalShell.PowerShellCore:
                // Encoded, so neither wt nor argv quoting touches the command; extra args use PowerShell syntax.
                var command = $"& '{claudeCommand.Replace("'", "''")}' {claudeArgs}" + (extraArgs.Length > 0 ? " " + extraArgs : string.Empty);
                args.Append(shell == TerminalShell.PowerShellCore ? " pwsh.exe" : " powershell.exe")
                    .Append(" -NoExit -EncodedCommand ")
                    .Append(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
                break;

            case TerminalShell.Cmd:
                // cmd /k mangles a quoted program path followed by more quotes; cmd finds "claude" on PATH.
                args.Append(" cmd.exe /k ").Append(claudeCommand.Contains(' ') ? "claude" : claudeCommand);
                AppendClaudeArgs(args, claudeArgs, extraArgs);
                break;

            default:
                args.Append(' ').Append(claudeCommand.Contains(' ') ? Quote(claudeCommand) : claudeCommand);
                AppendClaudeArgs(args, claudeArgs, extraArgs);
                break;
        }

        var startInfo = new ProcessStartInfo(wtPath, args.ToString()) { UseShellExecute = true };
        if (elevate)
        {
            startInfo.Verb = "runas";
        }

        return startInfo;
    }

    /// <summary>
    /// Opens the session in the Claude desktop app's Code tab. <c>claude://resume?session=&lt;id&gt;</c> imports the CLI
    /// session as a desktop session (or opens the one that already owns it), the same path as the app's resume picker.
    /// </summary>
    public static ProcessStartInfo DesktopApp(ClaudeSession session) =>
        new($"claude://resume?session={Uri.EscapeDataString(session.Id)}") { UseShellExecute = true };

    public static ProcessStartInfo Explorer(string folder) => new("explorer.exe", Quote(folder)) { UseShellExecute = true };

    /// <summary>PowerShell-friendly command that resumes the session from its folder.</summary>
    public static string ResumeCommandText(ClaudeSession session) => $"cd \"{session.Cwd}\"; claude --resume {session.Id}";

    private static void AppendClaudeArgs(StringBuilder args, string claudeArgs, string extraArgs)
    {
        args.Append(' ').Append(claudeArgs);
        if (extraArgs.Length > 0)
        {
            args.Append(' ').Append(EscapeForWt(extraArgs));
        }
    }

    /// <summary><c>wt</c> treats an unescaped <c>;</c> as a separator between its own subcommands.</summary>
    private static string EscapeForWt(string value) => value.Replace(";", "\\;");

    /// <summary>Quotes one argument so that it survives standard Windows command-line parsing.</summary>
    private static string Quote(string value)
    {
        var builder = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in value)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            // Backslashes are literal unless they precede a quote.
            builder.Append('\\', c == '"' ? (backslashes * 2) + 1 : backslashes);
            backslashes = 0;
            builder.Append(c);
        }

        // ...or the closing quote.
        builder.Append('\\', backslashes * 2).Append('"');
        return builder.ToString();
    }
}
