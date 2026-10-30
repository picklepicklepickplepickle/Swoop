using Swoop.Core;
using Xunit;

namespace Swoop.Core.Tests;

public class ProgressParserTests
{
    [Theory]
    [InlineData("[download]  42.7% of ~3.01MiB at  1.23MiB/s ETA 00:05", 42.7)]
    [InlineData("[download]  100% of 12.34MiB", 100.0)]
    [InlineData("[download]   3.1% of ~ 45.67MiB ", 3.1)]
    [InlineData("[download] Destination: C:\\vid.mp4", null)]
    [InlineData("[Merger] Merging formats into \"out.mp4\"", null)]
    [InlineData("", null)]
    public void Parse_line_to_percent(string line, double? expected)
        => Assert.Equal(expected, ProgressParser.Parse(line));
}

public class ToolLocatorTests
{
    [Fact]
    public void Find_from_search_dirs_returns_exe_path()
    {
        var dir = ToolTestDir.Make(x => File.WriteAllText(Path.Combine(x, "yt-dlp.exe"), "stub"));
        Assert.Equal(Path.Combine(dir, "yt-dlp.exe"), ToolLocator.FindFromDirs("yt-dlp", [dir]));
    }

    [Fact]
    public void Find_missing_returns_null()
        => Assert.Null(ToolLocator.FindFromDirs("yt-dlp", [Path.GetTempPath(), "Z:\\does\\not\\exist"]));

    [Fact]
    public void Find_skips_dirs_without_the_tool()
    {
        var dir = ToolTestDir.Make(_ => { });
        Assert.Null(ToolLocator.FindFromDirs("ffmpeg", [dir]));
    }

    [Fact]
    public void Compose_env_dirs_yt_dlp_path_then_winget_links()
    {
        var d1 = ToolTestDir.Make(_ => {}); // PATH-style dir
        var d2 = ToolTestDir.Make(_ => {}); // winget-links-style dir
        var dirs = ToolLocator.ComposeSearchDirs(new[] { d1 }, d2);
        var l = new List<string>(dirs);
        Assert.Equal(0, l.IndexOf(d1));
        Assert.Equal(1, l.IndexOf(d2));
    }
}

file static class ToolTestDir
{
    public static string Make(Action<string> seed)
    {
        var dir = Path.Combine(Path.GetTempPath(), "gg-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        seed(dir);
        return dir;
    }
}

