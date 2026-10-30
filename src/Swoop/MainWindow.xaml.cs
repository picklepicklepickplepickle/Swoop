using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Swoop.Core;
using Microsoft.Win32;

namespace Swoop;

public sealed class QueueItemVm : INotifyPropertyChanged
{
    private string _state = "Queued";
    private double _percent;
    private Brush _dot = new SolidColorBrush(Color.FromRgb(0x7A,0x7A,0x8A));
    private Visibility _cancelVisib = Visibility.Collapsed;
    private string _tooltip = "";
    public required string Label { get; init; }
    public required string Url { get; init; }
    public string Tooltip { get => _tooltip; set { _tooltip = value; Raise(); } }
    public string State { get => _state; set { _state = value; Raise(); } }
    public double Percent { get => _percent; set { _percent = value; Raise(); } }
    public Brush Dot { get => _dot; set { _dot = value; Raise(); } }
    public Visibility CancelVisib { get => _cancelVisib; set { _cancelVisib = value; Raise(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n!));
}

public partial class MainWindow : Window
{
    private static readonly string[] AudioFormats = ["mp3", "m4a", "aac", "wav", "flac", "ogg"];

    private readonly ObservableCollection<QueueItemVm> _queue = [];
    private CancellationTokenSource? _cts;
    private bool _engineBusy;

    private static readonly (string Token, string Display)[] Formats =
    [
        ("mp4",  "mp4 (video, standard)"),
        ("mp3",  "mp3 (audio, 320 kbps)"),
        ("mov",  "mov / QuickTime"),
        ("m4a",  "m4a (audio)"),
        ("wav",  "wav (audio)"),
        ("flac", "flac (audio, lossless)"),
        ("aac",  "aac (audio)"),
        ("ogg",  "ogg (audio)"),
        ("mkv",  "mkv (video)"),
        ("webm", "webm (video)"),
    ];

    private static readonly (int? Height, string Display)[] Resolutions =
    [
        (null,  "Best available"),
        (2160,  "2160p (4K)"),
        (1440,  "1440p (QHD)"),
        (1080,  "1080p (Full HD)"),
        (720,   "720p (HD)"),
        (480,   "480p"),
        (360,   "360p"),
    ];

    public MainWindow()
    {
        InitializeComponent();
        FolderBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
        foreach (var f in Formats) FormatCombo.Items.Add(f.Display);
        foreach (var r in Resolutions) ResCombo.Items.Add(r.Display);
        FormatCombo.SelectedIndex = 0;
        ResCombo.SelectedIndex = 3; // 1080p default
        QueueList.ItemsSource = _queue;
        Loaded += async (_, _) => await EnsureToolsAsync();
        // Closing mid-download: cancel, give the engine a bounded moment to
        // kill the process tree, then close — never orphan yt-dlp
        Closing += async (s, e) =>
        {
            if (!_engineBusy) return;
            e.Cancel = true;
            _cts?.Cancel();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (_engineBusy && DateTime.UtcNow < deadline) await Task.Delay(50);
            Close();
        };
    }

    private string FormatToken => Formats[Math.Max(0, FormatCombo.SelectedIndex)].Token;
    private int? ResHeight => AudioFormats.Contains(FormatToken) ? null : Resolutions[Math.Max(0, ResCombo.SelectedIndex)].Height;

    // ---------- tool resolution ----------

    private string? _ytDlpPath;
    private string? _ffmpegPath;

    private async Task EnsureToolsAsync()
    {
        _ytDlpPath = ToolLocator.Find("yt-dlp");
        _ffmpegPath = ToolLocator.Find("ffmpeg");

        if (_ytDlpPath is null)
        {
            StatusText.Text = "yt-dlp not found — downloading it once to %LOCALAPPDATA%\\Swoop …";
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Swoop");
                _ytDlpPath = await ToolLocator.AutoFetchYtDlpAsync(dir);
            }
            catch (Exception e) { StatusText.Text = e.Message; return; }
        }

        StatusText.Text = _ffmpegPath is null
            ? "yt-dlp ready. ffmpeg not found — audio transcodes (mp3 etc.) will fail."
            : "Ready — yt-dlp and ffmpeg found.";

        // first-run race: items queued while the fetch was running must start now
        if (_queue.Any(q => q.State is "Queued")) _ = RunQueueAsync();
    }

    // ---------- paste input ----------

    private void LinkBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Return)
            Add_LinkBox();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Add_LinkBox();

    private void Add_LinkBox()
    {
        var text = LinkBox.Text;
        if (string.IsNullOrWhiteSpace(text)) return;
        var lines = text.Split(["\r\n", "\r", "\n", ";", " "], StringSplitOptions.RemoveEmptyEntries);
        var links = DragHelper.ExtractLinks(lines);
        if (links.Count == 0)
        {
            StatusText.Text = "No valid link found in that text.";
            return;
        }
        LinkBox.Clear();
        Enqueue(links);
    }

    // ---------- drag & drop ----------

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x6C,0xC0,0x6C));
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.StringFormat)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
        => DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A,0x3A,0x46));

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A,0x3A,0x46));
        e.Handled = true;

        var lines = new List<string>();
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            foreach (var f in files)
            {
                if (Directory.Exists(f)) continue;
                if (string.Equals(Path.GetExtension(f), ".txt", StringComparison.OrdinalIgnoreCase))
                {
                    try { lines.AddRange(File.ReadAllLines(f)); } catch { /* unreadable — skip */ }
                }
                else lines.Add(f);
            }
        }
        string raw = e.Data.GetData(DataFormats.StringFormat) as string ?? "";
        lines.AddRange(raw.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries));

        Enqueue(DragHelper.ExtractLinks(lines));
    }

    // ---------- queue ----------

    private void Enqueue(IReadOnlyList<string> urls)
    {
        foreach (var u in urls)
        {
            _queue.Add(new QueueItemVm
            {
                Label = u.Length > 80 ? u[..80] + "…" : u,
                Url = u,
                CancelVisib = Visibility.Visible, // queued rows pop on ✕
            });
        }
        _ = RunQueueAsync();
    }

    private async Task RunQueueAsync()
    {
        if (_engineBusy) return;
        _engineBusy = true;
        try
        {
            if (_ytDlpPath is null)
            {
                StatusText.Text = "yt-dlp unavailable — cannot download.";
                return;
            }
            var engine = new DownloadEngine(_ytDlpPath);

            while (true)
            {
                var next = _queue.FirstOrDefault(q => q.State == "Queued");
                if (next is null) break;

                _cts ??= new CancellationTokenSource();
                var ct = _cts.Token;
                next.CancelVisib = Visibility.Visible;
                next.State = "Working";
                next.Dot = new SolidColorBrush(Color.FromRgb(0xE8,0xC0,0x50));
                StatusText.Text = $"Downloading: {next.Url}";

                var opts = new DownloadOptions
                {
                    Url = next.Url,
                    Format = FormatToken,
                    MaxHeight = ResHeight,
                    OutputDir = FolderBox.Text,
                };

                if (_ffmpegPath is null && AudioFormats.Contains(opts.Format))
                {
                    Finish(next, "Failed", Colors.Red, $"ffmpeg missing — cannot transcode to {opts.Format}.");
                    continue;
                }

                var progress = new Progress<DownloadProgress>(p =>
                {
                    next.Tooltip = p.Line;
                    if (p.Percent is { } pc) next.Percent = Math.Min(pc, 100);
                });

                var r = await engine.RunAsync(opts, progress, ct);

                _cts.Dispose();
                _cts = null;
                next.CancelVisib = Visibility.Collapsed;

                if (r.Cancelled) Finish(next, "Cancelled", Colors.DarkOrange, "Cancelled");
                else if (r.Success) { next.Percent = 100; Finish(next, "Done", Colors.LimeGreen, r.OutputPath ?? "OK"); }
                else Finish(next, "Failed", Colors.Red, r.Error);
            }
            StatusText.Text = "Queue empty.";
        }
        finally { _engineBusy = false; }
    }

    private static void Finish(QueueItemVm q, string state, Color dot, string tip)
    {
        q.State = state;
        q.Dot = new SolidColorBrush(dot);
        q.Tooltip = tip;
    }

    // ---------- controls ----------

    private void Format_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ResCombo is null || ResLabel is null || ResLabelCol is null) return;
        var audio = AudioFormats.Contains(FormatToken);
        ResCombo.IsEnabled = !audio;
        ResLabel.Opacity = audio ? 0.4 : 1.0;
        if (audio) ResCombo.SelectedIndex = 0;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Choose download folder",
            InitialDirectory = Directory.Exists(FolderBox.Text) ? FolderBox.Text : null,
        };
        if (dlg.ShowDialog(this) is true) FolderBox.Text = dlg.FolderName;
    }

    private void CancelRow_Click(object sender, RoutedEventArgs e)
    {
        if (e.Source is not System.Windows.Controls.Button b || b.Tag is not QueueItemVm row) return;
        if (row.State == "Queued") _queue.Remove(row);
        else _cts?.Cancel();
    }

    private void CancelAll_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        for (int i = _queue.Count - 1; i >= 0; i--)
            if (_queue[i].State is "Done" or "Failed" or "Cancelled") _queue.RemoveAt(i);
    }
}


