using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace CopilotApp.Services;

/// <summary>
/// Provides methods for finding and focusing existing application windows.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class WindowFocusService
{
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    /// <summary>
    /// Attempts to bring the window of the specified process to the foreground.
    /// </summary>
    /// <param name="pid">The process ID whose window should be focused.</param>
    /// <returns><c>true</c> if the window was found and focused; otherwise <c>false</c>.</returns>
    internal static bool TryFocusProcess(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            IntPtr hwnd = proc.MainWindowHandle;
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }

            SetForegroundWindow(hwnd);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
