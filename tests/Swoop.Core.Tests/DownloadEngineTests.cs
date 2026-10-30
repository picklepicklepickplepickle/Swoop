using System.Diagnostics;
using Swoop.Core;
using Xunit;

namespace Swoop.Core.Tests;

public class DownloadEngineTests
{
    // Cancellation seam: stub the "yt-dlp" process with cmd /c ping (10 s),
    // cancel ~500 ms in, expect Cancelled fast.
    [Fact]
    public async Task Cancel_kills_stub_and_returns_cancelled()
    {
        var engine = new DownloadEngine(
            "cmd.exe",
            _ => ["/c", "ping", "-n", "10", "127.0.0.1"]);
        using var cts = new CancellationTokenSource(500);

        var sw = Stopwatch.StartNew();
        var r = await engine.RunAsync(
            new DownloadOptions
            {
                Url = "stub",
                Format = "mp4",
                OutputDir = ToolTestDir.Useless()
            },
            null,
            cts.Token);
        sw.Stop();

        Assert.True(r.Cancelled);
        Assert.False(r.Success);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), "cancel should return quickly");
        // sequential: a second RunAsync must start fresh after cancel
        using var cts2 = new CancellationTokenSource(300);
        var r2 = await engine.RunAsync(
            new DownloadOptions { Url = "stub", Format = "mp4", OutputDir = ToolTestDir.Useless() }, null, cts2.Token);
        Assert.True(r2.Cancelled);
    }
}

file static class ToolTestDir
{
    public static string Useless() => Path.Combine(Path.GetTempPath(), "gg-e-" + Guid.NewGuid().ToString("N"));
}

