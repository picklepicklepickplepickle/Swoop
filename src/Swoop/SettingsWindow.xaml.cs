using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Swoop;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = new AppSettings
        {
            DownloadFolder = settings.DownloadFolder,
            Theme = settings.Theme,
            BackgroundImagePath = settings.BackgroundImagePath,
        };
        ThemeCombo.SelectedIndex = _settings.Theme switch { "Aurora" => 1, "Ember" => 2, _ => 0 };
        FolderText.Text = _settings.DownloadFolder;
        BackgroundText.Text = _settings.BackgroundImagePath ?? "No image selected";
    }

    public AppSettings Settings => _settings;

    private void ChooseBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*",
            Title = "Choose a background image"
        };
        if (dialog.ShowDialog(this) is true)
        {
            _settings.BackgroundImagePath = dialog.FileName;
            BackgroundText.Text = dialog.FileName;
        }
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { InitialDirectory = Directory.Exists(_settings.DownloadFolder) ? _settings.DownloadFolder : null };
        if (dialog.ShowDialog(this) is true)
        {
            _settings.DownloadFolder = dialog.FolderName;
            FolderText.Text = dialog.FolderName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Theme = (ThemeCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Midnight";
        _settings.Save();
        DialogResult = true;
    }
}
