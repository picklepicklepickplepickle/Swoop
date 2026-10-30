namespace Swoop.Core;

public sealed record DownloadProgress(double? Percent, string Line);

public sealed record DownloadResult(string? OutputPath, string Error, bool Success, bool Cancelled);

/// <summary>
/// Drives one yt-dlp process per <see cref="DownloadOptions"/>; queueing is
/// strictly sequential (internal semaphore). Cancel kills the whole process
/// tree. stdout drives progress parsing; stderr is captured for errors.
/// </summary>
public sealed class DownloadEngine
{
    private readonly string _toolPath;
    private readonly Func<DownloadOptions, IReadOnlyList<string>> _argFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DownloadEngine(string ytDlpPath, Func<DownloadOptions, IReadOnlyList<string>>? argFactory = null)
    {
        _toolPath = ytDlpPath;
        _argFactory = argFactory ?? ArgBuilder.Build;
    }

    public async Task<DownloadResult> RunAsync(
        DownloadOptions options,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            System.Diagnostics.Process? proc;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _toolPath,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = options.OutputDir,
                };
                foreach (var a in _argFactory(options)) psi.ArgumentList.Add(a);
                Directory.CreateDirectory(options.OutputDir);
                proc = System.Diagnostics.Process.Start(psi);
            }
            catch (Exception startEx)
            {
                // start/mkdir failure returns a graded-failure result â€” the
                // queue loop must keep draining (never strand it in "Working")
                return new DownloadResult(null, $"{startEx.GetType().Name}: {startEx.Message}", false, false);
            }

            using (proc)
            {
                if (proc is null)
                    return new DownloadResult(null, $"Process.Start returned null for {_toolPath}", false, false);

                string? outputPath = null;
                var errorTail = new List<string>(32);

                var stdout = Consume(proc.StandardOutput, line =>
                {
                    // last Destination wins: post-extract/remux target is the real output
                    const string marker = "Destination: ";
                    var i = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (i >= 0)
                        outputPath = line[(i + marker.Length)..].Trim().ToString();
                    var p = ProgressParser.Parse(line);
                    if (p is not null || !string.IsNullOrWhiteSpace(line))
                        progress?.Report(new DownloadProgress(p, line));
                });

                var stderr = Consume(proc.StandardError, line =>
                {
                    if (errorTail.Count < 32) errorTail.Add(line);
                    progress?.Report(new DownloadProgress(null, line));
                });

                bool cancelled = false;
                try
                {
                    // wait for processes, then for pipes; cancellation kills tree first
                    await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    TryKillTree(proc);
                    // let pipes drain so awaiters complete without hanging
                    await Task.WhenAny(stdout, stderr).ConfigureAwait(false);
                }

                var exit = cancelled ? -1 : SafeExitCode(proc);

                // Destination lines reference intermediates yt-dlp deletes after
                // merging â€” verify existence, else newest surviving file.
                if (!cancelled && exit == 0 && (outputPath is null || !File.Exists(outputPath)))
                    outputPath = NewestFile(options.OutputDir);
                var success = !cancelled && exit == 0 && outputPath is not null && File.Exists(outputPath);

                return new DownloadResult(
                    exit == 0 ? outputPath : null,
                    cancelled ? "Cancelled."
                        : errorTail.Count > 0 ? string.Join('\n', errorTail.TakeLast(8))
                        : exit == 0 ? "" : $"yt-dlp exited with code {exit}.",
                    success,
                    cancelled);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task Consume(System.IO.StreamReader reader, Action<string> onLine)
    {
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            if (line.Length > 0) onLine(line);
        }
    }

    private static int SafeExitCode(System.Diagnostics.Process p)
    {
        try { return p.ExitCode; } catch { return -1; }
    }

    private static void TryKillTree(System.Diagnostics.Process p)
    {
        try { p.Kill(entireProcessTree: true); } catch { /* already dead */ }
    }

    private static string? NewestFile(string dir)
    {
        try
        {
            return new DirectoryInfo(dir).GetFiles()
                .Where(f => !f.Name.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch { return null; }
    }
}

