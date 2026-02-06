# Copilot Launcher

A Windows taskbar launcher for [GitHub Copilot CLI](https://github.com/github/copilot-cli) that provides:

- **Taskbar pinning** with the Copilot icon
- **Jump list** with active sessions, new session, and open existing session
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
```

## Manual Setup

### Build

```powershell
cd src
dotnet publish -c Release -o ..\publish
```

### Profile Setup

The `install.ps1` script installs the `copilot-permissive` function into your profile automatically.

To customize, edit `profile\copilot-permissive.ps1` before installing, or modify the function block in your profile (between the `# [copilot-permissive] BEGIN/END` markers).

### Configuration

Set the `COPILOT_WORK_DIR` environment variable to control the default working directory:

```powershell
[Environment]::SetEnvironmentVariable("COPILOT_WORK_DIR", "D:\repo\work", "User")
```

You can also pass a working directory as a command-line argument:

```powershell
CopilotPermissive.exe "C:\my\project"
```
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
