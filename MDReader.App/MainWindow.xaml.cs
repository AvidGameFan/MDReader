using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace MDReader.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int MaxRecentFiles = 10;
    private static readonly string RecentFilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MDReader",
        "recent.json");

    private readonly string? _initialFilePath;
    private readonly List<string> _recentFiles = new();
    private string? _currentFilePath;
    private string? _currentMarkdown;
    private bool _isDarkTheme;
    private double _zoomFactor = 1.0;

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(string? initialFilePath)
    {
        _initialFilePath = initialFilePath;
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
        LoadRecentFiles();
        RefreshRecentFilesMenu();
        ApplyZoom();

        if (!string.IsNullOrWhiteSpace(_initialFilePath))
        {
            await LoadMarkdownFileAsync(_initialFilePath);
        }
        else
        {
            ShowWelcomePage();
        }
    }

    private async Task InitializeWebViewAsync()
    {
        await MarkdownView.EnsureCoreWebView2Async();
        MarkdownView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
    }

    private async Task LoadMarkdownFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                var message = $"File not found:\n{filePath}";
                SetStatus(message);
                MessageBox.Show(this, message, "MDReader", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var markdown = await File.ReadAllTextAsync(filePath);
            _currentMarkdown = markdown;
            _currentFilePath = filePath;
            RenderMarkdown(markdown);
            Title = $"MDReader — {Path.GetFileName(filePath)}";
            SetStatus(filePath);
            AddRecentFile(filePath);
        }
        catch (Exception ex)
        {
            var message = $"Error loading file:\n{ex.Message}";
            SetStatus(message);
            MessageBox.Show(this, message, "MDReader", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RenderMarkdown(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var htmlBody = Markdown.ToHtml(markdown, pipeline);
        var themeStyles = _isDarkTheme
            ? "body { background: #1e1e1e; color: #e6e6e6; } a { color: #4ea1ff; } code, pre { background: #2d2d2d; }"
            : "body { background: #ffffff; color: #1b1b1b; } a { color: #0067c0; } code, pre { background: #f5f5f5; }";

        var html = $@"<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 24px; line-height: 1.6; }}
    code, pre {{ font-family: Consolas, 'Cascadia Code', monospace; }}
    pre {{ padding: 12px; overflow-x: auto; }}
    img {{ max-width: 100%; }}
    {themeStyles}
  </style>
</head>
<body>
{htmlBody}
</body>
</html>";

        MarkdownView.NavigateToString(html);
    }

    private void ShowWelcomePage()
    {
        var themeStyles = _isDarkTheme
            ? "body { background: #1e1e1e; color: #e6e6e6; } code { background: #2d2d2d; } a { color: #4ea1ff; }"
            : "body { background: #ffffff; color: #1b1b1b; } code { background: #f5f5f5; } a { color: #0067c0; }";

        var html = $@"<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 24px; line-height: 1.6; }}
    code {{ font-family: Consolas, 'Cascadia Code', monospace; padding: 2px 4px; }}
    {themeStyles}
  </style>
</head>
<body>
  <h2>MDReader</h2>
  <p>Open a markdown file using <b>File → Open</b>, drag-and-drop, or pass a file path on the command line.</p>
  <p>Examples:</p>
  <ul>
    <li>Drag a <code>.md</code> file onto the window</li>
    <li><code>MDReader.App.exe C:\path\to\file.md</code></li>
  </ul>
</body>
</html>";

        MarkdownView.NavigateToString(html);
        SetStatus("Ready");
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                try
                {
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
                catch
                {
                    SetStatus($"Unable to open link: {uri.AbsoluteUri}");
                }
            }
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                await LoadMarkdownFileAsync(files[0]);
            }
        }
    }

    private async void OpenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Markdown File",
            Filter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await LoadMarkdownFileAsync(dialog.FileName);
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DarkModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = DarkModeMenuItem.IsChecked;
        if (!string.IsNullOrWhiteSpace(_currentMarkdown))
        {
            RenderMarkdown(_currentMarkdown);
        }
        else
        {
            ShowWelcomePage();
        }
    }

    private void ZoomInMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _zoomFactor = Math.Min(3.0, _zoomFactor + 0.1);
        ApplyZoom();
    }

    private void ZoomOutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _zoomFactor = Math.Max(0.5, _zoomFactor - 0.1);
        ApplyZoom();
    }

    private void ZoomResetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _zoomFactor = 1.0;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        MarkdownView.ZoomFactor = _zoomFactor;
        SetStatus(_currentFilePath ?? "Ready");
    }

    private void RecentFilesMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        RefreshRecentFilesMenu();
    }

    private void RefreshRecentFilesMenu()
    {
        RecentFilesMenu.Items.Clear();

        if (_recentFiles.Count == 0)
        {
            RecentFilesMenu.Items.Add(new MenuItem { Header = "(No recent files)", IsEnabled = false });
            return;
        }

        foreach (var file in _recentFiles)
        {
            var item = new MenuItem { Header = file, Tag = file };
            item.Click += RecentFileItem_Click;
            RecentFilesMenu.Items.Add(item);
        }

        RecentFilesMenu.Items.Add(new Separator());
        var clearItem = new MenuItem { Header = "Clear Recent" };
        clearItem.Click += ClearRecentMenuItem_Click;
        RecentFilesMenu.Items.Add(clearItem);
    }

    private void RecentFileItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string filePath)
        {
            _ = LoadMarkdownFileAsync(filePath);
        }
    }

    private void ClearRecentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _recentFiles.Clear();
        SaveRecentFiles();
        RefreshRecentFilesMenu();
    }

    private void AddRecentFile(string filePath)
    {
        _recentFiles.RemoveAll(f => string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));
        _recentFiles.Insert(0, filePath);

        if (_recentFiles.Count > MaxRecentFiles)
        {
            _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
        }

        SaveRecentFiles();
        RefreshRecentFilesMenu();
    }

    private void LoadRecentFiles()
    {
        try
        {
            if (!File.Exists(RecentFilesPath))
            {
                return;
            }

            var json = File.ReadAllText(RecentFilesPath);
            var files = JsonSerializer.Deserialize<List<string>>(json);
            if (files is { Count: > 0 })
            {
                _recentFiles.Clear();
                _recentFiles.AddRange(files);
            }
        }
        catch
        {
            // Ignore recent files load errors
        }
    }

    private void SaveRecentFiles()
    {
        try
        {
            var folder = Path.GetDirectoryName(RecentFilesPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonSerializer.Serialize(_recentFiles);
            File.WriteAllText(RecentFilesPath, json);
        }
        catch
        {
            // Ignore recent files save errors
        }
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
    }
}