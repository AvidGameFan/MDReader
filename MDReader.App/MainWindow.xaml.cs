using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Markdig;
using Microsoft.Web.WebView2.Core;

namespace MDReader.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly string? _initialFilePath;

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
                SetStatus($"File not found: {filePath}");
                return;
            }

            var markdown = await File.ReadAllTextAsync(filePath);
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var htmlBody = Markdown.ToHtml(markdown, pipeline);

            var html = $@"<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 24px; line-height: 1.6; color: #1b1b1b; }}
    code, pre {{ font-family: Consolas, 'Cascadia Code', monospace; background: #f5f5f5; }}
    pre {{ padding: 12px; overflow-x: auto; }}
    a {{ color: #0067c0; }}
    img {{ max-width: 100%; }}
  </style>
</head>
<body>
{htmlBody}
</body>
</html>";

            MarkdownView.NavigateToString(html);
            Title = $"MDReader — {Path.GetFileName(filePath)}";
            SetStatus(filePath);
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading file: {ex.Message}");
        }
    }

        private void ShowWelcomePage()
        {
                var html = @"<!doctype html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <style>
        body { font-family: 'Segoe UI', Arial, sans-serif; margin: 24px; line-height: 1.6; color: #1b1b1b; }
        code { font-family: Consolas, 'Cascadia Code', monospace; background: #f5f5f5; padding: 2px 4px; }
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

    private void SetStatus(string text)
    {
        StatusText.Text = text;
    }
}