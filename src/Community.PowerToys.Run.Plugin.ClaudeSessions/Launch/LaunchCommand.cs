using System.Diagnostics;
using System.Text;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Settings;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;

/// <summary>Builds the processes that open a session. No side effects.</summary>
public static class LaunchCommand
{
    /// <summary>
    /// <c>wt.exe &lt;window&gt; [-p "&lt;profile&gt;"] -d "&lt;cwd&gt;" --title "&lt;title&gt;" claude --resume &lt;id&gt; [--fork-session] [&lt;extra args&gt;]</c>.
    /// <c>claude</c> is the tab's command line, so the tab closes per the profile's <c>closeOnExit</c>.
    /// </summary>
    /// <param name="claudeCommand">Path or name of the Claude CLI; quoted if it contains spaces.</param>
    /// <param name="elevate">Start <c>wt.exe</c> elevated, for profiles set to run as administrator (see <see cref="TerminalSettings"/>).</param>
    /// <param name="wtPath">Path to <c>wt.exe</c>; see <see cref="TerminalSettings.ResolveWtPath"/>.</param>
    public static ProcessStartInfo Terminal(ClaudeSession session, PluginSettings settings, bool fork, string claudeCommand = "claude", bool elevate = false, string wtPath = "wt.exe")
    {
        var args = new StringBuilder(settings.WindowMode == WindowMode.NewTab ? "-w 0 nt" : "-w new");

        if (settings.TerminalProfile.Length > 0)
        {
            args.Append(" -p ").Append(Quote(EscapeForWt(settings.TerminalProfile.Replace("\"", string.Empty))));
        }

        args.Append(" -d ").Append(Quote(session.Cwd));
        args.Append(" --title ").Append(Quote(EscapeForWt(session.Title.Replace("\"", string.Empty))));
        args.Append(' ').Append(claudeCommand.Contains(' ') ? Quote(claudeCommand) : claudeCommand);
        args.Append(" --resume ").Append(session.Id);

        if (fork)
        {
            args.Append(" --fork-session");
        }

        if (!string.IsNullOrWhiteSpace(settings.ExtraArgs))
        {
            args.Append(' ').Append(EscapeForWt(settings.ExtraArgs.Trim()));
        }

        var startInfo = new ProcessStartInfo(wtPath, args.ToString()) { UseShellExecute = true };
        if (elevate)
        {
            startInfo.Verb = "runas";
        }

        return startInfo;
    }

    /// <summary>
    /// Opens the Claude desktop app. It has no deep link for a CLI session (its <c>claude://…/project/…</c>
    /// route only takes cloud project IDs), so the caller copies the session ID for <c>/resume</c>.
    /// </summary>
    public static ProcessStartInfo DesktopApp() => new("claude://") { UseShellExecute = true };

    public static ProcessStartInfo Explorer(string folder) => new("explorer.exe", Quote(folder)) { UseShellExecute = true };

    /// <summary>PowerShell-friendly command that resumes the session from its folder.</summary>
    public static string ResumeCommandText(ClaudeSession session) => $"cd \"{session.Cwd}\"; claude --resume {session.Id}";

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
