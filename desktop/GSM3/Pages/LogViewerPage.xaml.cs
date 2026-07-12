using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage.Pickers;
using GSM3.Models;
using GSM3.Services;

namespace GSM3.Pages;

public sealed partial class LogViewerPage : Page
{
    private readonly InstanceManager _instanceManager;
    private string? _selectedDirectory;
    private string? _selectedFilePath;
    private string _fullLogContent = "";
    private List<LogFileInfo> _logFiles = new();

    public LogViewerPage()
    {
        InitializeComponent();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();
        Loaded += LogViewerPage_Loaded;
    }

    private async void LogViewerPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _instanceManager.InitializeAsync();
            var instances = _instanceManager.GetInstances();
            InstanceSelector.ItemsSource = instances;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载实例列表失败: {ex.Message}";
        }
    }

    // ── Data loading ─────────────────────────────────────────────

    private void LoadLogFiles()
    {
        if (string.IsNullOrWhiteSpace(_selectedDirectory) || !Directory.Exists(_selectedDirectory))
        {
            _logFiles.Clear();
            FileList.ItemsSource = null;
            FileListEmpty.Text = "目录不存在";
            FileListEmpty.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".log", ".txt", ".out", ".err"
            };

            _logFiles = Directory.GetFiles(_selectedDirectory)
                .Where(f => extensions.Contains(Path.GetExtension(f)) ||
                            Path.GetFileName(f).Contains("log", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Select(f => new LogFileInfo
                {
                    Name = f.Name,
                    FullPath = f.FullName,
                    ModifiedTime = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Size = f.Length
                })
                .ToList();

            if (_logFiles.Count > 0)
            {
                FileList.ItemsSource = _logFiles;
                FileListEmpty.Visibility = Visibility.Collapsed;
            }
            else
            {
                FileList.ItemsSource = null;
                FileListEmpty.Text = "未找到日志文件";
                FileListEmpty.Visibility = Visibility.Visible;
            }

            StatusText.Text = $"已加载 {_logFiles.Count} 个日志文件 — {_selectedDirectory}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载失败: {ex.Message}";
            FileListEmpty.Text = "加载失败";
            FileListEmpty.Visibility = Visibility.Visible;
        }
    }

    private void LoadFileContent(string filePath)
    {
        try
        {
            _selectedFilePath = filePath;
            _fullLogContent = File.ReadAllText(filePath);
            ApplyFilter();
            StatusText.Text = $"已加载: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            LogContent.Text = $"读取文件失败: {ex.Message}";
            LineCountText.Text = "共 0 行";
            StatusText.Text = $"读取失败: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        var filter = SearchBox.Text?.Trim();
        string displayContent;

        if (string.IsNullOrWhiteSpace(filter))
        {
            displayContent = _fullLogContent;
        }
        else
        {
            var lines = _fullLogContent.Split('\n');
            var filtered = lines.Where(line =>
                line.Contains(filter, StringComparison.OrdinalIgnoreCase));
            displayContent = string.Join("\n", filtered);
        }

        LogContent.Text = displayContent;

        var lineCount = string.IsNullOrEmpty(displayContent)
            ? 0
            : displayContent.Split('\n').Length;
        LineCountText.Text = $"共 {lineCount} 行";

        if (AutoScrollToggle.IsChecked == true)
        {
            LogScrollViewer.UpdateLayout();
            LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
        }
    }

    // ── Event handlers ───────────────────────────────────────────

    private void InstanceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InstanceSelector.SelectedItem is Instance instance)
        {
            var logsDir = Path.Combine(instance.WorkingDirectory, "logs");
            _selectedDirectory = Directory.Exists(logsDir) ? logsDir : instance.WorkingDirectory;
            DirectoryPath.Text = _selectedDirectory;
            LoadLogFiles();
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var window = App.MainAppWindow;
            if (window != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                DirectoryPath.Text = folder.Path;
                _selectedDirectory = folder.Path;
                LoadLogFiles();
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"选择目录失败: {ex.Message}";
        }
    }

    private void DirectoryPath_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            _selectedDirectory = DirectoryPath.Text?.Trim();
            LoadLogFiles();
            e.Handled = true;
        }
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is LogFileInfo file)
        {
            LoadFileContent(file.FullPath);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_selectedDirectory))
        {
            LoadLogFiles();
        }

        if (!string.IsNullOrWhiteSpace(_selectedFilePath) && File.Exists(_selectedFilePath))
        {
            LoadFileContent(_selectedFilePath);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_fullLogContent))
        {
            ApplyFilter();
        }
    }

    // ── Data model ───────────────────────────────────────────────

    public class LogFileInfo
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string ModifiedTime { get; set; } = "";
        public long Size { get; set; }
    }
}
