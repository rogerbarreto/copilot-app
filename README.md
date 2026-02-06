# Copilot Launcher

A Windows taskbar launcher for [GitHub Copilot CLI](https://github.com/github/copilot-cli) that provides:

- **Taskbar pinning** with the Copilot icon
- **Jump list** with active sessions, new session, open existing session, and settings
- **Settings UI** to configure allowed tools and directories (no profile editing needed)
- **Session picker** dialog to resume any previous session
- **Background jump list updates** every 5 minutes
- **Multi-instance coordination** via named mutexes to prevent collisions

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [PowerShell 7+](https://github.com/PowerShell/PowerShell)
- [GitHub Copilot CLI](https://docs.github.com/en/copilot/github-copilot-in-the-cli) installed via `winget install GitHub.Copilot` or `GitHub.Copilot.Prerelease`

## Quick Start

```powershell
# 1. Clone and build
git clone <repo-url> copilot-app
cd copilot-app
.\install.ps1

# 2. Pin to taskbar
#    Launch CopilotPermissive.exe, right-click its taskbar icon → "Pin to taskbar"

# 3. Configure
#    Right-click pinned icon → Settings
```

## Manual Setup

### Build

```powershell
cd src
dotnet publish -c Release -o ..\publish
```

### Configuration

All settings are managed via the **Settings UI** (right-click pinned icon → Settings, or run `CopilotPermissive.exe --settings`).

Settings are stored in `~/.copilot/launcher-settings.json` and include:

| Setting | Description |
|---------|-------------|
| **Allowed Tools** | Tools Copilot is permitted to use without prompting |
| **Allowed Directories** | Directories Copilot can access |
| **Default Work Dir** | Working directory for new sessions |

You can also pass a working directory as a command-line argument:

```powershell
CopilotPermissive.exe "C:\my\project"
```

### Default Allowed Tools

On first run, the following tools are whitelisted:

| Tool | Description |
|------|-------------|
| `Block` | Block tool for structured output |
| `Cmd` | Shell command execution |
| `Edit` | File editing |
| `GlobTool` | File pattern matching |
| `GrepTool` | Content search |
| `ReadNotebook` | Jupyter notebook reading |
| `Replace` | String replacement in files |
| `View` | File/directory viewing |
| `Write` | File creation |
| `BatchTool` | Batch operations |
| `exit` | Session exit |
| `mcp__github-mcp-server` | GitHub MCP server tools |
```

## Usage

| Action | How |
|--------|-----|
| New session | Click the pinned icon or select "New Copilot Session" from jump list |
| Resume session | Select session from "Active Sessions" in jump list |
| Open existing | Select "Open Existing Session" from jump list → pick from dialog |
| Custom work dir | `CopilotPermissive.exe "C:\my\project"` |

## Architecture

```
CopilotPermissive.exe (WinForms, hidden window)
├── Sets AppUserModelID for taskbar grouping
├── Registers PID in ~/.copilot/active-pids.json
├── Launches: pwsh -NoExit -Command "copilot-permissive [--resume id]"
├── Detects new session folder via directory snapshot
├── Updates jump list immediately + every 5min (background)
└── Cleans up on exit (unregisters PID, updates jump list)
```

### Files

| Path | Purpose |
|------|---------|
| `~/.copilot/active-pids.json` | PID → session ID registry |
| `~/.copilot/jumplist-lastupdate.txt` | Timestamp for update coordination |
| `~/.copilot/launcher.log` | Debug log |
| `~/.copilot/session-state/<id>/workspace.yaml` | Session metadata (managed by Copilot CLI) |

## License

MIT
