<#
.SYNOPSIS
    Installs the Copilot Permissive Launcher.

.DESCRIPTION
    Builds the CopilotPermissive.exe, installs the copilot-permissive function
    into the PowerShell profile, and creates a shortcut for the taskbar.

.PARAMETER PublishDir
    Where to publish the built exe. Default: ~\Documents\CopilotLauncher\publish

.PARAMETER WorkDir
    Default working directory for new sessions. Default: current directory.
#>

param(
    [string]$PublishDir = (Join-Path ([Environment]::GetFolderPath("MyDocuments")) "CopilotLauncher\publish"),
    [string]$WorkDir = (Get-Location).Path
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
$SrcDir = Join-Path $RepoRoot "src"

# 1. Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Cyan
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK is required. Install from https://dot.net/download"
    return
}
if (-not (Get-Command copilot -ErrorAction SilentlyContinue)) {
    Write-Error "GitHub Copilot CLI is required. Install via: winget install GitHub.Copilot"
    return
}

# 2. Build
Write-Host "Building CopilotPermissive..." -ForegroundColor Cyan
Push-Location $SrcDir
dotnet publish -c Release -o $PublishDir --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    Write-Error "Build failed"
    return
}
Pop-Location
Write-Host "Published to: $PublishDir" -ForegroundColor Green

# 3. Install profile function
Write-Host "Installing copilot-permissive function to PowerShell profile..." -ForegroundColor Cyan
$profilePath = $PROFILE.CurrentUserAllHosts
if (-not (Test-Path $profilePath)) {
    New-Item -ItemType File -Path $profilePath -Force | Out-Null
}

$profileContent = Get-Content $profilePath -Raw -ErrorAction SilentlyContinue
$marker = "# [copilot-permissive] BEGIN"
$endMarker = "# [copilot-permissive] END"

$functionBlock = @"
$marker
$(Get-Content (Join-Path $RepoRoot "profile\copilot-permissive.ps1") -Raw)
$endMarker
"@

if ($profileContent -and $profileContent.Contains($marker)) {
    # Replace existing block
    $pattern = [regex]::Escape($marker) + "[\s\S]*?" + [regex]::Escape($endMarker)
    $profileContent = [regex]::Replace($profileContent, $pattern, $functionBlock)
    Set-Content $profilePath -Value $profileContent -NoNewline
    Write-Host "Updated existing copilot-permissive function in profile." -ForegroundColor Green
} else {
    # Append
    Add-Content $profilePath -Value "`n$functionBlock"
    Write-Host "Added copilot-permissive function to profile." -ForegroundColor Green
}

# 4. Set environment variable for default work dir
Write-Host "Setting COPILOT_WORK_DIR=$WorkDir" -ForegroundColor Cyan
[Environment]::SetEnvironmentVariable("COPILOT_WORK_DIR", $WorkDir, "User")

# 5. Summary
Write-Host ""
Write-Host "=== Installation Complete ===" -ForegroundColor Green
Write-Host "Executable:  $PublishDir\CopilotPermissive.exe"
Write-Host "Profile:     $profilePath"
Write-Host "Work Dir:    $WorkDir (set via COPILOT_WORK_DIR)"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Run CopilotPermissive.exe"
Write-Host "  2. Right-click its taskbar icon and select 'Pin to taskbar'"
Write-Host "  3. Right-click the pinned icon to see the jump list"
Write-Host ""
Write-Host "To change the default work directory:" -ForegroundColor Yellow
Write-Host '  [Environment]::SetEnvironmentVariable("COPILOT_WORK_DIR", "C:\your\path", "User")'
