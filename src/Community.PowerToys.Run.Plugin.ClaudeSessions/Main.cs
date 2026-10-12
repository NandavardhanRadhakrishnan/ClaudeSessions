using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Launch;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Properties;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Search;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Sessions;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Settings;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions;

public class Main : IPlugin, IContextMenu, ISettingProvider, IPluginI18n, IDisposable
{
    private const string Separator = " · ";
    private const string FluentIcons = "Segoe Fluent Icons,Segoe MDL2 Assets";

    private PluginInitContext? _context;
    private SessionsApi? _api;
    private string _claudeHome = string.Empty;
    private string _iconPath = @"Images\sessions.dark.png";
    private volatile PluginSettings _settings = new();
    private bool _disposed;

    public static string PluginID => "08BED641FE144D12A24B804626640F06";

    public string Name => Resources.plugin_name;

    public string Description => Resources.plugin_description;

    public IEnumerable<PluginAdditionalOption> AdditionalOptions => PluginSettings.DefaultOptions();

    public void Init(PluginInitContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _context.API.ThemeChanged += OnThemeChanged;
        UpdateIconPath(_context.API.GetCurrentTheme());

        _claudeHome = SessionsApi.ResolveClaudeHome();
        var cacheFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "PowerToys", "PowerToys Run", "Settings", "Plugins",
            "Community.PowerToys.Run.Plugin.ClaudeSessions",
            "session-cache.json");
        _api = new SessionsApi(_claudeHome, cacheFile);
        _api.Load();

        // Warm the cache so the first "cc" doesn't pay for a cold scan.
        Task.Run(() =>
        {
            try
            {
                _api.GetSessions();
            }
            catch (Exception e)
            {
                Log.Exception("Initial session scan failed", e, GetType());
            }
        });
    }

    public List<Result> Query(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_api == null)
        {
            return [];
        }

        IReadOnlyList<ClaudeSession> sessions;
        try
        {
            sessions = _api.GetSessions();
        }
        catch (Exception e)
        {
            Log.Exception("Listing sessions failed", e, GetType());
            return [];
        }

        var settings = _settings;
        var parsed = QueryParser.Parse(query.Search);
        var now = DateTime.UtcNow;
        var matches = SessionMatcher.Match(sessions, parsed, new MatchOptions(settings.HideEmpty, settings.MaxAgeDays, settings.MaxResults), now);

        if (sessions.Count == 0 && parsed.Text.Length == 0 && parsed.FolderFilters.Count == 0)
        {
            return
            [
                new Result
                {
                    Title = Resources.no_sessions_title,
                    SubTitle = string.Format(CultureInfo.CurrentCulture, Resources.no_sessions_subtitle, Path.Combine(_claudeHome, "projects")),
                    IcoPath = _iconPath,
                    Action = _ => false,
                },
            ];
        }

        var results = new List<Result>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var session = matches[i].Session;
            results.Add(new Result
            {
                Title = session.Title,
                SubTitle = SubTitle(session, now),
                TitleHighlightData = matches[i].TitleHighlight.ToList(),
                IcoPath = _iconPath,
                ToolTipData = new ToolTipData(
                    session.Title,
                    string.Format(CultureInfo.CurrentCulture, Resources.tooltip_text, session.Cwd, session.Id, session.UserPromptCount)),
                ContextData = session,

                // Keep the matcher's order (recency or fuzzy score) rather than Run's usage boost.
                Score = matches.Count - i,
                DisableUsageBasedScoring = true,
                Action = _ => Open(session, _settings.OpenTarget),
            });
        }

        return results;
    }

    public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult?.ContextData is not ClaudeSession session)
        {
            return [];
        }

        var other = _settings.OpenTarget == OpenTarget.Terminal ? OpenTarget.Desktop : OpenTarget.Terminal;
        var menu = new List<ContextMenuResult>
        {
            new()
            {
                PluginName = Name,
                Title = other == OpenTarget.Desktop ? Resources.context_open_in_desktop : Resources.context_open_in_terminal,
                Glyph = other == OpenTarget.Desktop ? "\xE8A7" : "\xE756",
                FontFamily = FluentIcons,
                AcceleratorKey = Key.Enter,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ => Open(session, other),
            },
            new()
            {
                PluginName = Name,
                Title = Resources.context_fork,
                Glyph = "\xE8EE",
                FontFamily = FluentIcons,
                AcceleratorKey = Key.Enter,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ => OpenInTerminal(session, fork: true),
            },
            new()
            {
                PluginName = Name,
                Title = Resources.context_open_folder,
                Glyph = "\xE838",
                FontFamily = FluentIcons,
                AcceleratorKey = Key.F,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ => session.FolderExists ? Start(LaunchCommand.Explorer(session.Cwd)) : FolderMissing(session),
            },
            new()
            {
                PluginName = Name,
                Title = Resources.context_copy_id,
                Glyph = "\xE8C8",
                FontFamily = FluentIcons,
                AcceleratorKey = Key.C,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ => ClipboardHelper.TrySetText(session.Id),
            },
            new()
            {
                PluginName = Name,
                Title = Resources.context_copy_resume,
                Glyph = "\xE943",
                FontFamily = FluentIcons,
                AcceleratorKey = Key.C,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ => ClipboardHelper.TrySetText(LaunchCommand.ResumeCommandText(session)),
            },
        };

        return menu;
    }

    public Control CreateSettingPanel() => throw new NotImplementedException();

    public void UpdateSettings(PowerLauncherPluginSettings settings) =>
        _settings = PluginSettings.FromOptions(settings?.AdditionalOptions);

    public string GetTranslatedPluginTitle() => Resources.plugin_name;

    public string GetTranslatedPluginDescription() => Resources.plugin_description;

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing && _context?.API != null)
        {
            _context.API.ThemeChanged -= OnThemeChanged;
        }

        _disposed = true;
    }

    private static string SubTitle(ClaudeSession session, DateTime nowUtc)
    {
        var folder = session.Cwd.Length > 0
            ? session.FolderName + Separator + session.Cwd
            : Resources.subtitle_no_folder;
        var subtitle = folder + Separator + RelativeTime.Format(session.LastActive, nowUtc);
        return session.FolderExists ? subtitle : subtitle + Separator + Resources.subtitle_folder_missing;
    }

    private bool Open(ClaudeSession session, OpenTarget target) =>
        target == OpenTarget.Desktop ? OpenInDesktop(session) : OpenInTerminal(session, fork: false);

    private bool OpenInTerminal(ClaudeSession session, bool fork)
    {
        if (!session.FolderExists)
        {
            return FolderMissing(session);
        }

        var settings = _settings;
        var terminalSettings = TerminalSettings.ReadSettings();
        var elevate = terminalSettings != null && TerminalSettings.ProfileRequiresElevation(terminalSettings, settings.TerminalProfile);
        var shell = settings.OnExit == ExitBehavior.CloseTab
            ? TerminalShell.None
            : terminalSettings != null ? TerminalSettings.ProfileShell(terminalSettings, settings.TerminalProfile) : TerminalShell.Cmd;

        return Start(LaunchCommand.Terminal(session, settings, fork, ClaudeCli.Resolve(), elevate, TerminalSettings.ResolveWtPath(), shell));
    }

    private bool OpenInDesktop(ClaudeSession session)
    {
        // The desktop import only takes its title from a custom-title record; give the transcript one first.
        var transcript = _api?.GetTranscriptPath(session.Id);
        if (transcript != null && !TranscriptTitle.IsImportedIntoDesktop(session.Id, TranscriptTitle.DesktopSessionsRoot()))
        {
            try
            {
                TranscriptTitle.EnsureCustomTitle(transcript, session.Id, session.Title);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Exception($"Could not add a title to {transcript}", e, GetType());
            }
        }

        return Start(LaunchCommand.DesktopApp(session));
    }

    private bool FolderMissing(ClaudeSession session)
    {
        ClipboardHelper.TrySetText(LaunchCommand.ResumeCommandText(session));
        _context?.API.ShowMsg(
            Resources.msg_folder_missing_title,
            string.Format(CultureInfo.CurrentCulture, Resources.msg_folder_missing, session.Cwd.Length > 0 ? session.Cwd : session.Id),
            _iconPath,
            true);
        return false;
    }

    private bool Start(ProcessStartInfo startInfo)
    {
        try
        {
            using var _ = Process.Start(startInfo);
            return true;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Log.Exception($"Failed to start {startInfo.FileName} {startInfo.Arguments}", e, GetType());
            _context?.API.ShowMsg(Resources.msg_launch_failed, e.Message, _iconPath, true);
            return false;
        }
    }

    private void UpdateIconPath(Theme theme) =>
        _iconPath = theme is Theme.Light or Theme.HighContrastWhite
            ? @"Images\sessions.light.png"
            : @"Images\sessions.dark.png";

    private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIconPath(newTheme);
}
