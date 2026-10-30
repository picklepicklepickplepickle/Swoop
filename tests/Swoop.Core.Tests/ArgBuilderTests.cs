using Swoop.Core;
using Xunit;

namespace Swoop.Core.Tests;

public class ArgBuilderTests
{
    private static DownloadOptions Opt(string format, int? height = null, string? dir = null)
        => new()
        {
            Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            Format = format,
            MaxHeight = height,
            OutputDir = dir ?? @"C:\Users\boblo\Downloads"
        };

    private static IReadOnlyList<string> Build(string format, int? height = null, string? dir = null)
        => ArgBuilder.Build(Opt(format, height, dir));

    private static int Idx(IReadOnlyList<string> a, string s)
    {
        for (var i = 0; i < a.Count; i++) if (a[i] == s) return i;
        return -1;
    }

    [Fact]
    public void Build_mp3_extracts_audio_at_best_quality_no_height_filter()
    {
        var a = Build("mp3");
        Assert.Contains("-x", a);
        Assert.Contains("--audio-format", a);
        Assert.Contains("mp3", a);
        Assert.Contains("--audio-quality", a);
        Assert.Contains("0", a);
        Assert.DoesNotContain(a, s => s.Contains("[height<=", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("m4a", "m4a")]
    [InlineData("aac", "aac")]
    [InlineData("wav", "wav")]
    [InlineData("flac", "flac")]
    [InlineData("ogg", "ogg")]
    public void Build_audio_formats_transcode_via_x(string format, string fmt)
    {
        var a = Build(format);
        Assert.Contains("-x", a);
        Assert.Contains("--audio-format", a);
        Assert.Contains(fmt, a);
        Assert.DoesNotContain(a, s => s.Contains("[height<=", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("mp4")]
    [InlineData("mov")]
    [InlineData("mkv")]
    [InlineData("webm")]
    public void Build_video_format_1080p_uses_height_bound_and_merge(string format)
    {
        var a = Build(format, 1080);
        var fmtLine = a.Single(s => s.Contains("bestvideo[ext=", StringComparison.Ordinal));
        Assert.Contains("bestvideo[ext=" + format + "][height<=1080]+bestaudio", fmtLine, StringComparison.Ordinal);
        Assert.Contains("/best[ext=" + format + "][height<=1080]/best", fmtLine, StringComparison.Ordinal);
        Assert.Contains("--merge-output-format", a);
        Assert.Equal(format == "mov" ? "mp4" : format, a[Idx(a, "--merge-output-format") + 1]);
    }

    [Fact]
    public void Build_mov_adds_remux()
    {
        var a = Build("mov", 720);
        var remuxIdx = Idx(a, "--remux-video");
        Assert.True(remuxIdx >= 0);
        Assert.Equal("mov", a[remuxIdx + 1]);
    }

    [Fact]
    public void Build_Best_omits_height_filter()
    {
        var a = Build("mp4", null);
        var fmtLine = a.Single(s => s.Contains("bestvideo[ext=", StringComparison.Ordinal));
        Assert.Contains("bestvideo[ext=mp4]+bestaudio", fmtLine, StringComparison.Ordinal);
        Assert.Contains("/best[ext=mp4]/best", fmtLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_passes_output_dir_as_single_arg_verbatim_with_spaces()
    {
        var dir = @"C:\Users\boblo\My Videos\New folder (2)";
        var a = Build("mp4", dir: dir);
        var idx = Idx(a, "-o");
        Assert.True(idx >= 0);
        var template = a[idx + 1]; // a single list member containing everything, incl. the spacey dir
        Assert.Contains(dir, template, StringComparison.Ordinal);
        Assert.EndsWith("%(title)s [%(id)s].%(ext)s", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ends_with_url()
    {
        var a = Build("mp4");
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", a[^1]);
    }

    [Fact]
    public void Build_common_args_present_once_each()
    {
        var a = Build("mp4");
        foreach (var flag in new[] { "--newline", "--no-warnings", "--embed-thumbnail", "--add-metadata" })
            Assert.Equal(1, a.Count(s => s == flag));
    }
}


