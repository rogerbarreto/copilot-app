using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Taskbar;
using Microsoft.WindowsAPICodePack.Shell;

class Program
{
    const string AppId = "GitHub.CopilotCLI.Permissive";
    const string UpdaterMutexName = "Global\\CopilotJumpListUpdater";
    const string UpdateLockName = "Global\\CopilotJumpListUpdateLock";

    static readonly string CopilotDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilot");
    static readonly string SessionStateDir = Path.Combine(CopilotDir, "session-state");
    static readonly string PidRegistryFile = Path.Combine(CopilotDir, "active-pids.json");
    static readonly string LastUpdateFile = Path.Combine(CopilotDir, "jumplist-lastupdate.txt");
    static readonly string LogFile = Path.Combine(CopilotDir, "launcher.log");
    static readonly string LauncherExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
    static readonly string CopilotExePath = FindCopilotExe();
    static readonly string DefaultWorkDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    static Form? _hiddenForm;
    static Process? _pwshProcess;

    [DllImport("shell32.dll", SetLastError = true)]
    static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    static string FindCopilotExe()
    {
        // Search common install locations
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\WinGet\Packages\GitHub.Copilot.Prerelease_Microsoft.Winget.Source_8wekyb3d8bbwe\copilot.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\WinGet\Packages\GitHub.Copilot_Microsoft.Winget.Source_8wekyb3d8bbwe\copilot.exe"),
        };

        foreach (var path in candidates)
            if (File.Exists(path)) return path;

        // Fallback: try to find copilot in PATH
        try
        {
            var psi = new ProcessStartInfo("where", "copilot")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd().Trim();
            proc?.WaitForExit();
            if (!string.IsNullOrEmpty(output) && File.Exists(output.Split('\n')[0].Trim()))
                return output.Split('\n')[0].Trim();
        }
        catch { }

        return "copilot.exe";
    }

    static void Log(string message)
    {
        try
        {
            if (!Directory.Exists(CopilotDir))
                Directory.CreateDirectory(CopilotDir);
            File.AppendAllText(LogFile, $"[{DateTime.Now:o}] {message}\n");
        }
        catch { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        Log("Launcher started");

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        SetCurrentProcessExplicitAppUserModelID(AppId);

        // Parse arguments
        string? resumeSessionId = null;
        bool openExisting = false;
        string? workDir = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--resume" && i + 1 < args.Length)
            {
                resumeSessionId = args[i + 1];
                i++;
            }
            else if (args[i] == "--open-existing")
            {
                openExisting = true;
            }
            else
            {
                workDir = args[i];
            }
        }

        workDir ??= Environment.GetEnvironmentVariable("COPILOT_WORK_DIR") ?? DefaultWorkDir;

        // If open-existing mode, show session picker
        if (openExisting)
        {
            resumeSessionId = ShowSessionPicker();
            if (resumeSessionId == null) return;
        }

        Log($"WorkDir: {workDir}, Resume: {resumeSessionId ?? "none"}");

        // Create form - visible in taskbar for jump list
        _hiddenForm = new Form
        {
            Text = "Copilot Permissive",
            ShowInTaskbar = true,
            WindowState = FormWindowState.Minimized,
            FormBorderStyle = FormBorderStyle.FixedSingle,
            MinimizeBox = true,
            Size = new System.Drawing.Size(1, 1)
        };

        // Set window icon
        try
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(CopilotExePath);
            if (icon != null) _hiddenForm.Icon = icon;
        }
        catch { }

        _hiddenForm.Load += (s, e) =>
        {
            _hiddenForm.WindowState = FormWindowState.Minimized;
            _hiddenForm.ShowInTaskbar = true;
            StartCopilotSession(workDir, resumeSessionId);
        };

        Application.Run(_hiddenForm);
    }

    static void StartCopilotSession(string workDir, string? resumeSessionId)
    {
        var myPid = Environment.ProcessId;
        RegisterPid(myPid);
        Log($"Registered PID: {myPid}");

        // Try to become the jump list updater (single instance)
        bool isUpdater = false;
        Mutex? updaterMutex = null;
        try
        {
            updaterMutex = new Mutex(true, UpdaterMutexName, out isUpdater);
            Log($"Is updater: {isUpdater}");
        }
        catch (Exception ex) { Log($"Mutex error: {ex.Message}"); }

        var cts = new CancellationTokenSource();
        if (isUpdater)
        {
            var updaterThread = new Thread(() => UpdaterLoop(cts.Token)) { IsBackground = true };
            updaterThread.Start();
        }

        Log("Starting pwsh...");

        // Snapshot existing sessions before launch
        var existingSessions = new HashSet<string>(
            Directory.Exists(SessionStateDir)
                ? Directory.GetDirectories(SessionStateDir).Select(d => Path.GetFileName(d) ?? "")
                : Array.Empty<string>());

        // Launch pwsh + copilot-permissive
        var copilotCmd = resumeSessionId != null
            ? $"copilot-permissive --resume {resumeSessionId}"
            : "copilot-permissive";

        var psi = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            Arguments = $"-NoExit -Command \"{copilotCmd}\"",
            WorkingDirectory = workDir,
            UseShellExecute = true
        };

        _pwshProcess = Process.Start(psi);
        Log($"Started pwsh with PID: {_pwshProcess?.Id}");

        // Update jump list after session creation delay
        var timer = new System.Windows.Forms.Timer { Interval = 3000 };
        timer.Tick += (s, e) =>
        {
            timer.Stop();

            // Map this PID to its session
            string? sessionId = resumeSessionId;
            if (sessionId == null && Directory.Exists(SessionStateDir))
            {
                var newSession = Directory.GetDirectories(SessionStateDir)
                    .Select(d => Path.GetFileName(d) ?? "")
                    .FirstOrDefault(d => !string.IsNullOrEmpty(d) && !existingSessions.Contains(d));
                sessionId = newSession;
            }

            if (sessionId != null)
            {
                UpdatePidSessionId(myPid, sessionId);
                Log($"Mapped PID {myPid} to session {sessionId}");
            }

            Log("Updating jump list...");
            TryUpdateJumpListWithLock();
            Log("Jump list updated");

            // Watch for pwsh exit
            var exitWatcher = new Thread(() =>
            {
                _pwshProcess?.WaitForExit();
                Log("pwsh exited");

                UnregisterPid(myPid);
                TryUpdateJumpListWithLock();

                cts.Cancel();
                updaterMutex?.ReleaseMutex();
                updaterMutex?.Dispose();

                _hiddenForm?.Invoke(() => Application.Exit());
            }) { IsBackground = true };
            exitWatcher.Start();
        };
        timer.Start();
    }

    #region Jump List

    static void TryUpdateJumpListWithLock()
    {
        try
        {
            using var updateLock = new Mutex(false, UpdateLockName);
            if (updateLock.WaitOne(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    File.WriteAllText(LastUpdateFile, DateTime.UtcNow.ToString("o"));
                    UpdateJumpList();
                }
                finally
                {
                    updateLock.ReleaseMutex();
                }
            }
        }
        catch (Exception ex) { Log($"TryUpdateJumpListWithLock error: {ex.Message}"); }
    }

    static bool ShouldBackgroundUpdate(TimeSpan minInterval)
    {
        try
        {
            if (!File.Exists(LastUpdateFile)) return true;
            var lastUpdate = DateTime.Parse(File.ReadAllText(LastUpdateFile).Trim());
            return DateTime.UtcNow - lastUpdate > minInterval;
        }
        catch { return true; }
    }

    static void UpdaterLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (ShouldBackgroundUpdate(TimeSpan.FromMinutes(1)))
            {
                _hiddenForm?.Invoke(() => TryUpdateJumpListWithLock());
            }

            for (int i = 0; i < 300 && !ct.IsCancellationRequested; i++)
                Thread.Sleep(1000);
        }
    }

    static void UpdateJumpList()
    {
        try
        {
            var activeSessions = GetActiveSessions();

            if (_hiddenForm == null || !_hiddenForm.IsHandleCreated)
            {
                Log("Hidden form not ready");
                return;
            }

            var jumpList = JumpList.CreateJumpListForIndividualWindow(AppId, _hiddenForm.Handle);
            jumpList.KnownCategoryToDisplay = JumpListKnownCategoryType.Neither;
            jumpList.ClearAllUserTasks();

            var newSessionTask = new JumpListLink(LauncherExePath, "New Copilot Session")
            {
                IconReference = new IconReference(CopilotExePath, 0)
            };

            var openExistingTask = new JumpListLink(LauncherExePath, "Open Existing Session")
            {
                Arguments = "--open-existing",
                IconReference = new IconReference(CopilotExePath, 0)
            };

            jumpList.AddUserTasks(newSessionTask, new JumpListSeparator(), openExistingTask);

            var category = new JumpListCustomCategory("Active Sessions");
            foreach (var session in activeSessions)
            {
                var link = new JumpListLink(LauncherExePath, session.Summary)
                {
                    Arguments = $"--resume {session.Id}",
                    IconReference = new IconReference(CopilotExePath, 0),
                    WorkingDirectory = session.Cwd
                };
                category.AddJumpListItems(link);
            }
            jumpList.AddCustomCategories(category);

            jumpList.Refresh();
            Log($"Jump list updated: {activeSessions.Count} sessions");
        }
        catch (Exception ex)
        {
            Log($"UpdateJumpList error: {ex.Message}");
        }
    }

    #endregion

    #region PID Registry

    static void RegisterPid(int pid)
    {
        try
        {
            if (!Directory.Exists(CopilotDir))
                Directory.CreateDirectory(CopilotDir);

            Dictionary<string, object> registry = new();
            if (File.Exists(PidRegistryFile))
            {
                try { registry = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(PidRegistryFile)) ?? new(); }
                catch { }
            }
            registry[pid.ToString()] = new { started = DateTime.Now.ToString("o"), sessionId = (string?)null };
            File.WriteAllText(PidRegistryFile, JsonSerializer.Serialize(registry));
        }
        catch { }
    }

    static void UpdatePidSessionId(int pid, string sessionId)
    {
        try
        {
            if (!File.Exists(PidRegistryFile)) return;
            var json = File.ReadAllText(PidRegistryFile);
            var registry = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();

            registry[pid.ToString()] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(new { started = DateTime.Now.ToString("o"), sessionId }));

            File.WriteAllText(PidRegistryFile, JsonSerializer.Serialize(registry));
        }
        catch { }
    }

    static void UnregisterPid(int pid)
    {
        try
        {
            if (!File.Exists(PidRegistryFile)) return;
            var registry = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(PidRegistryFile)) ?? new();
            registry.Remove(pid.ToString());
            File.WriteAllText(PidRegistryFile, JsonSerializer.Serialize(registry));
        }
        catch { }
    }

    #endregion

    #region Session Discovery

    static List<SessionInfo> GetActiveSessions()
    {
        var sessions = new List<SessionInfo>();
        if (!File.Exists(PidRegistryFile)) return sessions;

        Dictionary<string, JsonElement>? pidRegistry;
        try
        {
            pidRegistry = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(PidRegistryFile));
        }
        catch { return sessions; }

        if (pidRegistry == null) return sessions;

        var toRemove = new List<string>();

        foreach (var (pidStr, entry) in pidRegistry)
        {
            if (!int.TryParse(pidStr, out int pid)) continue;

            try
            {
                var proc = Process.GetProcessById(pid);
                if (proc.ProcessName != "CopilotPermissive")
                {
                    toRemove.Add(pidStr);
                    continue;
                }

                string? sessionId = null;
                if (entry.TryGetProperty("sessionId", out var sidProp) && sidProp.ValueKind == JsonValueKind.String)
                    sessionId = sidProp.GetString();

                if (sessionId == null) continue;

                var workspaceFile = Path.Combine(SessionStateDir, sessionId, "workspace.yaml");
                if (!File.Exists(workspaceFile)) continue;

                var session = ParseWorkspace(workspaceFile, pid);
                if (session != null)
                    sessions.Add(session);
            }
            catch { toRemove.Add(pidStr); }
        }

        if (toRemove.Count > 0)
        {
            foreach (var pid in toRemove)
                pidRegistry.Remove(pid);
            try { File.WriteAllText(PidRegistryFile, JsonSerializer.Serialize(pidRegistry)); } catch { }
        }

        return sessions;
    }

    static SessionInfo? ParseWorkspace(string path, int pid)
    {
        try
        {
            var lines = File.ReadAllLines(path);
            string? id = null, cwd = null, summary = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("id:")) id = line[3..].Trim();
                else if (line.StartsWith("cwd:")) cwd = line[4..].Trim();
                else if (line.StartsWith("summary:")) summary = line[8..].Trim();
            }

            if (id == null) return null;

            var folder = Path.GetFileName(cwd?.TrimEnd('\\') ?? "Unknown");
            return new SessionInfo
            {
                Id = id,
                Cwd = cwd ?? "Unknown",
                Summary = string.IsNullOrEmpty(summary) ? $"[{folder}]" : $"[{folder}] {summary}",
                Pid = pid
            };
        }
        catch { return null; }
    }

    #endregion

    #region Session Picker

    static string? ShowSessionPicker()
    {
        if (!Directory.Exists(SessionStateDir)) return null;

        var sessions = Directory.GetDirectories(SessionStateDir)
            .OrderByDescending(d => Directory.GetLastWriteTime(d))
            .Select(d =>
            {
                var wsFile = Path.Combine(d, "workspace.yaml");
                if (!File.Exists(wsFile)) return null;

                var lines = File.ReadAllLines(wsFile);
                string? id = null, cwd = null, summary = null;
                foreach (var line in lines)
                {
                    if (line.StartsWith("id:")) id = line[3..].Trim();
                    else if (line.StartsWith("cwd:")) cwd = line[4..].Trim();
                    else if (line.StartsWith("summary:")) summary = line[8..].Trim();
                }

                if (id == null || string.IsNullOrWhiteSpace(summary)) return null;

                var folder = Path.GetFileName(cwd?.TrimEnd('\\') ?? "");
                return new { Id = id, Cwd = cwd ?? "", Folder = folder, Summary = summary, Dir = d };
            })
            .Where(s => s != null)
            .Take(50)
            .ToList();

        if (sessions.Count == 0)
        {
            MessageBox.Show("No named sessions found.", "Open Existing Session", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        var form = new Form
        {
            Text = "Open Existing Session",
            Size = new System.Drawing.Size(600, 500),
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.Sizable,
            MinimumSize = new System.Drawing.Size(400, 300)
        };

        try
        {
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(CopilotExePath);
            if (icon != null) form.Icon = icon;
        }
        catch { }

        var listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Tile,
            FullRowSelect = true,
            MultiSelect = false,
            TileSize = new System.Drawing.Size(560, 50)
        };
        listView.Columns.Add("Session");
        listView.Columns.Add("Details");

        foreach (var session in sessions)
        {
            if (session == null) continue;
            var lastWrite = Directory.GetLastWriteTime(session.Dir);
            var item = new ListViewItem(session.Summary) { Tag = session.Id };
            item.SubItems.Add($"[{session.Folder}]  •  {lastWrite:yyyy-MM-dd HH:mm}");
            listView.Items.Add(item);
        }

        string? selectedId = null;

        listView.DoubleClick += (s, e) =>
        {
            if (listView.SelectedItems.Count > 0)
            {
                selectedId = listView.SelectedItems[0].Tag as string;
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(5)
        };

        var btnCancel = new Button { Text = "Cancel", Width = 80 };
        btnCancel.Click += (s, e) => { form.DialogResult = DialogResult.Cancel; form.Close(); };

        var btnOpen = new Button { Text = "Open", Width = 80 };
        btnOpen.Click += (s, e) =>
        {
            if (listView.SelectedItems.Count > 0)
            {
                selectedId = listView.SelectedItems[0].Tag as string;
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
        };

        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnOpen);

        form.Controls.Add(listView);
        form.Controls.Add(buttonPanel);
        form.AcceptButton = btnOpen;
        form.CancelButton = btnCancel;

        return form.ShowDialog() == DialogResult.OK ? selectedId : null;
    }

    #endregion
}

class SessionInfo
{
    public string Id { get; set; } = "";
    public string Cwd { get; set; } = "";
    public string Summary { get; set; } = "";
    public int Pid { get; set; }
}
