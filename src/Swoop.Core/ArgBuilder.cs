namespace Swoop.Core;

/// <summary>
/// Builds yt-dlp argv from a <see cref="DownloadOptions"/>. All members are
/// emitted via ArgumentList callers â€” never string-concatenated.
/// </summary>
public static class ArgBuilder
{
    private static readonly string[] CommonFlags =
    [
        "--newline",
        "--no-warnings",
        "--embed-thumbnail",
        "--add-metadata",
    ];

    private static readonly string[] AudioOnlyFormats = ["mp3", "m4a", "aac", "wav", "flac", "ogg"];

    public static IReadOnlyList<string> Build(DownloadOptions o)
    {
        var args = new List<string>(CommonFlags)
        {
            "-o",
            System.IO.Path.Combine(o.OutputDir, "%(title)s [%(id)s].%(ext)s"),
        };

        if (o.Format == "mp3")
        {
            args.Add("-x");
            args.Add("--audio-format");
            args.Add("mp3");
            args.Add("--audio-quality");
            args.Add("0");
        }
        else if (AudioOnlyFormats.Contains(o.Format))
        {
            args.Add("-x");
            args.Add("--audio-format");
            args.Add(o.Format);
        }
        else
        {
            args.Add("-f");
            args.Add(VideoFilter(o.Format, o.MaxHeight));
            args.Add("--merge-output-format");
            args.Add(o.Format == "mov" ? "mp4" : o.Format);

            if (o.Format == "mov")
            {
                args.Add("--remux-video");
                args.Add("mov");
            }
        }

        args.Add(o.Url);
        return args;
    }

    private static string VideoFilter(string format, int? maxHeight)
    {
        var bound = maxHeight is null ? "" : $"[height<={maxHeight.Value}]";
        // webm containers only accept Vorbis/Opus audio â€” pairing m4a audio
        // into webm makes ffmpeg's merge fail, so the audio ext follows the
        // container for webm; every other container accepts m4a directly.
        var audioExt = format == "webm" ? "webm" : "m4a";
        return $"bestvideo[ext={format}]{bound}+bestaudio[ext={audioExt}]/best[ext={format}]{bound}/best";
    }
}

