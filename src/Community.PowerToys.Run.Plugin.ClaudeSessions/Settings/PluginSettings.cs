using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Community.PowerToys.Run.Plugin.ClaudeSessions.Properties;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Settings;

public enum OpenTarget
{
    Terminal = 0,
    Desktop = 1,
}

public enum WindowMode
{
    NewWindow = 0,
    NewTab = 1,
}

public enum ExitBehavior
{
    /// <summary>Claude runs inside the profile's shell, so <c>/exit</c> leaves a prompt in the session folder.</summary>
    StayInShell = 0,

    /// <summary>Claude is the tab's command line, so the tab closes with it.</summary>
    CloseTab = 1,
}

/// <summary>Typed view over the plugin's <see cref="PluginAdditionalOption"/> values.</summary>
public sealed record PluginSettings
{
    public const string OpenTargetKey = "OpenTarget";
    public const string WindowModeKey = "WindowMode";
    public const string OnExitKey = "OnExit";
    public const string TerminalProfileKey = "TerminalProfile";
    public const string ExtraArgsKey = "ExtraArgs";
    public const string HideEmptyKey = "HideEmpty";
    public const string MaxAgeDaysKey = "MaxAgeDays";
    public const string MaxResultsKey = "MaxResults";

    private const int DefaultMaxAgeDays = 90;
    private const int DefaultMaxResults = 30;

    public OpenTarget OpenTarget { get; init; } = OpenTarget.Terminal;

    public WindowMode WindowMode { get; init; } = WindowMode.NewWindow;

    public ExitBehavior OnExit { get; init; } = ExitBehavior.StayInShell;

    /// <summary>Gets the Windows Terminal profile name; empty means the default profile.</summary>
    public string TerminalProfile { get; init; } = string.Empty;

    /// <summary>Gets the extra arguments appended to the <c>claude</c> command line.</summary>
    public string ExtraArgs { get; init; } = string.Empty;

    public bool HideEmpty { get; init; } = true;

    /// <summary>Gets the age limit in days; 0 means no limit.</summary>
    public int MaxAgeDays { get; init; } = DefaultMaxAgeDays;

    public int MaxResults { get; init; } = DefaultMaxResults;

    public static IEnumerable<PluginAdditionalOption> DefaultOptions() =>
    [
        new()
        {
            Key = OpenTargetKey,
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
            DisplayLabel = Resources.setting_open_target,
            DisplayDescription = Resources.setting_open_target_description,
            ComboBoxItems =
            [
                new(Resources.setting_open_target_terminal, ((int)OpenTarget.Terminal).ToString(CultureInfo.InvariantCulture)),
                new(Resources.setting_open_target_desktop, ((int)OpenTarget.Desktop).ToString(CultureInfo.InvariantCulture)),
            ],
            ComboBoxValue = (int)OpenTarget.Terminal,
        },
        new()
        {
            Key = WindowModeKey,
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
            DisplayLabel = Resources.setting_window_mode,
            DisplayDescription = Resources.setting_window_mode_description,
            ComboBoxItems =
            [
                new(Resources.setting_window_mode_new_window, ((int)WindowMode.NewWindow).ToString(CultureInfo.InvariantCulture)),
                new(Resources.setting_window_mode_new_tab, ((int)WindowMode.NewTab).ToString(CultureInfo.InvariantCulture)),
            ],
            ComboBoxValue = (int)WindowMode.NewWindow,
        },
        new()
        {
            Key = OnExitKey,
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
            DisplayLabel = Resources.setting_on_exit,
            DisplayDescription = Resources.setting_on_exit_description,
            ComboBoxItems =
            [
                new(Resources.setting_on_exit_stay, ((int)ExitBehavior.StayInShell).ToString(CultureInfo.InvariantCulture)),
                new(Resources.setting_on_exit_close, ((int)ExitBehavior.CloseTab).ToString(CultureInfo.InvariantCulture)),
            ],
            ComboBoxValue = (int)ExitBehavior.StayInShell,
        },
        new()
        {
            Key = TerminalProfileKey,
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
            DisplayLabel = Resources.setting_terminal_profile,
            DisplayDescription = Resources.setting_terminal_profile_description,
            PlaceholderText = Resources.setting_terminal_profile_placeholder,
            TextValue = string.Empty,
        },
        new()
        {
            Key = ExtraArgsKey,
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
            DisplayLabel = Resources.setting_extra_args,
            DisplayDescription = Resources.setting_extra_args_description,
            PlaceholderText = "--dangerously-skip-permissions",
            TextValue = string.Empty,
        },
        new()
        {
            Key = HideEmptyKey,
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
            DisplayLabel = Resources.setting_hide_empty,
            Value = true,
        },
        new()
        {
            Key = MaxAgeDaysKey,
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
            DisplayLabel = Resources.setting_max_age_days,
            DisplayDescription = Resources.setting_max_age_days_description,
            NumberValue = DefaultMaxAgeDays,
            NumberBoxMin = 0,
            NumberBoxMax = 3650,
            NumberBoxSmallChange = 1,
            NumberBoxLargeChange = 30,
        },
        new()
        {
            Key = MaxResultsKey,
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
            DisplayLabel = Resources.setting_max_results,
            NumberValue = DefaultMaxResults,
            NumberBoxMin = 1,
            NumberBoxMax = 200,
            NumberBoxSmallChange = 1,
            NumberBoxLargeChange = 10,
        },
    ];

    /// <summary>Reads saved option values; missing or invalid values fall back to the defaults.</summary>
    public static PluginSettings FromOptions(IEnumerable<PluginAdditionalOption>? options)
    {
        var byKey = (options ?? [])
            .Where(o => o?.Key != null)
            .GroupBy(o => o.Key)
            .ToDictionary(g => g.Key, g => g.Last());
        var defaults = new PluginSettings();

        return new PluginSettings
        {
            OpenTarget = byKey.TryGetValue(OpenTargetKey, out var target) && Enum.IsDefined((OpenTarget)target.ComboBoxValue)
                ? (OpenTarget)target.ComboBoxValue
                : defaults.OpenTarget,
            WindowMode = byKey.TryGetValue(WindowModeKey, out var mode) && Enum.IsDefined((WindowMode)mode.ComboBoxValue)
                ? (WindowMode)mode.ComboBoxValue
                : defaults.WindowMode,
            OnExit = byKey.TryGetValue(OnExitKey, out var onExit) && Enum.IsDefined((ExitBehavior)onExit.ComboBoxValue)
                ? (ExitBehavior)onExit.ComboBoxValue
                : defaults.OnExit,
            TerminalProfile = byKey.TryGetValue(TerminalProfileKey, out var profile) ? profile.TextValue?.Trim() ?? string.Empty : defaults.TerminalProfile,
            ExtraArgs = byKey.TryGetValue(ExtraArgsKey, out var extra) ? extra.TextValue?.Trim() ?? string.Empty : defaults.ExtraArgs,
            HideEmpty = byKey.TryGetValue(HideEmptyKey, out var hide) ? hide.Value : defaults.HideEmpty,
            MaxAgeDays = byKey.TryGetValue(MaxAgeDaysKey, out var age) ? ToInt(age.NumberValue, 0, defaults.MaxAgeDays) : defaults.MaxAgeDays,
            MaxResults = byKey.TryGetValue(MaxResultsKey, out var max) ? ToInt(max.NumberValue, 1, defaults.MaxResults) : defaults.MaxResults,
        };
    }

    private static int ToInt(double value, int min, int fallback) =>
        double.IsNaN(value) ? fallback : (int)Math.Clamp(Math.Round(value), min, int.MaxValue);
}
