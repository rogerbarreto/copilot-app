# PowerShell Profile Template for Copilot Permissive
# This function wraps the `copilot` CLI with pre-configured allowed tools and directories.
# Customize the $allowedTools and $allowDirs arrays to your needs.

function copilot-permissive {
    $allowedTools = @(
        "Block",
        "Cmd",
        "Edit",
        "GlobTool",
        "GrepTool",
        "ReadNotebook",
        "Replace",
        "View",
        "Write",
        "BatchTool",
        "exit",
        "mcp__github-mcp-server"
    )
    $allowDirs = @(
        (Get-Location).Path
    )
    $toolArg = ($allowedTools | ForEach-Object { "--allow-tool=$_" }) -join " "
    $dirArg = ($allowDirs | ForEach-Object { "--allow-dir=`"$_`"" }) -join " "
    $fullCmd = "copilot $toolArg $dirArg $args"
    Invoke-Expression $fullCmd
}
