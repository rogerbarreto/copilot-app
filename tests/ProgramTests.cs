using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

public class LauncherSettingsTests : IDisposable
{
    private readonly string _tempDir;

    public LauncherSettingsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Load_WhenFileNotExists_CreatesDefaultFile()
    {
        var file = Path.Combine(_tempDir, "sub", "settings.json");
        var settings = LauncherSettings.Load(file);

        Assert.True(File.Exists(file));
        var loaded = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(file));
        Assert.NotNull(loaded);
        Assert.Empty(loaded!.AllowedTools);
    }

    [Fact]
    public void Load_WhenFileNotExists_ReturnsDefault()
    {
        var file = Path.Combine(_tempDir, "nonexistent", "settings.json");
        var settings = LauncherSettings.Load(file);

        Assert.NotNull(settings);
        Assert.Empty(settings.AllowedTools);
        Assert.Empty(settings.AllowedDirs);
        Assert.Equal("", settings.DefaultWorkDir);
    }

    [Fact]
    public void Load_WhenFileExists_DeserializesCorrectly()
    {
        var file = Path.Combine(_tempDir, "settings.json");
        var json = JsonSerializer.Serialize(new
        {
            allowedTools = new[] { "tool1", "tool2" },
            allowedDirs = new[] { @"C:\dir1" },
            defaultWorkDir = @"C:\work",
            ides = new[] { new { path = @"C:\code.exe", description = "VS Code" } }
        });
        File.WriteAllText(file, json);

        var settings = LauncherSettings.Load(file);

        Assert.Equal(2, settings.AllowedTools.Count);
        Assert.Contains("tool1", settings.AllowedTools);
        Assert.Contains("tool2", settings.AllowedTools);
        Assert.Single(settings.AllowedDirs);
        Assert.Equal(@"C:\dir1", settings.AllowedDirs[0]);
        Assert.Equal(@"C:\work", settings.DefaultWorkDir);
        Assert.Single(settings.Ides);
        Assert.Equal("VS Code", settings.Ides[0].Description);
        Assert.Equal(@"C:\code.exe", settings.Ides[0].Path);
    }

    [Fact]
    public void Load_WhenFileCorrupt_ReturnsDefault()
    {
        var file = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(file, "not valid json {{{");

        var settings = LauncherSettings.Load(file);

        Assert.NotNull(settings);
        Assert.Empty(settings.AllowedTools);
        Assert.Empty(settings.AllowedDirs);
    }

    [Fact]
    public void Save_CreatesDirectoryAndFile()
    {
        var file = Path.Combine(_tempDir, "sub", "deep", "settings.json");
        var settings = new LauncherSettings
        {
            AllowedTools = new List<string> { "mytool" },
            DefaultWorkDir = @"C:\mywork"
        };

        settings.Save(file);

        Assert.True(File.Exists(file));
        var loaded = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(file));
        Assert.NotNull(loaded);
        Assert.Contains("mytool", loaded!.AllowedTools);
        Assert.Equal(@"C:\mywork", loaded.DefaultWorkDir);
    }

    [Fact]
    public void Save_OverwritesExisting()
    {
        var file = Path.Combine(_tempDir, "settings.json");
        var s1 = new LauncherSettings { DefaultWorkDir = "first" };
        s1.Save(file);

        var s2 = new LauncherSettings { DefaultWorkDir = "second" };
        s2.Save(file);

        var loaded = LauncherSettings.Load(file);
        Assert.Equal("second", loaded.DefaultWorkDir);
    }

    [Fact]
    public void CreateDefault_HasEmptyCollections()
    {
        var settings = LauncherSettings.CreateDefault();

        Assert.Empty(settings.AllowedTools);
        Assert.Empty(settings.AllowedDirs);
        Assert.Equal("", settings.DefaultWorkDir);
    }

    [Fact]
    public void BuildCopilotArgs_NoToolsNoDirs_ReturnsExtraArgsOnly()
    {
        var settings = new LauncherSettings();
        var result = settings.BuildCopilotArgs(new[] { "--resume", "abc" });

        Assert.Equal("--resume abc", result);
    }

    [Fact]
    public void BuildCopilotArgs_WithToolsAndDirs_FormatsCorrectly()
    {
        var settings = new LauncherSettings
        {
            AllowedTools = new List<string> { "bash", "python" },
            AllowedDirs = new List<string> { @"C:\code", @"D:\work" }
        };

        var result = settings.BuildCopilotArgs(Array.Empty<string>());

        Assert.Contains("\"--allow-tool=bash\"", result);
        Assert.Contains("\"--allow-tool=python\"", result);
        Assert.Contains("\"--add-dir=C:\\code\"", result);
        Assert.Contains("\"--add-dir=D:\\work\"", result);
    }

    [Fact]
    public void BuildCopilotArgs_WithExtraArgs_AppendsCorrectly()
    {
        var settings = new LauncherSettings
        {
            AllowedTools = new List<string> { "tool1" }
        };

        var result = settings.BuildCopilotArgs(new[] { "--resume session1" });

        Assert.StartsWith("\"--allow-tool=tool1\"", result);
        Assert.EndsWith("--resume session1", result);
    }

    [Fact]
    public void BuildCopilotArgs_EmptyExtraArgs_ReturnsToolsAndDirsOnly()
    {
        var settings = new LauncherSettings
        {
            AllowedTools = new List<string> { "tool1" },
            AllowedDirs = new List<string> { @"C:\dir" }
        };

        var result = settings.BuildCopilotArgs(Array.Empty<string>());

        Assert.Equal("\"--allow-tool=tool1\" \"--add-dir=C:\\dir\"", result);
    }
}

public class IdeEntryTests
{
    [Fact]
    public void ToString_WithDescription_ReturnsDescriptionAndPath()
    {
        var entry = new IdeEntry { Description = "VS Code", Path = @"C:\code.exe" };
        Assert.Equal(@"VS Code  —  C:\code.exe", entry.ToString());
    }

    [Fact]
    public void ToString_WithoutDescription_ReturnsPathOnly()
    {
        var entry = new IdeEntry { Description = "", Path = @"C:\code.exe" };
        Assert.Equal(@"C:\code.exe", entry.ToString());
    }

    [Fact]
    public void ToString_EmptyDescriptionAndPath_ReturnsEmptyPath()
    {
        var entry = new IdeEntry { Description = "", Path = "" };
        Assert.Equal("", entry.ToString());
    }
}

public class ParseWorkspaceTests : IDisposable
{
    private readonly string _tempDir;

    public ParseWorkspaceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void ParseWorkspace_ValidFile_ReturnsSessionInfo()
    {
        var wsFile = Path.Combine(_tempDir, "workspace.yaml");
        File.WriteAllText(wsFile, "id: session-123\ncwd: C:\\myproject\nsummary: Fix the bug");

        var result = Program.ParseWorkspace(wsFile, 42);

        Assert.NotNull(result);
        Assert.Equal("session-123", result!.Id);
        Assert.Equal(@"C:\myproject", result.Cwd);
        Assert.Equal("[myproject] Fix the bug", result.Summary);
        Assert.Equal(42, result.Pid);
    }

    [Fact]
    public void ParseWorkspace_MissingId_ReturnsNull()
    {
        var wsFile = Path.Combine(_tempDir, "workspace.yaml");
        File.WriteAllText(wsFile, "cwd: C:\\myproject\nsummary: Fix the bug");

        var result = Program.ParseWorkspace(wsFile, 1);

        Assert.Null(result);
    }

    [Fact]
    public void ParseWorkspace_MissingSummary_ReturnsFolderOnlyBrackets()
    {
        var wsFile = Path.Combine(_tempDir, "workspace.yaml");
        File.WriteAllText(wsFile, "id: session-456\ncwd: C:\\myproject");

        var result = Program.ParseWorkspace(wsFile, 1);

        Assert.NotNull(result);
        Assert.Equal("[myproject]", result!.Summary);
    }

    [Fact]
    public void ParseWorkspace_WithSummary_ReturnsFolderAndSummary()
    {
        var wsFile = Path.Combine(_tempDir, "workspace.yaml");
        File.WriteAllText(wsFile, "id: s1\ncwd: D:\\repos\\app\nsummary: Implement feature X");

        var result = Program.ParseWorkspace(wsFile, 5);

        Assert.NotNull(result);
        Assert.Equal("[app] Implement feature X", result!.Summary);
    }

    [Fact]
    public void ParseWorkspace_FileNotFound_ReturnsNull()
    {
        var result = Program.ParseWorkspace(Path.Combine(_tempDir, "missing.yaml"), 1);

        Assert.Null(result);
    }

    [Fact]
    public void ParseWorkspace_EmptyCwd_ReturnsEmptyFolder()
    {
        var wsFile = Path.Combine(_tempDir, "workspace.yaml");
        File.WriteAllText(wsFile, "id: s1\ncwd: \nsummary: test");

        var result = Program.ParseWorkspace(wsFile, 1);

        Assert.NotNull(result);
        Assert.Equal("[] test", result!.Summary);
    }

    [Fact]
    public void ParseWorkspace_CwdWithTrailingBackslash_StripsIt()
    {
        var wsFile = Path.Combine(_tempDir, "workspace.yaml");
        File.WriteAllText(wsFile, "id: s1\ncwd: C:\\myproject\\\nsummary: test");

        var result = Program.ParseWorkspace(wsFile, 1);

        Assert.NotNull(result);
        Assert.Equal("[myproject] test", result!.Summary);
    }

    [Fact]
    public void ParseWorkspace_NoCwd_ReturnsUnknown()
    {
        var wsFile = Path.Combine(_tempDir, "workspace.yaml");
        File.WriteAllText(wsFile, "id: s1\nsummary: test");

        var result = Program.ParseWorkspace(wsFile, 1);

        Assert.NotNull(result);
        Assert.Equal("Unknown", result!.Cwd);
    }
}

public class LoadNamedSessionsTests : IDisposable
{
    private readonly string _tempDir;

    public LoadNamedSessionsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void LoadNamedSessions_NoSessionStateDir_ReturnsEmpty()
    {
        var nonExistent = Path.Combine(_tempDir, "nonexistent");
        var result = MainForm.LoadNamedSessions(nonExistent);
        Assert.Empty(result);
    }

    [Fact]
    public void LoadNamedSessions_WithValidSessions_ReturnsParsedSessions()
    {
        var sessionDir = Path.Combine(_tempDir, "session1");
        Directory.CreateDirectory(sessionDir);
        File.WriteAllText(Path.Combine(sessionDir, "workspace.yaml"),
            "id: session1\ncwd: C:\\project\nsummary: My session");

        var result = MainForm.LoadNamedSessions(_tempDir);

        Assert.Single(result);
        Assert.Equal("session1", result[0].Id);
        Assert.Equal("[project] My session", result[0].Summary);
    }

    [Fact]
    public void LoadNamedSessions_SkipsSessionsWithoutSummary()
    {
        var s1 = Path.Combine(_tempDir, "s1");
        Directory.CreateDirectory(s1);
        File.WriteAllText(Path.Combine(s1, "workspace.yaml"), "id: s1\ncwd: C:\\a\nsummary: Has summary");

        var s2 = Path.Combine(_tempDir, "s2");
        Directory.CreateDirectory(s2);
        File.WriteAllText(Path.Combine(s2, "workspace.yaml"), "id: s2\ncwd: C:\\b");

        var result = MainForm.LoadNamedSessions(_tempDir);

        Assert.Single(result);
        Assert.Equal("s1", result[0].Id);
    }

    [Fact]
    public void LoadNamedSessions_MaxFiftySessions()
    {
        for (int i = 0; i < 60; i++)
        {
            var dir = Path.Combine(_tempDir, $"session-{i:D3}");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "workspace.yaml"),
                $"id: session-{i:D3}\ncwd: C:\\proj{i}\nsummary: Session {i}");
        }

        var result = MainForm.LoadNamedSessions(_tempDir);

        Assert.Equal(50, result.Count);
    }

    [Fact]
    public void LoadNamedSessions_OrderedByLastModified()
    {
        var s1 = Path.Combine(_tempDir, "old-session");
        Directory.CreateDirectory(s1);
        File.WriteAllText(Path.Combine(s1, "workspace.yaml"),
            "id: old\ncwd: C:\\old\nsummary: Old session");
        Directory.SetLastWriteTime(s1, DateTime.Now.AddHours(-2));

        var s2 = Path.Combine(_tempDir, "new-session");
        Directory.CreateDirectory(s2);
        File.WriteAllText(Path.Combine(s2, "workspace.yaml"),
            "id: new\ncwd: C:\\new\nsummary: New session");
        Directory.SetLastWriteTime(s2, DateTime.Now);

        var result = MainForm.LoadNamedSessions(_tempDir);

        Assert.Equal(2, result.Count);
        Assert.Equal("new", result[0].Id);
        Assert.Equal("old", result[1].Id);
    }
}

public class PidRegistryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _pidFile;

    public PidRegistryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _pidFile = Path.Combine(_tempDir, "active-pids.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void RegisterPid_CreatesRegistryFile()
    {
        Program.RegisterPid(1234, _tempDir, _pidFile);

        Assert.True(File.Exists(_pidFile));
        var json = File.ReadAllText(_pidFile);
        Assert.Contains("1234", json);
    }

    [Fact]
    public void RegisterPid_WithCorruptExistingFile_OverwritesCleanly()
    {
        File.WriteAllText(_pidFile, "corrupt json {{{");
        Program.RegisterPid(5555, _tempDir, _pidFile);

        Assert.True(File.Exists(_pidFile));
        var json = File.ReadAllText(_pidFile);
        Assert.Contains("5555", json);
    }

    [Fact]
    public void RegisterPid_CreatesDirectory()
    {
        var subDir = Path.Combine(_tempDir, "newsubdir");
        var subFile = Path.Combine(subDir, "pids.json");

        Program.RegisterPid(1234, subDir, subFile);

        Assert.True(Directory.Exists(subDir));
        Assert.True(File.Exists(subFile));
    }

    [Fact]
    public void RegisterPid_AddsToExistingRegistry()
    {
        Program.RegisterPid(1111, _tempDir, _pidFile);
        Program.RegisterPid(2222, _tempDir, _pidFile);

        var json = File.ReadAllText(_pidFile);
        Assert.Contains("1111", json);
        Assert.Contains("2222", json);
    }

    [Fact]
    public void UnregisterPid_RemovesPid()
    {
        Program.RegisterPid(1234, _tempDir, _pidFile);
        Program.RegisterPid(5678, _tempDir, _pidFile);

        Program.UnregisterPid(1234, _pidFile);

        var json = File.ReadAllText(_pidFile);
        Assert.DoesNotContain("1234", json);
        Assert.Contains("5678", json);
    }

    [Fact]
    public void UnregisterPid_NoFileExists_DoesNotThrow()
    {
        var nonExistent = Path.Combine(_tempDir, "no-such-file.json");
        var ex = Record.Exception(() => Program.UnregisterPid(1234, nonExistent));
        Assert.Null(ex);
    }

    [Fact]
    public void UpdatePidSessionId_UpdatesExistingPid()
    {
        Program.RegisterPid(1234, _tempDir, _pidFile);
        Program.UpdatePidSessionId(1234, "session-abc", _pidFile);

        var json = File.ReadAllText(_pidFile);
        Assert.Contains("session-abc", json);
    }

    [Fact]
    public void UpdatePidSessionId_NoFileExists_DoesNotThrow()
    {
        var nonExistent = Path.Combine(_tempDir, "no-such-file.json");
        var ex = Record.Exception(() => Program.UpdatePidSessionId(1234, "s1", nonExistent));
        Assert.Null(ex);
    }
}

public class GetActiveSessionsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _pidFile;
    private readonly string _sessionStateDir;

    public GetActiveSessionsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _pidFile = Path.Combine(_tempDir, "active-pids.json");
        _sessionStateDir = Path.Combine(_tempDir, "session-state");
        Directory.CreateDirectory(_sessionStateDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void GetActiveSessions_NoPidFile_ReturnsEmpty()
    {
        var result = Program.GetActiveSessions(
            Path.Combine(_tempDir, "nonexistent.json"), _sessionStateDir);
        Assert.Empty(result);
    }

    [Fact]
    public void GetActiveSessions_EmptyRegistry_ReturnsEmpty()
    {
        File.WriteAllText(_pidFile, "{}");
        var result = Program.GetActiveSessions(_pidFile, _sessionStateDir);
        Assert.Empty(result);
    }

    [Fact]
    public void GetActiveSessions_InvalidJson_ReturnsEmpty()
    {
        File.WriteAllText(_pidFile, "not json at all");
        var result = Program.GetActiveSessions(_pidFile, _sessionStateDir);
        Assert.Empty(result);
    }

    [Fact]
    public void GetActiveSessions_PidNotRunning_RemovesStalePid()
    {
        var fakePid = 99999;
        var registry = new Dictionary<string, object>
        {
            [fakePid.ToString()] = new { started = DateTime.Now.ToString("o"), sessionId = "s1" }
        };
        File.WriteAllText(_pidFile, JsonSerializer.Serialize(registry));

        var result = Program.GetActiveSessions(_pidFile, _sessionStateDir);

        Assert.Empty(result);
        var updatedJson = File.ReadAllText(_pidFile);
        Assert.DoesNotContain(fakePid.ToString(), updatedJson);
    }

    [Fact]
    public void GetActiveSessions_NonNumericPid_Skipped()
    {
        var registry = new Dictionary<string, object>
        {
            ["not-a-number"] = new { started = DateTime.Now.ToString("o"), sessionId = "s1" }
        };
        File.WriteAllText(_pidFile, JsonSerializer.Serialize(registry));

        var result = Program.GetActiveSessions(_pidFile, _sessionStateDir);

        Assert.Empty(result);
    }

    [Fact]
    public void GetActiveSessions_NullSessionId_SkipsEntry()
    {
        // Use current process PID (it's running but sessionId is null)
        var myPid = Environment.ProcessId;
        var registry = new Dictionary<string, object>
        {
            [myPid.ToString()] = new { started = DateTime.Now.ToString("o"), sessionId = (string?)null }
        };
        File.WriteAllText(_pidFile, JsonSerializer.Serialize(registry));

        var result = Program.GetActiveSessions(_pidFile, _sessionStateDir);

        // Process is running but not CopilotApp, so it gets removed
        // OR sessionId is null so it gets skipped — either way empty
        Assert.Empty(result);
    }

    [Fact]
    public void GetActiveSessions_NoWorkspaceFile_SkipsEntry()
    {
        var myPid = Environment.ProcessId;
        var registry = new Dictionary<string, object>
        {
            [myPid.ToString()] = new { started = DateTime.Now.ToString("o"), sessionId = "nonexistent-session" }
        };
        File.WriteAllText(_pidFile, JsonSerializer.Serialize(registry));

        var result = Program.GetActiveSessions(_pidFile, _sessionStateDir);

        Assert.Empty(result);
    }
}

public class FindGitRootTests : IDisposable
{
    private readonly string _tempDir;

    public FindGitRootTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void FindGitRoot_HasGitDir_ReturnsRoot()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));

        var result = Program.FindGitRoot(_tempDir);

        Assert.Equal(_tempDir, result);
    }

    [Fact]
    public void FindGitRoot_NoGitDir_ReturnsNull()
    {
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);

        var result = Program.FindGitRoot(subDir);

        Assert.Null(result);
    }

    [Fact]
    public void FindGitRoot_NestedDir_FindsParent()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));
        var child = Path.Combine(_tempDir, "src", "deep");
        Directory.CreateDirectory(child);

        var result = Program.FindGitRoot(child);

        Assert.Equal(_tempDir, result);
    }

    [Fact]
    public void FindGitRoot_RootDir_ReturnsNull()
    {
        var root = Path.GetPathRoot(Path.GetTempPath())!;
        var result = Program.FindGitRoot(root);

        // Should not infinite loop; may return null or root depending on if .git exists there
        // The key assertion is that it terminates without error
        Assert.True(result == null || Directory.Exists(Path.Combine(result, ".git")));
    }
}

public class ShouldBackgroundUpdateTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _lastUpdateFile;

    public ShouldBackgroundUpdateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _lastUpdateFile = Path.Combine(_tempDir, "lastupdate.txt");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void ShouldBackgroundUpdate_NoFile_ReturnsTrue()
    {
        var result = Program.ShouldBackgroundUpdate(TimeSpan.FromMinutes(1), _lastUpdateFile);
        Assert.True(result);
    }

    [Fact]
    public void ShouldBackgroundUpdate_RecentUpdate_ReturnsFalse()
    {
        File.WriteAllText(_lastUpdateFile, DateTime.UtcNow.ToString("o"));

        var result = Program.ShouldBackgroundUpdate(TimeSpan.FromMinutes(5), _lastUpdateFile);

        Assert.False(result);
    }

    [Fact]
    public void ShouldBackgroundUpdate_OldUpdate_ReturnsTrue()
    {
        File.WriteAllText(_lastUpdateFile, DateTime.UtcNow.AddHours(-2).ToString("o"));

        var result = Program.ShouldBackgroundUpdate(TimeSpan.FromMinutes(1), _lastUpdateFile);

        Assert.True(result);
    }

    [Fact]
    public void ShouldBackgroundUpdate_InvalidContent_ReturnsTrue()
    {
        File.WriteAllText(_lastUpdateFile, "not a date");

        var result = Program.ShouldBackgroundUpdate(TimeSpan.FromMinutes(1), _lastUpdateFile);

        Assert.True(result);
    }
}

public class LogTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _logFile;

    public LogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _logFile = Path.Combine(_tempDir, "sub", "launcher.log");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Log_CreatesDirectoryAndAppendsMessage()
    {
        Program.Log("test message", _logFile);

        Assert.True(File.Exists(_logFile));
        var content = File.ReadAllText(_logFile);
        Assert.Contains("test message", content);
    }

    [Fact]
    public void Log_AppendsMultipleMessages()
    {
        Program.Log("first", _logFile);
        Program.Log("second", _logFile);

        var content = File.ReadAllText(_logFile);
        Assert.Contains("first", content);
        Assert.Contains("second", content);
        Assert.Equal(2, content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }
}

public class SessionInfoTests
{
    [Fact]
    public void SessionInfo_DefaultPropertyValues()
    {
        var info = new SessionInfo();

        Assert.Equal("", info.Id);
        Assert.Equal("", info.Cwd);
        Assert.Equal("", info.Summary);
        Assert.Equal(0, info.Pid);
    }

    [Fact]
    public void SessionInfo_PropertyGettersSetters()
    {
        var info = new SessionInfo
        {
            Id = "test-id",
            Cwd = @"C:\test",
            Summary = "Test summary",
            Pid = 42
        };

        Assert.Equal("test-id", info.Id);
        Assert.Equal(@"C:\test", info.Cwd);
        Assert.Equal("Test summary", info.Summary);
        Assert.Equal(42, info.Pid);
    }
}

public class NamedSessionTests
{
    [Fact]
    public void NamedSession_DefaultPropertyValues()
    {
        var session = new NamedSession();

        Assert.Equal("", session.Id);
        Assert.Equal("", session.Cwd);
        Assert.Equal("", session.Summary);
        Assert.Equal(default(DateTime), session.LastModified);
    }

    [Fact]
    public void NamedSession_PropertyGettersSetters()
    {
        var now = DateTime.Now;
        var session = new NamedSession
        {
            Id = "ns-1",
            Cwd = @"D:\work",
            Summary = "[work] Fix bug",
            LastModified = now
        };

        Assert.Equal("ns-1", session.Id);
        Assert.Equal(@"D:\work", session.Cwd);
        Assert.Equal("[work] Fix bug", session.Summary);
        Assert.Equal(now, session.LastModified);
    }
}

public class ParseArgumentsTests
{
    [Fact]
    public void ParseArguments_EmptyArgs_AllDefaults()
    {
        var result = Program.ParseArguments(Array.Empty<string>());

        Assert.Null(result.ResumeSessionId);
        Assert.False(result.OpenExisting);
        Assert.False(result.ShowSettings);
        Assert.Null(result.OpenIdeSessionId);
        Assert.Null(result.WorkDir);
    }

    [Fact]
    public void ParseArguments_Resume_SetsSessionId()
    {
        var result = Program.ParseArguments(new[] { "--resume", "session-123" });

        Assert.Equal("session-123", result.ResumeSessionId);
        Assert.False(result.OpenExisting);
    }

    [Fact]
    public void ParseArguments_OpenExisting_SetsFlag()
    {
        var result = Program.ParseArguments(new[] { "--open-existing" });

        Assert.True(result.OpenExisting);
        Assert.Null(result.ResumeSessionId);
    }

    [Fact]
    public void ParseArguments_Settings_SetsFlag()
    {
        var result = Program.ParseArguments(new[] { "--settings" });

        Assert.True(result.ShowSettings);
    }

    [Fact]
    public void ParseArguments_OpenIde_SetsSessionId()
    {
        var result = Program.ParseArguments(new[] { "--open-ide", "ide-session-1" });

        Assert.Equal("ide-session-1", result.OpenIdeSessionId);
    }

    [Fact]
    public void ParseArguments_WorkDir_SetsPath()
    {
        var result = Program.ParseArguments(new[] { @"D:\repo\work" });

        Assert.Equal(@"D:\repo\work", result.WorkDir);
    }

    [Fact]
    public void ParseArguments_ResumeWithoutValue_IgnoresFlag()
    {
        var result = Program.ParseArguments(new[] { "--resume" });

        Assert.Null(result.ResumeSessionId);
    }

    [Fact]
    public void ParseArguments_MultipleArgs_AllParsed()
    {
        var result = Program.ParseArguments(new[] { "--resume", "s1", "--settings", @"C:\work" });

        Assert.Equal("s1", result.ResumeSessionId);
        Assert.True(result.ShowSettings);
        Assert.Equal(@"C:\work", result.WorkDir);
    }
}

public class FindCopilotExeTests : IDisposable
{
    private readonly string _tempDir;

    public FindCopilotExeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void FindCopilotExe_CandidateExists_ReturnsIt()
    {
        var fakeCopilot = Path.Combine(_tempDir, "copilot.exe");
        File.WriteAllText(fakeCopilot, "fake");

        var result = Program.FindCopilotExe(new[] { fakeCopilot });

        Assert.Equal(fakeCopilot, result);
    }

    [Fact]
    public void FindCopilotExe_NoCandidatesExist_FallsBackToDefault()
    {
        var result = Program.FindCopilotExe(new[]
        {
            Path.Combine(_tempDir, "nonexistent1.exe"),
            Path.Combine(_tempDir, "nonexistent2.exe")
        });

        // Falls through to 'where' command or returns "copilot.exe"
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FindCopilotExe_FirstCandidateMatches_ReturnsFirst()
    {
        var first = Path.Combine(_tempDir, "first.exe");
        var second = Path.Combine(_tempDir, "second.exe");
        File.WriteAllText(first, "fake1");
        File.WriteAllText(second, "fake2");

        var result = Program.FindCopilotExe(new[] { first, second });

        Assert.Equal(first, result);
    }

    [Fact]
    public void FindCopilotExe_EmptyCandidates_FallsBackToDefault()
    {
        var result = Program.FindCopilotExe(Array.Empty<string>());

        Assert.NotEmpty(result);
    }
}
