using Swoop.Core;
using Xunit;

namespace Swoop.Core.Tests;

[Trait("Category", "E2E")]
public class EndToEndSmokes
{
    private const string Fixture = "https://www.youtube.com/watch?v=jNQXAC9IVRw"; // stable 19s public clip

    [Fact]
    public async Task Real_download_mp4_1080p()
    {
        var engine = new DownloadEngine(ResolveYtDlp());
        var outDir = NewDir("mp4");
        var r = await engine.RunAsync(new DownloadOptions
        {
            Url = Fixture, Format = "mp4", MaxHeight = 1080, OutputDir = outDir,
        }, null, CancellationToken.None);

        Assert.True(r.Success, r.Error);
        var file = r.OutputPath;
        Assert.NotNull(file);
        Assert.True(File.Exists(file!));
        Assert.True(new FileInfo(file!).Length > 100_000, "too small for a video clip");
        Assert.Equal(".mp4", Path.GetExtension(file!), ignoreCase: true);
    }

    [Fact]
    public async Task Real_download_mp3_transcode()
    {
        var engine = new DownloadEngine(ResolveYtDlp());
        var outDir = NewDir("mp3");
        var r = await engine.RunAsync(new DownloadOptions
        {
            Url = Fixture, Format = "mp3", MaxHeight = null, OutputDir = outDir,
        }, null, CancellationToken.None);

        Assert.True(r.Success, r.Error);
        var mp3 = r.OutputPath;
        Assert.NotNull(mp3);
        Assert.Equal(".mp3", Path.GetExtension(mp3!), ignoreCase: true);
    }

    [Fact]
    public async Task Garbage_url_fails_gracefully()
    {
        var engine = new DownloadEngine(ResolveYtDlp());
        var r = await engine.RunAsync(new DownloadOptions
        {
            Url = "https://not-a-real-site.invalid/video", Format = "mp4", OutputDir = NewDir("g"),
        }, null, CancellationToken.None);

        Assert.False(r.Success);
        Assert.False(r.Cancelled);
        // stderr tail must surface, no crash â€” Review Focus: garbage input drains the queue, not the loop
        Assert.False(string.IsNullOrWhiteSpace(r.Error));
    }

    private static string ResolveYtDlp()
    {
        var p = ToolLocator.Find("yt-dlp");
        Assert.True(p is not null, "yt-dlp missing on PATH / winget links");
        return p!;
    }

    private static string NewDir(string tag)
    {
        var d = Path.Combine(Path.GetTempPath(), "gg-e2e-" + tag + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }
}

