using System;
//using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
//using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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
    private bool _showToc = true;
    private bool _liveReloadEnabled = true;
    private double _zoomFactor = 1.0;
    private FileSystemWatcher? _fileWatcher;
    private DispatcherTimer? _reloadTimer;
    private string? _pendingReloadPath;
    private readonly SearchState _searchState = new();
    private readonly AppSettings _settings = AppSettings.Load();
    private bool _isEditMode;
    private bool _hasShownEditRoundTripWarning;

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(string? initialFilePath)
    {
        _initialFilePath = initialFilePath;
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!await EnsurePendingEditChangesHandledAsync("exiting"))
        {
            e.Cancel = true;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
        LoadRecentFiles();
        RefreshRecentFilesMenu();
        ApplyZoom();
        LiveReloadMenuItem.IsChecked = _liveReloadEnabled;
        TocMenuItem.IsChecked = _showToc;
        HardLineBreaksMenuItem.IsChecked = _settings.SoftLineBreaksAsHard;
        IgnoreEditRoundTripWarningMenuItem.IsChecked = _settings.IgnoreEditRoundTripWarning;
        EditModeMenuItem.IsChecked = _isEditMode;

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
        var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
        MarkdownView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app",
            assetsPath,
            CoreWebView2HostResourceAccessKind.Allow);
        MarkdownView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
        MarkdownView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
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
            _hasShownEditRoundTripWarning = false;
            RenderMarkdown(markdown);
            Title = $"MDReader — {Path.GetFileName(filePath)}";
            SetStatus(filePath);
            AddRecentFile(filePath);
            SetupFileWatcher(filePath);
        }
        catch (Exception ex)
        {
            var message = $"Error loading file:\n{ex.Message}";
            SetStatus(message);
            MessageBox.Show(this, message, "MDReader", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public Task LoadFileFromExternal(string filePath)
    {
        return LoadMarkdownFileAsync(filePath);
    }

    private void RenderMarkdown(string markdown)
    {
        if (_isEditMode)
        {
            RenderEditor(markdown);
            return;
        }

        var pipelineBuilder = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseTaskLists();
        if (_settings.SoftLineBreaksAsHard)
        {
            pipelineBuilder = pipelineBuilder.UseSoftlineBreakAsHardlineBreak();
        }
        var pipeline = pipelineBuilder.Build();
        var htmlBody = Markdown.ToHtml(markdown, pipeline);
        var themeStyles = _isDarkTheme
            ? "body { background: #1e1e1e; color: #e6e6e6; } a { color: #4ea1ff; } code, pre { background: #2d2d2d; }"
            : "body { background: #ffffff; color: #1b1b1b; } a { color: #0067c0; } code, pre { background: #f5f5f5; }";
                var tocHtml = _showToc ? "<nav id=\"toc\"><strong>Contents</strong></nav>" : string.Empty;
                var tocScript = @"
<script>
(function() {
    const toc = document.getElementById('toc');
    if (!toc) return;
    const headings = document.querySelectorAll('h1, h2, h3');
    if (!headings.length) { toc.style.display = 'none'; return; }

    const slugify = (text) => text.toLowerCase()
        .replace(/[^a-z0-9\s-]/g, '')
        .trim()
        .replace(/\s+/g, '-')
        .substring(0, 80);

    const list = document.createElement('ul');
    headings.forEach(h => {
        if (!h.id) { h.id = slugify(h.textContent || ''); }
        const li = document.createElement('li');
        li.className = 'toc-' + h.tagName.toLowerCase();
        const a = document.createElement('a');
        a.textContent = h.textContent || '';
        a.href = '#' + h.id;
        li.appendChild(a);
        list.appendChild(li);
    });
    toc.appendChild(list);
})();
</script>";
        var taskScript = @"
<script>
(function() {
    var root = document.body;
    if (!root) {
        return;
    }

    var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
    var textNodes = [];
    var current;
    while ((current = walker.nextNode())) {
        if (!current.nodeValue || !/\[(?: |x|X)\]/.test(current.nodeValue)) {
            continue;
        }

        var parent = current.parentElement;
        if (!parent) {
            continue;
        }

        var tag = parent.tagName;
        if (tag === 'CODE' || tag === 'PRE' || tag === 'SCRIPT' || tag === 'STYLE') {
            continue;
        }

        textNodes.push(current);
    }

    textNodes.forEach(function(textNode) {
        var text = textNode.nodeValue || '';
        var regex = /\[( |x|X)\]/g;
        var lastIndex = 0;
        var match;
        var fragment = document.createDocumentFragment();
        var changed = false;

        while ((match = regex.exec(text)) !== null) {
            changed = true;
            var before = text.substring(lastIndex, match.index);
            if (before.length > 0) {
                fragment.appendChild(document.createTextNode(before));
            }

            var checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.checked = match[1].toLowerCase() === 'x';
            checkbox.disabled = true;
            fragment.appendChild(checkbox);

            lastIndex = match.index + match[0].length;
        }

        if (!changed) {
            return;
        }

        var after = text.substring(lastIndex);
        if (after.length > 0) {
            fragment.appendChild(document.createTextNode(after));
        }

        textNode.parentNode.replaceChild(fragment, textNode);
    });

    root.querySelectorAll('input[type=checkbox]').forEach(function(cb) {
        cb.disabled = true;
    });
})();
</script>";

        var html = $@"<!doctype html>
<html>
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <style>
    body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 24px; line-height: 1.6; }}
    code, pre {{ font-family: Consolas, 'Cascadia Code', monospace; }}
    input[type='checkbox'] {{ pointer-events: none; }}
    pre {{ padding: 12px; overflow-x: auto; }}
    img {{ max-width: 100%; }}
        #toc {{ padding: 12px 16px; margin-bottom: 16px; border: 1px solid #ddd; border-radius: 6px; }}
        #toc ul {{ margin: 8px 0 0 16px; padding: 0; }}
        #toc li {{ margin: 4px 0; }}
        #toc .toc-h2 {{ margin-left: 12px; }}
        #toc .toc-h3 {{ margin-left: 24px; }}
    {themeStyles}
  </style>
</head>
<body>
{tocHtml}
{htmlBody}
{tocScript}
{taskScript}
</body>
</html>";

        MarkdownView.NavigateToString(html);
    }

        private void RenderEditor(string markdown)
        {
                var pipelineBuilder = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseTaskLists();
                if (_settings.SoftLineBreaksAsHard)
                {
                        pipelineBuilder = pipelineBuilder.UseSoftlineBreakAsHardlineBreak();
                }
                var pipeline = pipelineBuilder.Build();
                var htmlBody = Markdown.ToHtml(markdown, pipeline);
                var themeStyles = _isDarkTheme
                        ? "body { background: #1e1e1e; color: #e6e6e6; } a { color: #4ea1ff; } code, pre { background: #2d2d2d; }"
                        : "body { background: #ffffff; color: #1b1b1b; } a { color: #0067c0; } code, pre { background: #f5f5f5; }";

                var softLineBreaksAsHard = _settings.SoftLineBreaksAsHard ? "true" : "false";
                var script = @"<script>
(function() {
    var editor = document.getElementById('editor');

    var upgradeTaskTokens = function(root) {
        if (!root) {
            return;
        }

        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
        var textNodes = [];
        var current;
        while ((current = walker.nextNode())) {
            if (!current.nodeValue || !/\[(?: |x|X)\]/.test(current.nodeValue)) {
                continue;
            }

            var parent = current.parentElement;
            if (!parent) {
                continue;
            }

            var tag = parent.tagName;
            if (tag === 'CODE' || tag === 'PRE' || tag === 'SCRIPT' || tag === 'STYLE') {
                continue;
            }

            textNodes.push(current);
        }

        textNodes.forEach(function(textNode) {
            var text = textNode.nodeValue || '';
            var regex = /\[( |x|X)\]/g;
            var lastIndex = 0;
            var match;
            var fragment = document.createDocumentFragment();
            var changed = false;

            while ((match = regex.exec(text)) !== null) {
                changed = true;
                var before = text.substring(lastIndex, match.index);
                if (before.length > 0) {
                    fragment.appendChild(document.createTextNode(before));
                }

                var checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.checked = match[1].toLowerCase() === 'x';
                fragment.appendChild(checkbox);

                lastIndex = match.index + match[0].length;
            }

            if (!changed) {
                return;
            }

            var after = text.substring(lastIndex);
            if (after.length > 0) {
                fragment.appendChild(document.createTextNode(after));
            }

            textNode.parentNode.replaceChild(fragment, textNode);
        });
    };

    var normalizeCheckboxes = function(root) {
        if (!root) {
            return;
        }

        root.querySelectorAll('input[type=checkbox]').forEach(function(cb) {
            cb.disabled = false;
            cb.removeAttribute('disabled');
            cb.setAttribute('contenteditable', 'false');
        });
    };

    if (editor) {
        upgradeTaskTokens(editor);
        normalizeCheckboxes(editor);

        var syncCheckbox = function(e) {
            var target = e.target;
            if (!(target instanceof HTMLInputElement) || target.type !== 'checkbox') {
                return;
            }

            if (target.checked) {
                target.setAttribute('checked', 'checked');
            } else {
                target.removeAttribute('checked');
            }
        };

        editor.addEventListener('change', syncCheckbox);
    }

    window.mdreader_insertCheckbox = function() {
        document.execCommand('insertHTML', false, '<input type=""checkbox"" /> ');
    };

    window.mdreader_getMarkdown = function() {
        if (!editor) {
            return '';
        }
        if (!window.TurndownService) {
            return '__TURNDOWN_MISSING__';
        }

        var turndownService = new TurndownService({ headingStyle: 'atx', bulletListMarker: '-', codeBlockStyle: 'fenced', hr: '---' });
        turndownService.addRule('checkbox', {
            filter: function(node) {
                return node.nodeName === 'INPUT' && node.type === 'checkbox';
            },
                replacement: function(content, node) {
                    var checked = typeof node.checked === 'boolean'
                        ? node.checked
                        : (typeof node.getAttribute === 'function' && node.getAttribute('checked') !== null);
                    return (checked ? '[x]' : '[ ]');
                }
            });
            turndownService.addRule('table', {
                filter: 'table',
                replacement: function (content, node) {
                    var rows = node.querySelectorAll('tr');
                    if (rows.length === 0) return '';
                    var markdownRows = [];
                    var headerSeparatorNeeded = false;
                    Array.prototype.forEach.call(rows, function (row) {
                        var cells = row.querySelectorAll('th, td');
                        var cellContents = [];
                        Array.prototype.forEach.call(cells, function (cell) {
                            var cellText = turndownService.turndown(cell).trim();
                            cellText = cellText.replace(/\|/g, '\\|').replace(/\n+/g, ' ');
                            cellContents.push(' ' + cellText + ' ');
                        });
                        markdownRows.push('|' + cellContents.join('|') + '|');
                    });
                    if (markdownRows.length > 1) {
                        var firstRowHasTH = node.querySelector('tr th') !== null;
                        headerSeparatorNeeded = firstRowHasTH || node.querySelector('thead') !== null;
                    }
                    if (headerSeparatorNeeded && markdownRows.length > 1) {
                        var colCount = markdownRows[0].split('|').filter(function (c) { return c.trim() !== ''; }).length;
                        var sepParts = [];
                        for (var i = 0; i < colCount; i++) { sepParts.push(' --- '); }
                        markdownRows.splice(1, 0, '|' + sepParts.join('|') + '|');
                    }
                    return '\n\n' + markdownRows.join('\n') + '\n\n';
                }
            });
            if (__HARDLINEBREAKS__) {
                turndownService.addRule('linebreak', {
                    filter: 'br',
                    replacement: function() {
                        return '\n';
                    }
                });
            }

        editor.querySelectorAll('input[type=checkbox]').forEach(function(cb) {
            if (cb.checked) {
                cb.setAttribute('checked', 'checked');
            } else {
                cb.removeAttribute('checked');
            }
        });

        var markdown = turndownService.turndown(editor);
        markdown = markdown.replace(/^\-\s{2,}/gm, '- ');
        return markdown;
    };
})();
</script>";
                script = script.Replace("__HARDLINEBREAKS__", softLineBreaksAsHard);

                var html = $@"<!doctype html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 0; line-height: 1.6; }}
        #toolbar {{ padding: 8px 12px; border-bottom: 1px solid #ccc; position: sticky; top: 0; background: inherit; }}
        #toolbar button {{ margin-right: 6px; }}
        #editor {{ padding: 16px 24px; outline: none; min-height: calc(100vh - 60px); }}
        code, pre {{ font-family: Consolas, 'Cascadia Code', monospace; }}
        pre {{ padding: 12px; overflow-x: auto; }}
        img {{ max-width: 100%; }}
        {themeStyles}
    </style>
</head>
<body>
    <div id=""toolbar"">
        <button onclick=""document.execCommand('bold')"">B</button>
        <button onclick=""document.execCommand('italic')"">I</button>
        <button onclick=""document.execCommand('insertUnorderedList')"">• List</button>
        <button onclick=""document.execCommand('insertOrderedList')"">1. List</button>
        <button onclick=""document.execCommand('formatBlock', false, 'h1')"">H1</button>
        <button onclick=""document.execCommand('formatBlock', false, 'h2')"">H2</button>
        <button onclick=""document.execCommand('formatBlock', false, 'h3')"">H3</button>
        <button onclick=""var url=prompt('Link URL'); if(url) document.execCommand('createLink', false, url);"">Link</button>
        <button onclick=""document.execCommand('formatBlock', false, 'pre')"">Code</button>
        <button onclick=""window.mdreader_insertCheckbox && window.mdreader_insertCheckbox()"">☐ Task</button>
    </div>
    <div id=""editor"" contenteditable=""true"">{htmlBody}</div>
    <script src=""https://app/turndown.js""></script>
    {script}
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
        if (TryHandleExternalNavigation(e.Uri))
        {
            e.Cancel = true;
        }
    }

    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (TryHandleExternalNavigation(e.Uri))
        {
            e.Handled = true;
        }
    }

    private bool TryHandleExternalNavigation(string? uriText)
    {
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.IsFile)
        {
            var filePath = uri.LocalPath;
            if (File.Exists(filePath))
            {
                _ = LoadMarkdownFileAsync(filePath);
                return true;
            }
        }


        if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
            uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryOpenInDefaultBrowser(uri))
            {
                SetStatus($"Unable to open link: {uri.AbsoluteUri}");
            }
            return true;
        }

        return false;
    }



    private bool TryOpenInDefaultBrowser(Uri uri)
    {
        try
        {
            var browserExe = GetAssociatedExecutableForProtocol("https");
            if (string.IsNullOrWhiteSpace(browserExe))
            {
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = browserExe,
                UseShellExecute = false
            };
            psi.ArgumentList.Add(uri.AbsoluteUri); // no command-line concatenation

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetAssociatedExecutableForProtocol(string protocol)
    {
        uint length = 0;
        _ = AssocQueryString(AssocF.None, AssocStr.Executable, protocol, null, null, ref length);
        if (length == 0)
        {
            return null;
        }

        var sb = new StringBuilder((int)length);
        var hr = AssocQueryString(AssocF.None, AssocStr.Executable, protocol, null, sb, ref length);
        return hr == 0 ? sb.ToString() : null;
    }

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint AssocQueryString(
        AssocF flags,
        AssocStr str,
        string pszAssoc,
        string? pszExtra,
        StringBuilder? pszOut,
        ref uint pcchOut);

    private enum AssocF : uint
    {
        None = 0
    }

    private enum AssocStr
    {
        Executable = 2
    }

    

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        HandleFileDragOver(e);
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        await HandleFileDropAsync(e);
    }

    private void MarkdownView_DragOver(object sender, DragEventArgs e)
    {
        HandleFileDragOver(e);
    }

    private async void MarkdownView_Drop(object sender, DragEventArgs e)
    {
        await HandleFileDropAsync(e);
    }

    private void HandleFileDragOver(DragEventArgs e)
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

    private async Task HandleFileDropAsync(DragEventArgs e)
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

    private async void NewMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsurePendingEditChangesHandledAsync("creating a new file"))
        {
            return;
        }

        _fileWatcher?.Dispose();
        _fileWatcher = null;
        _pendingReloadPath = null;

        _currentFilePath = null;
        _currentMarkdown = string.Empty;
        _hasShownEditRoundTripWarning = false;

        _isEditMode = true;
        EditModeMenuItem.IsChecked = true;

        Title = "MDReader - Untitled";
        SetStatus("New file (unsaved)");
        RenderMarkdown(_currentMarkdown);
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

    private void HardLineBreaksMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _settings.SoftLineBreaksAsHard = HardLineBreaksMenuItem.IsChecked;
        _settings.Save();

        if (!string.IsNullOrWhiteSpace(_currentMarkdown))
        {
            RenderMarkdown(_currentMarkdown);
        }
        else
        {
            ShowWelcomePage();
        }
    }

    private void IgnoreEditRoundTripWarningMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _settings.IgnoreEditRoundTripWarning = IgnoreEditRoundTripWarningMenuItem.IsChecked;
        _settings.Save();
    }

    private void LiveReloadMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _liveReloadEnabled = LiveReloadMenuItem.IsChecked;
        if (_liveReloadEnabled && !string.IsNullOrWhiteSpace(_currentFilePath))
        {
            SetupFileWatcher(_currentFilePath);
        }
        else
        {
            _fileWatcher?.Dispose();
            _fileWatcher = null;
        }
    }

    private void TocMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _showToc = TocMenuItem.IsChecked;
        if (!string.IsNullOrWhiteSpace(_currentMarkdown))
        {
            RenderMarkdown(_currentMarkdown);
        }
        else
        {
            ShowWelcomePage();
        }
    }

    private async void EditModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var switchingToEdit = EditModeMenuItem.IsChecked;
        if (!switchingToEdit && _isEditMode)
        {
            if (!await EnsurePendingEditChangesHandledAsync("switching to view mode"))
            {
                EditModeMenuItem.IsChecked = true;
                return;
            }
        }

        _isEditMode = switchingToEdit;
        if (!string.IsNullOrWhiteSpace(_currentMarkdown))
        {
            RenderMarkdown(_currentMarkdown);
            if (switchingToEdit)
            {
                await WarnIfEditRoundTripChangesAsync();
            }
        }
        else
        {
            ShowWelcomePage();
        }
    }

    private void FindMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Focus();
        SearchTextBox.SelectAll();
    }

    private async void FindButton_Click(object sender, RoutedEventArgs e)
    {
        await FindInPageAsync();
    }

    private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await FindInPageAsync();
            e.Handled = true;
        }
    }

    private async Task FindInPageAsync()
    {
        if (MarkdownView.CoreWebView2 == null)
        {
            return;
        }

        var query = SearchTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SetStatus("Enter text to search.");
            return;
        }

        var outcome = await _searchState.FindNextAsync(query, wrap => FindInWebViewAsync(query, wrap));
        switch (outcome)
        {
            case SearchOutcome.Found:
                SetStatus($"Found: {query}");
                break;
            case SearchOutcome.WrappedFound:
                SetStatus($"Wrapped to top: {query}");
                break;
            case SearchOutcome.NoMatch:
                SetStatus($"No matches for: {query}");
                break;
        }
    }

    private async Task<bool> FindInWebViewAsync(string query, bool wrap)
    {
        var script = $"window.find({JsonSerializer.Serialize(query)}, false, false, {wrap.ToString().ToLowerInvariant()}, false, false, false)";
        var result = await MarkdownView.CoreWebView2.ExecuteScriptAsync(script);
        return bool.TryParse(result, out var found) && found;
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

    private void SetupFileWatcher(string filePath)
    {
        _fileWatcher?.Dispose();
        _fileWatcher = null;

        if (!_liveReloadEnabled)
        {
            return;
        }

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        _fileWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
        };

        _fileWatcher.Changed += OnWatchedFileChanged;
        _fileWatcher.Renamed += OnWatchedFileChanged;
        _fileWatcher.EnableRaisingEvents = true;

        _reloadTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _reloadTimer.Tick -= ReloadTimer_Tick;
        _reloadTimer.Tick += ReloadTimer_Tick;
    }

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        _pendingReloadPath = _currentFilePath;
        Dispatcher.Invoke(() =>
        {
            _reloadTimer?.Stop();
            _reloadTimer?.Start();
        });
    }

    private async void ReloadTimer_Tick(object? sender, EventArgs e)
    {
        _reloadTimer?.Stop();

        if (!string.IsNullOrWhiteSpace(_pendingReloadPath))
        {
            await LoadMarkdownFileAsync(_pendingReloadPath);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            NewMenuItem_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _ = SaveCurrentAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _ = PrintCurrentAsync();
            e.Handled = true;
        }
        else if (!_isEditMode && Keyboard.Modifiers == ModifierKeys.None && (e.Key == Key.Left || e.Key == Key.Right))
        {
            if (Keyboard.FocusedElement is TextBox)
            {
                return;
            }

            _ = NavigateSiblingMarkdownAsync(e.Key == Key.Right ? 1 : -1);
            e.Handled = true;
        }
    }

    private async Task NavigateSiblingMarkdownAsync(int direction)
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            SetStatus("Open a markdown file first.");
            return;
        }

        var directory = Path.GetDirectoryName(_currentFilePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var files = Directory
            .EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            SetStatus("No .md files found in folder.");
            return;
        }

        var currentIndex = files.FindIndex(path => string.Equals(path, _currentFilePath, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = (currentIndex + direction + files.Count) % files.Count;
        if (nextIndex == currentIndex)
        {
            return;
        }

        await LoadMarkdownFileAsync(files[nextIndex]);
    }

    private async void SaveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await SaveCurrentAsync();
    }

    private async void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await SaveCurrentAsync(forceSaveAs: true);
    }

    private async void PrintMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await PrintCurrentAsync();
    }

    private async Task PrintCurrentAsync()
    {
        var (success, error) = await PrintHelper.TryPrintAsync(MarkdownView.CoreWebView2);
        if (!success && !string.IsNullOrWhiteSpace(error))
        {
            SetStatus(error);
        }
    }

    private async Task<bool> SaveCurrentAsync(bool forceSaveAs = false)
    {
        if (!_isEditMode)
        {
            SetStatus("Switch to Edit Mode to save changes.");
            return false;
        }

        if (MarkdownView.CoreWebView2 == null)
        {
            return false;
        }

        var filePath = _currentFilePath;
        if (string.IsNullOrWhiteSpace(filePath) || forceSaveAs)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Markdown File",
                Filter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
                FileName = filePath != null ? Path.GetFileName(filePath) : "document.md"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return false;
            }

            filePath = dialog.FileName;
        }

        var markdown = await TryGetEditorMarkdownAsync(showTurndownError: true);
        if (markdown == null)
        {
            return false;
        }

        await File.WriteAllTextAsync(filePath, markdown);
        _currentFilePath = filePath;
        _currentMarkdown = markdown;
        _hasShownEditRoundTripWarning = false;
        Title = $"MDReader — {Path.GetFileName(filePath)}";
        SetStatus($"Saved: {filePath}");
        AddRecentFile(filePath);
        return true;
    }

    private async Task WarnIfEditRoundTripChangesAsync()
    {
        if (_settings.IgnoreEditRoundTripWarning)
        {
            return;
        }

        if (_hasShownEditRoundTripWarning || !_isEditMode || MarkdownView.CoreWebView2 == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_currentMarkdown))
        {
            return;
        }

        if (!await WaitForEditorBridgeAsync())
        {
            return;
        }

        var roundTripMarkdown = await TryGetEditorMarkdownAsync(showTurndownError: false);
        if (roundTripMarkdown == null)
        {
            return;
        }

        if (string.Equals(roundTripMarkdown, _currentMarkdown, StringComparison.Ordinal))
        {
            return;
        }

        _hasShownEditRoundTripWarning = true;
        SetStatus("Warning: edit-mode save may change markdown formatting.");
        MessageBox.Show(
            this,
            "This file does not round-trip exactly through Edit Mode.\n\n" +
            "If you save from Edit Mode, formatting details (such as spacing, line breaks, or some markdown constructs) may change.",
            "MDReader",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async Task<bool> WaitForEditorBridgeAsync()
    {
        if (MarkdownView.CoreWebView2 == null)
        {
            return false;
        }

        for (var i = 0; i < 20; i++)
        {
            try
            {
                var readyJson = await MarkdownView.CoreWebView2.ExecuteScriptAsync("typeof window.mdreader_getMarkdown === 'function'");
                if (bool.TryParse(readyJson, out var ready) && ready)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore transient script timing errors while navigation completes.
            }

            await Task.Delay(50);
        }

        return false;
    }

    private async Task<bool> EnsurePendingEditChangesHandledAsync(string actionText)
    {
        if (!_isEditMode || MarkdownView.CoreWebView2 == null)
        {
            return true;
        }

        var editorMarkdown = await TryGetEditorMarkdownAsync(showTurndownError: true);
        if (editorMarkdown == null)
        {
            return false;
        }

        if (string.Equals(editorMarkdown, _currentMarkdown ?? string.Empty, StringComparison.Ordinal))
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            $"You have unsaved changes. Save before {actionText}?",
            "MDReader",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.Yes)
        {
            return await SaveCurrentAsync();
        }

        return true;
    }

    private async Task<string?> TryGetEditorMarkdownAsync(bool showTurndownError)
    {
        if (MarkdownView.CoreWebView2 == null)
        {
            return null;
        }

        var markdownJson = await MarkdownView.CoreWebView2.ExecuteScriptAsync("window.mdreader_getMarkdown && window.mdreader_getMarkdown()") ?? "''";
        var markdown = JsonSerializer.Deserialize<string>(markdownJson) ?? string.Empty;
        if (markdown == "__TURNDOWN_MISSING__")
        {
            if (showTurndownError)
            {
                MessageBox.Show(this, "Turndown failed to load. Please rebuild and try again.", "MDReader", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }

        return markdown;
    }

    private void SetStatus(string text)
    {
        StatusText.Text = text;
    }
}