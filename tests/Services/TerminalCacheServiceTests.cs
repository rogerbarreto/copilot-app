using System.Text.Json;

public sealed class TerminalCacheServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheFile;

    public TerminalCacheServiceTests()
    {
        this._tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(this._tempDir);
        this._cacheFile = Path.Combine(this._tempDir, "terminal-cache.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(this._tempDir, true); } catch { }
    }

    [Fact]
    public void CacheTerminal_CreatesFileAndEntry()
    {
        TerminalCacheService.CacheTerminal(this._cacheFile, "session-1", 1234);

        Assert.True(File.Exists(this._cacheFile));
        var json = File.ReadAllText(this._cacheFile);
        var cache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        Assert.True(cache.ContainsKey("session-1"));
        Assert.Equal(1234, cache["session-1"].GetProperty("copilotPid").GetInt32());
    }

    [Fact]
    public void CacheTerminal_UpdatesExistingEntry()
    {
        TerminalCacheService.CacheTerminal(this._cacheFile, "session-1", 1111);
        TerminalCacheService.CacheTerminal(this._cacheFile, "session-2", 2222);

        var json = File.ReadAllText(this._cacheFile);
        var cache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        Assert.Equal(2, cache.Count);
        Assert.Equal(1111, cache["session-1"].GetProperty("copilotPid").GetInt32());
        Assert.Equal(2222, cache["session-2"].GetProperty("copilotPid").GetInt32());
    }

    [Fact]
    public void GetCachedTerminals_ReturnsEmpty_WhenFileDoesNotExist()
    {
        var nonExistent = Path.Combine(this._tempDir, "no-such-file.json");
        var result = TerminalCacheService.GetCachedTerminals(nonExistent);
        Assert.Empty(result);
    }

    [Fact]
    public void GetCachedTerminals_ReturnsAllCachedSessions()
    {
        TerminalCacheService.CacheTerminal(this._cacheFile, "session-a", 1111);
        TerminalCacheService.CacheTerminal(this._cacheFile, "session-b", 2222);

        var result = TerminalCacheService.GetCachedTerminals(this._cacheFile);

        Assert.Contains("session-a", result);
        Assert.Contains("session-b", result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetCachedTerminals_DoesNotModifyCacheFile()
    {
        TerminalCacheService.CacheTerminal(this._cacheFile, "session-1", 99999);
        var beforeJson = File.ReadAllText(this._cacheFile);

        TerminalCacheService.GetCachedTerminals(this._cacheFile);

        var afterJson = File.ReadAllText(this._cacheFile);
        Assert.Equal(beforeJson, afterJson);
    }

    [Fact]
    public void RemoveTerminal_RemovesEntry()
    {
        TerminalCacheService.CacheTerminal(this._cacheFile, "session-1", 1234);
        TerminalCacheService.RemoveTerminal(this._cacheFile, "session-1");

        var json = File.ReadAllText(this._cacheFile);
        var cache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        Assert.False(cache.ContainsKey("session-1"));
    }

    [Fact]
    public void RemoveTerminal_NoopWhenFileDoesNotExist()
    {
        var nonExistent = Path.Combine(this._tempDir, "no-such-file.json");
        var ex = Record.Exception(() => TerminalCacheService.RemoveTerminal(nonExistent, "session-1"));
        Assert.Null(ex);
    }
}
