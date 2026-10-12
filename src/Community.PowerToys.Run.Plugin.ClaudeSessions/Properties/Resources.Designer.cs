// Hand-maintained accessor for Resources.resx (the SDK build has no ResXFileCodeGenerator).
// Add a property here when adding a string to the .resx.
#nullable enable
using System.Globalization;
using System.Resources;

namespace Community.PowerToys.Run.Plugin.ClaudeSessions.Properties;

internal static class Resources
{
    private static ResourceManager? _resourceManager;

    internal static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager("Community.PowerToys.Run.Plugin.ClaudeSessions.Properties.Resources", typeof(Resources).Assembly);

    internal static CultureInfo? Culture { get; set; }

    internal static string plugin_name => ResourceManager.GetString("plugin_name", Culture)!;

    internal static string plugin_description => ResourceManager.GetString("plugin_description", Culture)!;

    internal static string time_just_now => ResourceManager.GetString("time_just_now", Culture)!;

    internal static string time_minutes_ago => ResourceManager.GetString("time_minutes_ago", Culture)!;

    internal static string time_hours_ago => ResourceManager.GetString("time_hours_ago", Culture)!;

    internal static string time_days_ago => ResourceManager.GetString("time_days_ago", Culture)!;

    internal static string subtitle_folder_missing => ResourceManager.GetString("subtitle_folder_missing", Culture)!;

    internal static string subtitle_no_folder => ResourceManager.GetString("subtitle_no_folder", Culture)!;

    internal static string tooltip_text => ResourceManager.GetString("tooltip_text", Culture)!;

    internal static string context_open_in_desktop => ResourceManager.GetString("context_open_in_desktop", Culture)!;

    internal static string context_open_in_terminal => ResourceManager.GetString("context_open_in_terminal", Culture)!;

    internal static string context_fork => ResourceManager.GetString("context_fork", Culture)!;

    internal static string context_open_folder => ResourceManager.GetString("context_open_folder", Culture)!;

    internal static string context_copy_id => ResourceManager.GetString("context_copy_id", Culture)!;

    internal static string context_copy_resume => ResourceManager.GetString("context_copy_resume", Culture)!;

    internal static string msg_folder_missing_title => ResourceManager.GetString("msg_folder_missing_title", Culture)!;

    internal static string msg_folder_missing => ResourceManager.GetString("msg_folder_missing", Culture)!;



    internal static string msg_launch_failed => ResourceManager.GetString("msg_launch_failed", Culture)!;

    internal static string no_sessions_title => ResourceManager.GetString("no_sessions_title", Culture)!;

    internal static string no_sessions_subtitle => ResourceManager.GetString("no_sessions_subtitle", Culture)!;

    internal static string setting_open_target => ResourceManager.GetString("setting_open_target", Culture)!;

    internal static string setting_open_target_description => ResourceManager.GetString("setting_open_target_description", Culture)!;

    internal static string setting_open_target_terminal => ResourceManager.GetString("setting_open_target_terminal", Culture)!;

    internal static string setting_open_target_desktop => ResourceManager.GetString("setting_open_target_desktop", Culture)!;

    internal static string setting_window_mode => ResourceManager.GetString("setting_window_mode", Culture)!;

    internal static string setting_on_exit => ResourceManager.GetString("setting_on_exit", Culture)!;

    internal static string setting_on_exit_description => ResourceManager.GetString("setting_on_exit_description", Culture)!;

    internal static string setting_on_exit_stay => ResourceManager.GetString("setting_on_exit_stay", Culture)!;

    internal static string setting_on_exit_close => ResourceManager.GetString("setting_on_exit_close", Culture)!;

    internal static string setting_window_mode_description => ResourceManager.GetString("setting_window_mode_description", Culture)!;

    internal static string setting_window_mode_new_window => ResourceManager.GetString("setting_window_mode_new_window", Culture)!;

    internal static string setting_window_mode_new_tab => ResourceManager.GetString("setting_window_mode_new_tab", Culture)!;

    internal static string setting_terminal_profile => ResourceManager.GetString("setting_terminal_profile", Culture)!;

    internal static string setting_terminal_profile_description => ResourceManager.GetString("setting_terminal_profile_description", Culture)!;

    internal static string setting_terminal_profile_placeholder => ResourceManager.GetString("setting_terminal_profile_placeholder", Culture)!;

    internal static string setting_extra_args => ResourceManager.GetString("setting_extra_args", Culture)!;

    internal static string setting_extra_args_description => ResourceManager.GetString("setting_extra_args_description", Culture)!;

    internal static string setting_hide_empty => ResourceManager.GetString("setting_hide_empty", Culture)!;

    internal static string setting_max_age_days => ResourceManager.GetString("setting_max_age_days", Culture)!;

    internal static string setting_max_age_days_description => ResourceManager.GetString("setting_max_age_days_description", Culture)!;

    internal static string setting_max_results => ResourceManager.GetString("setting_max_results", Culture)!;
}
