# Claude Sessions for PowerToys Run

A [PowerToys Run](https://learn.microsoft.com/windows/powertoys/run) plugin that finds your
[Claude Code](https://claude.com/claude-code) sessions by name and resumes them in Windows Terminal,
in the folder each session was started from.

```
Alt+Space  cc login @acme  Enter
→ Windows Terminal opens in C:\code\acme-api running: claude --resume <session-id>
```

## Usage

| Query | Shows |
|---|---|
| `cc` | Most recent sessions |
| `cc fix login` | Sessions whose title fuzzy-matches "fix login" |
| `cc login @acme` | ...only from folders whose name or path contains "acme" |
| `cc @acme @api` | Every `@` filter must match |

Each entry shows the folder name, the full path and how long ago the session was active, e.g.
`acme-api · C:\code\acme-api · 2h ago`. If the folder has been deleted, the entry says
`folder missing` and Enter copies the resume command instead of launching.

The title is the session's `/rename` name if it has one, else its agent name, else Claude's
auto-generated title, else the first prompt.

### Actions

| Action | Shortcut |
|---|---|
| Open (Terminal or Desktop, see settings) | Enter |
| Open in the *other* target | Ctrl+Enter |
| Fork session in Terminal (`--fork-session`) | Ctrl+Shift+Enter |
| Open folder in Explorer | Ctrl+Shift+F |
| Copy session ID | Ctrl+C |
| Copy resume command (`cd "<folder>"; claude --resume <id>`) | Ctrl+Shift+C |

**Admin profiles:** if the Windows Terminal profile used is set to *Run this profile as Administrator*
(`"elevate": true`), the plugin starts Windows Terminal elevated itself; a non-elevated launch of an
elevated profile makes Terminal mangle the command line. Expect a UAC prompt if your UAC settings ask for one.

**Desktop target:** the Claude desktop app has no link that opens a specific CLI session, so the
plugin opens the app, copies the session ID to the clipboard and tells you to run `/resume` and
pick the session.

## Settings

PowerToys Settings → PowerToys Run → Plugins → Claude Sessions.

| Setting | Default | |
|---|---|---|
| Open sessions in | Windows Terminal | What Enter does |
| Windows Terminal window | New window | Or a new tab in the most recent window |
| Windows Terminal profile | *(default profile)* | Profile name for the new tab |
| Extra claude arguments | *(none)* | e.g. `--dangerously-skip-permissions` |
| Hide sessions with no prompts | On | Also hides SDK/automation sessions |
| Hide sessions older than (days) | 90 | 0 = show everything |
| Maximum results | 30 | |

## Install

Requires PowerToys 0.99+ and the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
(`winget install Microsoft.DotNet.SDK.9`).

```powershell
git clone https://github.com/NandavardhanRadhakrishnan/ClaudeSessions
cd ClaudeSessions
.\scripts\deploy.ps1            # or: .\scripts\deploy.ps1 -Platform ARM64
```

The script builds the plugin, stops PowerToys, copies it to
`%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\ClaudeSessions` and starts PowerToys again.

The build references the PowerToys assemblies from the PowerToys install folder
(`C:\Program Files\PowerToys`, or `%LOCALAPPDATA%\PowerToys` for a per-user install). Pass
`-p:PowerToysDir=<folder>` to `dotnet build` to use another location.

## How it works

Claude Code writes one JSON-lines transcript per session to
`%USERPROFILE%\.claude\projects\<encoded-folder>\<session-id>.jsonl` (or under `%CLAUDE_CONFIG_DIR%`).
The plugin reads the folder, title and prompt count from each transcript and keeps the results in
`%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Settings\Plugins\Community.PowerToys.Run.Plugin.ClaudeSessions\session-cache.json`,
so only transcripts that changed since the last query are read again.

## Development

```powershell
dotnet test
```

Design notes: [`docs/superpowers/specs/2026-10-12-claude-sessions-design.md`](docs/superpowers/specs/2026-10-12-claude-sessions-design.md).

## License

[MIT](LICENSE). Not affiliated with Anthropic or Microsoft.
