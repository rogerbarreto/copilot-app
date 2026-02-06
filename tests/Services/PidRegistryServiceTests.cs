public sealed class PidRegistryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _pidFile;

    public PidRegistryTests()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(this._tempDir);
        this._pidFile = Path.Combine(this._tempDir, "active-pids.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(this._tempDir, true); } catch { }
    }

    [Fact]
    public void RegisterPid_CreatesRegistryFile()
    {
        PidRegistryService.RegisterPid(1234, this._tempDir, this._pidFile);

        Assert.True(File.Exists(this._pidFile));
        var json = File.ReadAllText(this._pidFile);
        Assert.Contains("1234", json);
    }

    [Fact]
    public void RegisterPid_WithCorruptExistingFile_OverwritesCleanly()
    {
        File.WriteAllText(this._pidFile, "corrupt json {{{");
        PidRegistryService.RegisterPid(5555, this._tempDir, this._pidFile);

        Assert.True(File.Exists(this._pidFile));
        var json = File.ReadAllText(this._pidFile);
        Assert.Contains("5555", json);
    }

    [Fact]
    public void RegisterPid_CreatesDirectory()
    {
        var subDir = Path.Combine(this._tempDir, "newsubdir");
        var subFile = Path.Combine(subDir, "pids.json");

        PidRegistryService.RegisterPid(1234, subDir, subFile);

        Assert.True(Directory.Exists(subDir));
        Assert.True(File.Exists(subFile));
    }

    [Fact]
    public void RegisterPid_AddsToExistingRegistry()
    {
        PidRegistryService.RegisterPid(1111, this._tempDir, this._pidFile);
        PidRegistryService.RegisterPid(2222, this._tempDir, this._pidFile);

        var json = File.ReadAllText(this._pidFile);
        Assert.Contains("1111", json);
        Assert.Contains("2222", json);
    }

    [Fact]
    public void UnregisterPid_RemovesPid()
    {
        PidRegistryService.RegisterPid(1234, this._tempDir, this._pidFile);
        PidRegistryService.RegisterPid(5678, this._tempDir, this._pidFile);

        PidRegistryService.UnregisterPid(1234, this._pidFile);

        var json = File.ReadAllText(this._pidFile);
        Assert.DoesNotContain("1234", json);
        Assert.Contains("5678", json);
    }

    [Fact]
    public void UnregisterPid_NoFileExists_DoesNotThrow()
    {
        var nonExistent = Path.Combine(this._tempDir, "no-such-file.json");
        var ex = Record.Exception(() => PidRegistryService.UnregisterPid(1234, nonExistent));
        Assert.Null(ex);
    }

    [Fact]
    public void UpdatePidSessionId_UpdatesExistingPid()
    {
        PidRegistryService.RegisterPid(1234, this._tempDir, this._pidFile);
        PidRegistryService.UpdatePidSessionId(1234, "session-abc", this._pidFile);

        var json = File.ReadAllText(this._pidFile);
        Assert.Contains("session-abc", json);
    }

    [Fact]
    public void UpdatePidSessionId_StoresCopilotPid()
    {
        PidRegistryService.RegisterPid(1234, this._tempDir, this._pidFile);
        PidRegistryService.UpdatePidSessionId(1234, "session-abc", this._pidFile, copilotPid: 5678);

        var json = File.ReadAllText(this._pidFile);
        Assert.Contains("session-abc", json);
        Assert.Contains("5678", json);

        var registry = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json)!;
        var entry = registry["1234"];
        Assert.Equal(5678, entry.GetProperty("copilotPid").GetInt32());
    }

    [Fact]
    public void UpdatePidSessionId_DefaultCopilotPidIsZero()
    {
        PidRegistryService.RegisterPid(1234, this._tempDir, this._pidFile);
        PidRegistryService.UpdatePidSessionId(1234, "session-abc", this._pidFile);

        var json = File.ReadAllText(this._pidFile);
        var registry = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(json)!;
        var entry = registry["1234"];
        Assert.Equal(0, entry.GetProperty("copilotPid").GetInt32());
    }

    [Fact]
    public void UpdatePidSessionId_NoFileExists_DoesNotThrow()
    {
        var nonExistent = Path.Combine(this._tempDir, "no-such-file.json");
        var ex = Record.Exception(() => PidRegistryService.UpdatePidSessionId(1234, "s1", nonExistent));
        Assert.Null(ex);
    }
}
