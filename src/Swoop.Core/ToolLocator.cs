namespace Swoop.Core;

/// <summary>
/// Resolves yt-dlp / ffmpeg. Search order (spec constraint): PATH dirs,
/// then the WinGet Links directory; yt-dlp additionally auto-downloads from
/// GitHub's latest release when neither has it.
/// </summary>
public static class ToolLocator
{
    public const string WinGetLinksDirPattern = @"Microsoft\WinGet\Links";

    public static IReadOnlyList<string> ComposeSearchDirs(IEnumerable<string> pathDirs, string wingetLinksDir)
    {
        var list = new List<string>(pathDirs.Where(d => !string.IsNullOrWhiteSpace(d)));
        if (Directory.Exists(wingetLinksDir)) list.Add(wingetLinksDir);
        return list;
    }

    /// <summary>Testable core: first dir containing <paramref name="toolName"/> + ".exe" wins.</summary>
    public static string? FindFromDirs(string toolName, IEnumerable<string> dirs)
    {
        foreach (var dir in dirs)
        {
            var candidate = Path.Combine(Directory.Exists(dir) ? dir : dir, toolName + ".exe");
            try
            {
                if (File.Exists(candidate)) return candidate;
            }
            catch (System.IO.IOException) { /* unreadable dir â€” keep scanning */ }
        }
        return null;
    }

    /// <summary>Environment-facing resolver: current PATH dirs, then WinGet Links.</summary>
    public static string? Find(string toolName)
    {
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        var links = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), WinGetLinksDirPattern);
        return FindFromDirs(toolName, ComposeSearchDirs(pathDirs, links));
    }

    /// <summary>
    /// Downloads the newest yt-dlp.exe from the pinned GitHub release URL into
    /// <paramref name="destDir"/> and returns its full path.
    /// Throws with the URL in the message when the fetch fails.
    /// </summary>
    public static async Task<string> AutoFetchYtDlpAsync(string destDir)
    {
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, "yt-dlp.exe");
        const string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        try
        {
            using var http = new HttpClient();
            using var resp = await http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Create(dest);
            await resp.Content.CopyToAsync(fs);
            return dest;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Swoop could not auto-download yt-dlp from {url}: {e.Message}", e);
        }
    }
}

