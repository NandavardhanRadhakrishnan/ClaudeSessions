# Claude Sessions — PowerToys Run plugin design

**Date:** 2026-10-12
**Status:** Draft for review
**Repo:** https://github.com/NandavardhanRadhakrishnan/ClaudeSessions

## 1. Goal

A PowerToys Run plugin that lets you type a few words of a Claude Code session's name, pick it from the
Run dropdown, and land back in that session — resumed with `claude --resume <id>` in Windows Terminal,
in the folder the session was started from.

Success looks like: `Alt+Space`, `cc login`, Enter → a Windows Terminal window opens in
`C:\code\acme-api` running the "fix login bug" session, in well under a second of perceived latency.

### What the user asked for

- PowerToys **Run** plugin (not Command Palette — CmdPal feels slow).
- Setting to choose where sessions open: terminal / desktop (/ web — dropped, see §8).
- Query matches the **session name**; an optional **folder qualifier** disambiguates similar names.
- Each dropdown entry shows **which folder** the session belongs to.
- v1 extras: context-menu actions, Windows Terminal profile + extra args settings, hide old/empty
  sessions, fork instead of resume.

### Prior art (checked 2026-10-12)

Nothing does this today. Closest: *New Chat for Claude* (claude.ai chats, no resume), *QuickShell*
(project folders in a terminal, not Claude-aware), *PowerToy_UI* (sessions via `claude://` links into the
desktop app, not a launcher), *claude-session-restore* (macOS/Linux terminals only).

## 2. Data source

Claude Code writes one JSON-lines transcript per session:

```
%USERPROFILE%\.claude\projects\<encoded-cwd>\<session-uuid>.jsonl
```

- The Claude home is `%CLAUDE_CONFIG_DIR%` if set, else `%USERPROFILE%\.claude`.
- Only files **directly inside** a project directory are sessions. Files deeper down
  (`<uuid>\subagents\*.jsonl`) and other folders (`memory\`) are ignored.
- The session ID is the file name without extension; files whose stem is not a GUID are ignored.
- `<encoded-cwd>` is lossy (`C:\code\acme-api` → `C--code-acme-api`), so the folder is
  **never** decoded from the directory name — it is read from the `cwd` field inside the file.
- Files are read with `FileShare.ReadWrite` because Claude may be appending to them.

### Fields extracted per session

| Field | Source |
|---|---|
| `Id` | File name stem |
| `Cwd` | First record containing a `cwd` string, normalised (below) |
| `Title` | Last-written of each kind, by precedence: `custom-title.customTitle` (`/rename`) → `agent-name.agentName` → `ai-title.aiTitle` → first user prompt (trimmed, single line, ≤ 80 chars) → `"(untitled) <first 8 chars of id>"` |
| `LastActive` | File `LastWriteTimeUtc` |
| `UserPromptCount` | Records with `"type":"user"` where `message.content` is a **string**, `isMeta` is not `true`, and the text does not start with `<` (filters command/meta wrappers). Tool results (array content) don't count. |

### `cwd` normalisation

| Raw | Normalised |
|---|---|
| `C:\code\x` | `C:\code\x` |
| `C:/code/x` | `C:\code\x` |
| `/c/code/x` (Git Bash / MSYS) | `C:\code\x` |
| anything else (e.g. WSL `/home/u/x`) | kept as-is; treated as a missing folder |

Trailing separators are trimmed. "Folder name" means the last path segment (`acme-api`).

### Parsing strategy

A regular user easily accumulates hundreds of transcripts totalling hundreds of MB, with single files
of several MB, so parsing must be cheap:

- Stream line by line; JSON-parse (System.Text.Json) **only** lines that contain one of:
  `"cwd":"` (until a cwd is found), `"type":"custom-title"`, `"type":"agent-name"`, `"type":"ai-title"`,
  `"type":"user"`.
- A line that fails to parse is skipped. An unreadable file is logged and skipped.

## 3. Architecture

Modelled on the in-box *VS Code Workspaces* Run plugin (`Main` + `*Api` + model + context menu with
accelerators). That plugin re-reads its data on every query, which is fine for VS Code's single small
storage file; our data is two orders of magnitude bigger, so `SessionsApi` adds an incremental cache
behind the same kind of property.

```
ClaudeSessions.sln
src/Community.PowerToys.Run.Plugin.ClaudeSessions/
  Main.cs                     IPlugin, IContextMenu, ISettingProvider, IPluginI18n
  plugin.json
  Images/sessions.dark.png, sessions.light.png
  Properties/Resources.resx   all user-facing strings (English only in v1)
  Sessions/ClaudeSession.cs   immutable model
  Sessions/SessionScanner.cs  one .jsonl → ClaudeSession
  Sessions/PathNormalizer.cs  cwd normalisation
  Sessions/SessionsApi.cs     enumerate + cache + persist
  Search/QueryParser.cs       raw search → ParsedQuery
  Search/SessionMatcher.cs    filter + score
  Launch/LaunchCommand.cs     settings + session → ProcessStartInfo (pure)
  Settings/PluginSettings.cs  typed view over PluginAdditionalOption values
tests/Community.PowerToys.Run.Plugin.ClaudeSessions.UnitTests/
  Fixtures/*.jsonl
scripts/deploy.ps1
README.md, LICENSE (MIT)
```

### Units

**`ClaudeSession`** — `Id`, `Title`, `Cwd`, `FolderName`, `LastActive`, `UserPromptCount`,
`FolderExists` (evaluated at query time, not cached).

**`SessionScanner.Scan(string path) → ClaudeSession?`** — implements §2. Pure apart from reading the file.

**`SessionsApi`**
- `IReadOnlyList<ClaudeSession> GetSessions()` — enumerates `projects\*\*.jsonl` (name, size, mtime only),
  re-scans files whose `(size, mtime)` differ from the cache, drops entries for deleted files, returns all.
- Cold start parses changed files in parallel (`Parallel.ForEach`).
- Cache file: `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Settings\Plugins\Community.PowerToys.Run.Plugin.ClaudeSessions\session-cache.json`
  — `{ "version": 1, "entries": [ { "path", "size", "mtimeTicks", "session" } ] }`.
  Loaded on `Init`; written only when something changed. A missing, corrupt or wrong-version cache is
  ignored and rebuilt.
- Thread-safe: Run may call `Query` concurrently; refresh is guarded by a lock.

**`QueryParser.Parse(string search) → ParsedQuery`** — splits on whitespace. Tokens starting with `@` and
longer than one character are folder filters (the `@` stripped); everything else, rejoined with single
spaces, is the title search text.

**`SessionMatcher`**
1. Apply settings filters: hide sessions with `UserPromptCount == 0` (if enabled), hide sessions older than
   max age (if > 0).
2. Every folder filter must be a case-insensitive substring of `FolderName` **or** of the full `Cwd`.
3. Title text empty → order by `LastActive` descending.
   Title text present → `StringMatcher.FuzzySearch(text, Title)`; keep matches with score > 0;
   order by score, then `LastActive`.
4. Take the first *max results*. Assign `Result.Score` descending from the final order so Run keeps it.

**`LaunchCommand`** — builds `ProcessStartInfo`s; no side effects, fully unit-tested.
- **Terminal** (`wt.exe`):
  `wt.exe <window> [-p "<profile>"] -d "<cwd>" --title "<title>" claude --resume <id> [--fork-session] [<extra args>]`
  where `<window>` is `-w new` (new window) or `-w 0 nt` (new tab in the most recent window).
  `claude` is run directly as the tab's command line (the profile contributes appearance/starting
  environment, not a shell), so the tab closes per the profile's `closeOnExit` when Claude exits.
  Semicolons in the title or extra args are escaped as `\;` so `wt` doesn't split them into extra commands;
  double quotes in the title are removed.
- **Desktop** — decided by the probe in §7: if a `claude://` URI that opens a specific CLI session is
  found, launch it; otherwise open the Claude desktop app (`claude://`), copy the session ID to the
  clipboard, and show a message: "Claude opened — run /resume and pick '<title>' (session ID copied)".
- **Resume command text** (for copying): `cd "<cwd>"; claude --resume <id>`.

**`Main`**
- `Query`: parse, get sessions, match, map to `Result`:
  - `Title` = session title
  - `SubTitle` = `<FolderName> · <Cwd> · <relative time>` (e.g. `acme-api · C:\code\acme-api · 2h ago`);
    with ` · folder missing` appended when the folder doesn't exist
  - `ToolTipData` = title + full cwd + session ID
  - `Action` = launch with the configured target; if the folder is missing, copy the resume command and
    show a message instead of launching
- `LoadContextMenus` (accelerators in the style of VS Code Workspaces):

  | Action | Shortcut |
  |---|---|
  | Open in the *other* target (Desktop ↔ Terminal) | Ctrl+Enter |
  | Fork session in Terminal (`--fork-session`) | Ctrl+Shift+Enter |
  | Open folder in Explorer | Ctrl+Shift+F |
  | Copy session ID | Ctrl+C |
  | Copy resume command | Ctrl+Shift+C |

- Errors launching (`Win32Exception` etc.) are logged via `Wox.Plugin.Logger.Log` and shown with
  `context.API.ShowMsg`; the Run window stays open.

### `plugin.json`

```json
{
  "ID": "08BED641FE144D12A24B804626640F06",
  "ActionKeyword": "cc",
  "IsGlobal": false,
  "Name": "Claude Sessions",
  "Author": "NandavardhanRadhakrishnan",
  "Version": "0.1.0",
  "Language": "csharp",
  "Website": "https://github.com/NandavardhanRadhakrishnan/ClaudeSessions",
  "ExecuteFileName": "Community.PowerToys.Run.Plugin.ClaudeSessions.dll",
  "IcoPathDark": "Images\\sessions.dark.png",
  "IcoPathLight": "Images\\sessions.light.png"
}
```

Icons are original artwork (a generic terminal/history glyph), not the Claude or Anthropic logo.

## 4. Settings

Exposed through `ISettingProvider.AdditionalOptions` in PowerToys Settings → PowerToys Run → Claude Sessions.

| Key | Type | Default | Meaning |
|---|---|---|---|
| `OpenTarget` | Combobox | Terminal | Terminal / Desktop — what Enter does |
| `WindowMode` | Combobox | New window | New window / New tab in the most recent window |
| `TerminalProfile` | Textbox | *(empty)* | Windows Terminal profile name; empty = default profile |
| `ExtraArgs` | Textbox | *(empty)* | Appended to the `claude` command (e.g. `--dangerously-skip-permissions`) |
| `HideEmpty` | Checkbox | on | Hide sessions with no user prompts |
| `MaxAgeDays` | Numberbox | 90 | Hide sessions older than this; 0 = no limit |
| `MaxResults` | Numberbox | 30 | Results shown per query |

## 5. Data flow

```
Alt+Space "cc login @acme"
  → Main.Query
  → QueryParser        → { text: "login", folders: ["acme"] }
  → SessionsApi        → stat files → re-scan changed → cached sessions
  → SessionMatcher     → settings filters → folder filter → fuzzy title → top N
  → List<Result>       → title / "acme-api · C:\code\acme-api · 2h ago"
Enter
  → LaunchCommand      → wt.exe -w new -d "C:\code\acme-api" --title "fix login bug" claude --resume <id>
```

## 6. Performance targets

- Warm query (no changed files) with several hundred sessions: **< 50 ms**.
- Cold start with no cache file: **< 3 s**, once; later cold starts only re-scan changed files.

## 7. Testing

**Unit tests (MSTest, written test-first):**
- `SessionScanner`: title precedence and last-wins per kind; first-prompt fallback skips tool results,
  `isMeta` and `<…>` wrappers; untitled fallback; corrupt lines skipped; empty file → `UserPromptCount` 0.
- `PathNormalizer`: every row of the table in §2.
- `QueryParser`: plain text, `@folder` only, mixed, lone `@`, multiple `@` filters, extra whitespace.
- `SessionMatcher`: folder filter on name and on full path, hide-empty, max-age, max-results,
  recency ordering for an empty query, fuzzy ordering otherwise.
- `LaunchCommand`: new window vs new tab, with/without profile, fork flag, extra args, paths with spaces,
  `;` and `"` escaping, resume-command text.
- `SessionsApi`: unchanged file not re-scanned; changed size/mtime re-scanned; deleted file dropped;
  corrupt/wrong-version cache ignored. Uses a temp directory as the Claude home.

**Desktop deep-link probe (first implementation task, throwaway):** try `claude://` URIs with a CLI
session ID (including the `claude://claude.ai/epitaxy/local_<id>` shape seen in PowerToy_UI) against the
installed Claude desktop app. The result sets the Desktop branch of `LaunchCommand`; the fallback in §3
ships if nothing works.

**Manual smoke test:** `scripts\deploy.ps1`, then in Run: `cc` (recent list), `cc <title words>`,
`cc <words> @<folder>`, Enter (terminal opens in the right folder and resumes), each context-menu action,
each setting, a session whose folder was deleted.

## 8. Out of scope for v1

- **Web target** — claude.ai/code cannot open a local CLI session; a session is only visible there while
  Remote Control is bridging a running CLI process.
- Command Palette version.
- GitHub Actions release workflow (follow-up once v1 works).
- Translations beyond English (strings live in `.resx`, so they can be added later).

## 9. Build and deploy

- Target `net9.0-windows`, platforms x64 and ARM64 (PowerToys 0.99 runs on .NET 9.0.15).
- Requires the .NET 9 SDK to build (`winget install Microsoft.DotNet.SDK.9`).
- PowerToys references (`Wox.Plugin`, `Wox.Infrastructure`, `PowerToys.Settings.UI.Lib`) come from the
  `Community.PowerToys.Run.Plugin.Dependencies` NuGet package; if it has no build compatible with 0.99,
  reference the DLLs in `C:\Program Files\PowerToys` with `Private=false`.
- `scripts/deploy.ps1`: build Release x64 → stop PowerToys → copy output to
  `%LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\ClaudeSessions\` → start PowerToys.
