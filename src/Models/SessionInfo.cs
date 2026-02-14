namespace CopilotBooster.Models;

/// <summary>
/// Represents information about a running Copilot session.
/// </summary>
internal class SessionInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the session.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Gets or sets the current working directory of the session.
    /// </summary>
    public string Cwd { get; set; } = "";

    /// <summary>
    /// Gets or sets a brief summary or description of the session.
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// Gets or sets the process ID of the launcher.
    /// </summary>
    public int Pid { get; set; }

    /// <summary>
    /// Gets or sets the process ID of the cmd.exe hosting the Copilot CLI process.
    /// </summary>
    public int CopilotPid { get; set; }
}
