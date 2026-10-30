namespace Swoop.Core;

/// <summary>One queued download request.</summary>
public record DownloadOptions
{
    /// <summary>Source URL (YouTube / YT Music / SoundCloud / playlist).</summary>
    public required string Url { get; init; }

    /// <summary>
    /// Format token. Audio: mp3, m4a, aac, wav, flac, ogg. Video: mp4, mov, mkv, webm.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>Max video height in px; null = Best (no height bound). Ignored for audio formats.</summary>
    public int? MaxHeight { get; init; }

    /// <summary>Directory the output file goes into.</summary>
    public required string OutputDir { get; init; }
}

