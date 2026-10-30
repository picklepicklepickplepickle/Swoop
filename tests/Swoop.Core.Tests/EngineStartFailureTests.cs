using Swoop.Core;
using Xunit;

namespace Swoop.Core.Tests;

public class EngineStartFailureTests
{
    [Fact]
    public async Task Start_failure_returns_failure_not_throw()
    {
        var engine = new DownloadEngine("definitely-not-a-tool-xyz-nope.exe");
        var outDir = Path.Combine(Path.GetTempPath(), "gg-sf-" + Guid.NewGuid().ToString("N"));

        var r = await engine.RunAsync(
            new DownloadOptions { Url = "x", Format = "mp4", OutputDir = outDir },
            null,
            CancellationToken.None);

        Assert.False(r.Success);
        Assert.False(r.Cancelled);
        Assert.False(string.IsNullOrWhiteSpace(r.Error), "start failure must surface a message");
        // queue liveness: gate released, engine still usable (stub process, quick cancel)
        using var cts = new CancellationTokenSource(400);
        var engine2 = new DownloadEngine("cmd.exe", _ => ["/c", "ping", "-n", "10", "127.0.0.1"]);
        var r2 = await engine2.RunAsync(
            new DownloadOptions { Url = "x", Format = "mp4", OutputDir = outDir },
            null, cts.Token);
        Assert.True(r2.Cancelled);
    }
}

