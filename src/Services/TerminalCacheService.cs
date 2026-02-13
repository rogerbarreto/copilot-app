using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace CopilotApp.Services;

/// <summary>
/// Manages terminal PID caching for tracking running terminal sessions across app restarts.
/// </summary>
internal static class TerminalCacheService
{
    /// <summary>
    /// Adds or updates a terminal cache entry for the specified session.
    /// </summary>
    /// <param name="cacheFile">Path to the terminal cache JSON file.</param>
    /// <param name="sessionId">The session ID to cache.</param>
    /// <param name="copilotPid">The process ID of the copilot terminal.</param>
    internal static void CacheTerminal(string cacheFile, string sessionId, int copilotPid)
    {
        try
        {
            Dictionary<string, JsonElement> cache = [];
            if (File.Exists(cacheFile))
            {
                try
                {
                    cache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(cacheFile)) ?? [];
                }
                catch { }
            }

            cache[sessionId] = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(new { copilotPid, started = System.DateTime.Now.ToString("o") }));

            File.WriteAllText(cacheFile, JsonSerializer.Serialize(cache));
        }
        catch { }
    }

    /// <summary>
    /// Returns session IDs whose cached terminal process is still running.
    /// Dead entries are removed and the cleaned cache is written back.
    /// </summary>
    /// <param name="cacheFile">Path to the terminal cache JSON file.</param>
    /// <returns>A set of session IDs with still-running terminal processes.</returns>
    internal static HashSet<string> GetCachedTerminals(string cacheFile)
    {
        var alive = new HashSet<string>();
        try
        {
            if (!File.Exists(cacheFile))
            {
                return alive;
            }

            var cache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(cacheFile)) ?? [];
            var dead = new List<string>();

            foreach (var entry in cache)
            {
                bool isAlive = false;
                try
                {
                    if (entry.Value.TryGetProperty("copilotPid", out var pidElement))
                    {
                        int pid = pidElement.GetInt32();
                        Process.GetProcessById(pid);
                        isAlive = true;
                    }
                }
                catch { }

                if (isAlive)
                {
                    alive.Add(entry.Key);
                }
                else
                {
                    dead.Add(entry.Key);
                }
            }

            if (dead.Count > 0)
            {
                foreach (var key in dead)
                {
                    cache.Remove(key);
                }
                File.WriteAllText(cacheFile, JsonSerializer.Serialize(cache));
            }
        }
        catch { }

        return alive;
    }

    /// <summary>
    /// Removes the cache entry for the specified session.
    /// </summary>
    /// <param name="cacheFile">Path to the terminal cache JSON file.</param>
    /// <param name="sessionId">The session ID to remove.</param>
    internal static void RemoveTerminal(string cacheFile, string sessionId)
    {
        try
        {
            if (!File.Exists(cacheFile))
            {
                return;
            }

            var cache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(cacheFile)) ?? [];
            cache.Remove(sessionId);
            File.WriteAllText(cacheFile, JsonSerializer.Serialize(cache));
        }
        catch { }
    }
}
