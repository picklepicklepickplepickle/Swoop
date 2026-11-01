using System.Diagnostics;
using System.Text.Json;

namespace Swoop.Core;

public sealed record MediaPreview(string Title, string Uploader, string? ThumbnailUrl, string? Duration, string Url);

public sealed class MediaPreviewService
{
    private readonly string _ytDlpPath;

    public MediaPreviewService(string ytDlpPath) => _ytDlpPath = ytDlpPath;

    public async Task<MediaPreview?> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("--dump-single-json");
        psi.ArgumentList.Add("--skip-download");
        psi.ArgumentList.Add("--no-playlist");
        psi.ArgumentList.Add("--no-warnings");
        psi.ArgumentList.Add(url);

        using var process = Process.Start(psi);
        if (process is null) return null;
        var json = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var duration = root.TryGetProperty("duration_string", out var durationValue)
                ? durationValue.GetString()
                : FormatDuration(root);
            return new MediaPreview(
                GetString(root, "title") ?? "Untitled media",
                GetString(root, "uploader") ?? GetString(root, "channel") ?? "Unknown creator",
                GetString(root, "thumbnail"),
                duration,
                url);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FormatDuration(JsonElement root)
    {
        if (!root.TryGetProperty("duration", out var value) || !value.TryGetDouble(out var seconds)) return null;
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"m\:ss");
    }
}
