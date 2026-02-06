using System;
using System.IO;

namespace CopilotApp.Services;

/// <summary>
/// Provides simple file-based logging with timestamped messages.
/// </summary>
internal class LogService
{
    private readonly string _logFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogService"/> class.
    /// </summary>
    /// <param name="logFile">Path to the log file.</param>
    internal LogService(string logFile) { this._logFile = logFile; }

    /// <summary>
    /// Appends a timestamped message to the configured log file.
    /// </summary>
    /// <param name="message">The message to log.</param>
    internal void Log(string message) => Log(message, this._logFile);

    /// <summary>
    /// Appends a timestamped message to the specified log file, creating the directory if needed.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="logFile">Path to the log file.</param>
    internal static void Log(string message, string logFile)
    {
        try
        {
            var dir = Path.GetDirectoryName(logFile)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(logFile, $"[{DateTime.Now:o}] {message}\n");
        }
        catch { }
    }
}
