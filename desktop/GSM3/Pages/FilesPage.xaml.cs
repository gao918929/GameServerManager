using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using GSM3.Models;
using GSM3.Services;
using System.IO;
using System.IO.Compression;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace GSM3.Pages;

public sealed partial class FilesPage : Page
{
    private string _currentPath = @"C:\";
    private readonly ObservableCollection<FileItem> _items = new();
    private readonly InstanceManager _instanceManager;
    private bool _suppressDriveChange;
    private bool _suppressInstanceChange;
    private bool _isSearchMode;

    private static readonly HashSet<string> EditableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".xml", ".yml", ".yaml", ".cfg", ".ini", ".properties",
        ".conf", ".log", ".bat", ".sh", ".ps1", ".cmd", ".cs", ".js", ".ts",
        ".py", ".java", ".md"
    };

    public FilesPage()
    {
        InitializeComponent();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();
        FileListView.ItemsSource = _items;
        SortComboBox.SelectedIndex = 0;
        Loaded += FilesPage_Loaded;
    }

    // ========== Initialization ==========

    private async void FilesPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _instanceManager.InitializeAsync();
            PopulateInstanceSelector();
            PopulateDrives();
            NavigateTo(@"C:\");
        }
        catch (Exception ex)
        {
            ShowStatus($"初始化错误: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void PopulateDrives()
    {
        _suppressDriveChange = true;
        DriveSelector.Items.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                DriveSelector.Items.Add(drive.Name);
            }
        }

        for (int i = 0; i < DriveSelector.Items.Count; i++)
        {
            if (string.Equals(DriveSelector.Items[i] as string,
                    Path.GetPathRoot(_currentPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                DriveSelector.SelectedIndex = i;
                break;
            }
        }
        _suppressDriveChange = false;
    }

    private void PopulateInstanceSelector()
    {
        _suppressInstanceChange = true;
        InstanceSelector.Items.Clear();
        InstanceSelector.Items.Add("全部文件");

        var instances = _instanceManager.GetInstances();
        foreach (var instance in instances)
        {
            InstanceSelector.Items.Add(instance);
        }

        _suppressInstanceChange = false;
    }

    private void InstanceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressInstanceChange) return;

        if (InstanceSelector.SelectedItem is Instance selectedInstance)
        {
            if (!string.IsNullOrEmpty(selectedInstance.WorkingDirectory) &&
                Directory.Exists(selectedInstance.WorkingDirectory))
            {
                NavigateTo(selectedInstance.WorkingDirectory);
            }
            else
            {
                ShowStatus($"实例 '{selectedInstance.Name}' 的工作目录不存在或未设置。", InfoBarSeverity.Warning);
            }
        }
        else if (InstanceSelector.SelectedItem is string s && s == "全部文件")
        {
            NavigateTo(@"C:\");
        }
    }

    // ========== Core Navigation ==========

    private void NavigateTo(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                ShowStatus($"路径不存在: {path}", InfoBarSeverity.Error);
                return;
            }

            _currentPath = Path.GetFullPath(path);
            PathTextBox.Text = _currentPath;
            _isSearchMode = false;

            _items.Clear();

            // Directories first
            foreach (var dir in Directory.GetDirectories(_currentPath))
            {
                try
                {
                    var info = new DirectoryInfo(dir);
                    _items.Add(new FileItem
                    {
                        Name = info.Name,
                        Path = info.FullName,
                        Type = FileItemType.Directory,
                        Size = 0,
                        ModifiedAt = info.LastWriteTime,
                        CreatedAt = info.CreationTime,
                        Extension = "",
                        IsReadOnly = false,
                        IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden)
                    });
                }
                catch
                {
                    // Skip directories we cannot access
                }
            }

            // Then files
            foreach (var file in Directory.GetFiles(_currentPath))
            {
                try
                {
                    var info = new FileInfo(file);
                    _items.Add(new FileItem
                    {
                        Name = info.Name,
                        Path = info.FullName,
                        Type = FileItemType.File,
                        Size = info.Length,
                        ModifiedAt = info.LastWriteTime,
                        CreatedAt = info.CreationTime,
                        Extension = info.Extension,
                        IsReadOnly = info.IsReadOnly,
                        IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden)
                    });
                }
                catch
                {
                    // Skip files we cannot access
                }
            }

            // Sync drive selector without triggering navigation
            _suppressDriveChange = true;
            var root = Path.GetPathRoot(_currentPath);
            for (int i = 0; i < DriveSelector.Items.Count; i++)
            {
                if (string.Equals(DriveSelector.Items[i] as string, root, StringComparison.OrdinalIgnoreCase))
                {
                    DriveSelector.SelectedIndex = i;
                    break;
                }
            }
            _suppressDriveChange = false;

            ApplySort();
            UpdateStatusText();
            StatusInfoBar.IsOpen = false;
        }
        catch (UnauthorizedAccessException)
        {
            ShowStatus($"访问被拒绝: {path}", InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            ShowStatus($"导航错误 {path}: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void UpdateStatusText()
    {
        var selectedCount = FileListView.SelectedItems.Count;
        var prefix = _isSearchMode ? "搜索结果: " : "";
        StatusText.Text = $"{prefix}共 {_items.Count} 项, 已选择 {selectedCount} 项";
    }

    // ========== Static Helpers (used by x:Bind in DataTemplate) ==========

    public static string GetFileIcon(FileItemType type)
    {
        return type == FileItemType.Directory ? "" : "";
    }

    public static string FormatFileSize(long size, FileItemType type)
    {
        if (type == FileItemType.Directory)
            return "";

        if (size < 1024)
            return $"{size} B";
        if (size < 1024 * 1024)
            return $"{size / 1024.0:F1} KB";
        if (size < 1024 * 1024 * 1024)
            return $"{size / (1024.0 * 1024.0):F1} MB";
        return $"{size / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    public static string FormatDateTime(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm");
    }

    // ========== Drive Selector ==========

    private void DriveSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDriveChange) return;
        if (DriveSelector.SelectedItem is string drivePath)
        {
            NavigateTo(drivePath);
        }
    }

    // ========== Navigation Controls ==========

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_currentPath);
        if (parent != null)
        {
            NavigateTo(parent.FullName);
        }
    }

    private void GoButton_Click(object sender, RoutedEventArgs e)
    {
        var path = PathTextBox.Text?.Trim();
        if (!string.IsNullOrEmpty(path))
        {
            NavigateTo(path);
        }
    }

    private void PathTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var path = PathTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                NavigateTo(path);
            }
        }
    }

    // ========== Sorting ==========

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplySort();
    }

    private void ApplySort()
    {
        if (SortComboBox == null || SortComboBox.SelectedIndex < 0 || _items.Count == 0)
            return;

        var sortIndex = SortComboBox.SelectedIndex;
        var dirs = _items.Where(i => i.Type == FileItemType.Directory).ToList();
        var files = _items.Where(i => i.Type == FileItemType.File).ToList();

        switch (sortIndex)
        {
            case 0: // 名称
                dirs = dirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
                files = files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
                break;
            case 1: // 大小
                dirs = dirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
                files = files.OrderBy(f => f.Size).ToList();
                break;
            case 2: // 修改日期
                dirs = dirs.OrderByDescending(d => d.ModifiedAt).ToList();
                files = files.OrderByDescending(f => f.ModifiedAt).ToList();
                break;
            case 3: // 类型
                dirs = dirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
                files = files.OrderBy(f => f.Extension, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
                break;
        }

        _items.Clear();
        foreach (var d in dirs) _items.Add(d);
        foreach (var f in files) _items.Add(f);
    }

    // ========== Search ==========

    private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            PerformSearch();
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private async void PerformSearch()
    {
        var query = SearchTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            ShowStatus("请输入搜索关键词。", InfoBarSeverity.Warning);
            return;
        }

        _isSearchMode = true;
        ShowStatus("正在搜索...", InfoBarSeverity.Informational);

        var results = new List<FileItem>();
        var searchPath = _currentPath;

        await Task.Run(() => SearchRecursive(searchPath, query, results, 1000));

        _items.Clear();
        foreach (var item in results)
        {
            _items.Add(item);
        }

        ApplySort();
        UpdateStatusText();
        ShowStatus($"搜索完成，找到 {_items.Count} 项。", InfoBarSeverity.Success);
    }

    private static void SearchRecursive(string directory, string query, List<FileItem> results, int maxResults)
    {
        if (results.Count >= maxResults) return;

        try
        {
            foreach (var dir in Directory.GetDirectories(directory))
            {
                if (results.Count >= maxResults) return;
                try
                {
                    var info = new DirectoryInfo(dir);
                    if (info.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new FileItem
                        {
                            Name = info.Name,
                            Path = info.FullName,
                            Type = FileItemType.Directory,
                            Size = 0,
                            ModifiedAt = info.LastWriteTime,
                            CreatedAt = info.CreationTime,
                            Extension = "",
                            IsReadOnly = false,
                            IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden)
                        });
                    }
                    SearchRecursive(dir, query, results, maxResults);
                }
                catch
                {
                    // Skip inaccessible directories
                }
            }

            foreach (var file in Directory.GetFiles(directory))
            {
                if (results.Count >= maxResults) return;
                try
                {
                    var info = new FileInfo(file);
                    if (info.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new FileItem
                        {
                            Name = info.Name,
                            Path = info.FullName,
                            Type = FileItemType.File,
                            Size = info.Length,
                            ModifiedAt = info.LastWriteTime,
                            CreatedAt = info.CreationTime,
                            Extension = info.Extension,
                            IsReadOnly = info.IsReadOnly,
                            IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden)
                        });
                    }
                }
                catch
                {
                    // Skip inaccessible files
                }
            }
        }
        catch
        {
            // Skip inaccessible directories
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = "";
        _isSearchMode = false;
        NavigateTo(_currentPath);
    }

    // ========== Toolbar Actions ==========

    private async void NewFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "新建文件",
            Content = new TextBox { PlaceholderText = "输入文件名..." },
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var nameBox = dialog.Content as TextBox;
            var name = nameBox?.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowStatus("文件名不能为空。", InfoBarSeverity.Warning);
                return;
            }

            try
            {
                var filePath = System.IO.Path.Combine(_currentPath, name);
                File.Create(filePath).Dispose();
                ShowStatus($"文件 '{name}' 已创建。", InfoBarSeverity.Success);
                NavigateTo(_currentPath);
            }
            catch (Exception ex)
            {
                ShowStatus($"创建文件失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void NewFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "新建文件夹",
            Content = new TextBox { PlaceholderText = "输入文件夹名..." },
            PrimaryButtonText = "创建",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var nameBox = dialog.Content as TextBox;
            var name = nameBox?.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowStatus("文件夹名不能为空。", InfoBarSeverity.Warning);
                return;
            }

            try
            {
                var dirPath = System.IO.Path.Combine(_currentPath, name);
                Directory.CreateDirectory(dirPath);
                ShowStatus($"文件夹 '{name}' 已创建。", InfoBarSeverity.Success);
                NavigateTo(_currentPath);
            }
            catch (Exception ex)
            {
                ShowStatus($"创建文件夹失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        ShowStatus("上传功能暂未实现。", InfoBarSeverity.Informational);
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        ShowStatus("下载功能暂未实现。", InfoBarSeverity.Informational);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItems.Count == 0)
        {
            ShowStatus("请选择要删除的项目。", InfoBarSeverity.Warning);
            return;
        }

        var items = FileListView.SelectedItems.Cast<FileItem>().ToList();
        var names = string.Join(", ", items.Select(i => i.Name));

        var dialog = new ContentDialog
        {
            Title = "确认删除",
            Content = $"确定要删除 {items.Count} 个项目吗？\n{names}",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            int deleted = 0;
            foreach (var item in items)
            {
                try
                {
                    if (item.Type == FileItemType.Directory)
                    {
                        Directory.Delete(item.Path, true);
                    }
                    else
                    {
                        File.Delete(item.Path);
                    }
                    deleted++;
                }
                catch (Exception ex)
                {
                    ShowStatus($"删除 '{item.Name}' 失败: {ex.Message}", InfoBarSeverity.Error);
                }
            }

            if (deleted > 0)
            {
                ShowStatus($"已删除 {deleted} 个项目。", InfoBarSeverity.Success);
            }
            NavigateTo(_currentPath);
        }
    }

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItems.Count == 0)
        {
            ShowStatus("请选择要复制的项目。", InfoBarSeverity.Warning);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "复制到",
            Content = new TextBox { PlaceholderText = "输入目标路径...", Text = _currentPath },
            PrimaryButtonText = "复制",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var destBox = dialog.Content as TextBox;
            var dest = destBox?.Text?.Trim();
            if (string.IsNullOrEmpty(dest))
            {
                ShowStatus("目标路径不能为空。", InfoBarSeverity.Warning);
                return;
            }

            var items = FileListView.SelectedItems.Cast<FileItem>().ToList();
            int copied = 0;
            foreach (var item in items)
            {
                try
                {
                    var target = System.IO.Path.Combine(dest, item.Name);
                    if (item.Type == FileItemType.Directory)
                    {
                        CopyDirectory(item.Path, target);
                    }
                    else
                    {
                        Directory.CreateDirectory(dest);
                        File.Copy(item.Path, target, false);
                    }
                    copied++;
                }
                catch (Exception ex)
                {
                    ShowStatus($"复制 '{item.Name}' 失败: {ex.Message}", InfoBarSeverity.Error);
                }
            }

            if (copied > 0)
            {
                ShowStatus($"已复制 {copied} 个项目到 {dest}。", InfoBarSeverity.Success);
            }
            NavigateTo(_currentPath);
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file)));
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(dir)));
        }
    }

    private async void MoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItems.Count == 0)
        {
            ShowStatus("请选择要移动的项目。", InfoBarSeverity.Warning);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "移动到",
            Content = new TextBox { PlaceholderText = "输入目标路径...", Text = _currentPath },
            PrimaryButtonText = "移动",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var destBox = dialog.Content as TextBox;
            var dest = destBox?.Text?.Trim();
            if (string.IsNullOrEmpty(dest))
            {
                ShowStatus("目标路径不能为空。", InfoBarSeverity.Warning);
                return;
            }

            var items = FileListView.SelectedItems.Cast<FileItem>().ToList();
            int moved = 0;
            foreach (var item in items)
            {
                try
                {
                    var target = System.IO.Path.Combine(dest, item.Name);
                    if (item.Type == FileItemType.Directory)
                    {
                        Directory.Move(item.Path, target);
                    }
                    else
                    {
                        Directory.CreateDirectory(dest);
                        File.Move(item.Path, target);
                    }
                    moved++;
                }
                catch (Exception ex)
                {
                    ShowStatus($"移动 '{item.Name}' 失败: {ex.Message}", InfoBarSeverity.Error);
                }
            }

            if (moved > 0)
            {
                ShowStatus($"已移动 {moved} 个项目到 {dest}。", InfoBarSeverity.Success);
            }
            NavigateTo(_currentPath);
        }
    }

    private async void CompressButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItems.Count == 0)
        {
            ShowStatus("请选择要压缩的项目。", InfoBarSeverity.Warning);
            return;
        }

        var item = FileListView.SelectedItems.Cast<FileItem>().First();

        if (item.Type == FileItemType.Directory)
        {
            var zipPath = item.Path + ".zip";
            var dialog = new ContentDialog
            {
                Title = "压缩文件夹",
                Content = $"将 '{item.Name}' 压缩为:\n{zipPath}",
                PrimaryButtonText = "压缩",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }
                    ZipFile.CreateFromDirectory(item.Path, zipPath);
                    ShowStatus($"已压缩 '{item.Name}' 为 {System.IO.Path.GetFileName(zipPath)}。", InfoBarSeverity.Success);
                    NavigateTo(_currentPath);
                }
                catch (Exception ex)
                {
                    ShowStatus($"压缩失败: {ex.Message}", InfoBarSeverity.Error);
                }
            }
        }
        else
        {
            var zipPath = System.IO.Path.ChangeExtension(item.Path, ".zip");
            var dialog = new ContentDialog
            {
                Title = "压缩文件",
                Content = $"将 '{item.Name}' 压缩为:\n{System.IO.Path.GetFileName(zipPath)}",
                PrimaryButtonText = "压缩",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    if (File.Exists(zipPath))
                    {
                        File.Delete(zipPath);
                    }
                    using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                    archive.CreateEntryFromFile(item.Path, item.Name);
                    ShowStatus($"已压缩 '{item.Name}' 为 {System.IO.Path.GetFileName(zipPath)}。", InfoBarSeverity.Success);
                    NavigateTo(_currentPath);
                }
                catch (Exception ex)
                {
                    ShowStatus($"压缩失败: {ex.Message}", InfoBarSeverity.Error);
                }
            }
        }
    }

    // ========== Extract ==========

    private async void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        if (FileListView.SelectedItems.Count == 0)
        {
            ShowStatus("请选择要解压的 .zip 文件。", InfoBarSeverity.Warning);
            return;
        }

        var item = FileListView.SelectedItems.Cast<FileItem>().First();
        if (item.Type != FileItemType.File ||
            !item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ShowStatus("请选择 .zip 文件进行解压。", InfoBarSeverity.Warning);
            return;
        }

        await ExtractZipFile(item);
    }

    private async Task ExtractZipFile(FileItem item)
    {
        var parentDir = System.IO.Path.GetDirectoryName(item.Path) ?? _currentPath;
        var destFolder = System.IO.Path.Combine(parentDir,
            System.IO.Path.GetFileNameWithoutExtension(item.Name));

        var textBox = new TextBox { Text = destFolder, PlaceholderText = "输入解压目标路径..." };
        var dialog = new ContentDialog
        {
            Title = "解压文件",
            Content = textBox,
            PrimaryButtonText = "解压",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var dest = textBox.Text?.Trim();
            if (string.IsNullOrEmpty(dest))
            {
                ShowStatus("目标路径不能为空。", InfoBarSeverity.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(dest);
                ZipFile.ExtractToDirectory(item.Path, dest, System.Text.Encoding.UTF8, true);
                ShowStatus($"已解压 '{item.Name}' 到 {dest}。", InfoBarSeverity.Success);
                NavigateTo(_currentPath);
            }
            catch (Exception ex)
            {
                ShowStatus($"解压失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    // ========== Right-Click Context Menu ==========

    private void FileItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not FileItem fileItem) return;

        var flyout = new MenuFlyout();

        // Open
        var openItem = new MenuFlyoutItem
        {
            Text = "打开",
            Icon = new FontIcon { Glyph = "" }
        };
        openItem.Click += (s, args) => ContextMenu_Open(fileItem);
        flyout.Items.Add(openItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        // Copy
        var copyItem = new MenuFlyoutItem
        {
            Text = "复制到...",
            Icon = new FontIcon { Glyph = "" }
        };
        copyItem.Click += (s, args) => ContextMenu_CopyTo(fileItem);
        flyout.Items.Add(copyItem);

        // Move
        var moveItem = new MenuFlyoutItem
        {
            Text = "移动到...",
            Icon = new FontIcon { Glyph = "" }
        };
        moveItem.Click += (s, args) => ContextMenu_MoveTo(fileItem);
        flyout.Items.Add(moveItem);

        // Rename
        var renameItem = new MenuFlyoutItem
        {
            Text = "重命名",
            Icon = new FontIcon { Glyph = "" }
        };
        renameItem.Click += (s, args) => ContextMenu_Rename(fileItem);
        flyout.Items.Add(renameItem);

        // Delete
        var deleteItem = new MenuFlyoutItem
        {
            Text = "删除",
            Icon = new FontIcon { Glyph = "" }
        };
        deleteItem.Click += (s, args) => ContextMenu_Delete(fileItem);
        flyout.Items.Add(deleteItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        // Compress
        var compressItem = new MenuFlyoutItem
        {
            Text = "压缩",
            Icon = new FontIcon { Glyph = "" }
        };
        compressItem.Click += (s, args) => ContextMenu_Compress(fileItem);
        flyout.Items.Add(compressItem);

        // Extract (only for .zip files)
        if (fileItem.Type == FileItemType.File &&
            fileItem.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var extractItem = new MenuFlyoutItem
            {
                Text = "解压",
                Icon = new FontIcon { Glyph = "" }
            };
            extractItem.Click += (s, args) => ContextMenu_Extract(fileItem);
            flyout.Items.Add(extractItem);
        }

        flyout.ShowAt(element, e.GetPosition(element));
    }

    private void ContextMenu_Open(FileItem item)
    {
        if (item.Type == FileItemType.Directory)
        {
            NavigateTo(item.Path);
        }
        else if (IsEditableTextFile(item.Extension))
        {
            OpenTextEditor(item);
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowStatus($"无法打开文件: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void ContextMenu_CopyTo(FileItem item)
    {
        var dialog = new ContentDialog
        {
            Title = "复制到",
            Content = new TextBox { PlaceholderText = "输入目标路径...", Text = _currentPath },
            PrimaryButtonText = "复制",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var destBox = dialog.Content as TextBox;
            var dest = destBox?.Text?.Trim();
            if (string.IsNullOrEmpty(dest)) return;

            try
            {
                var target = System.IO.Path.Combine(dest, item.Name);
                if (item.Type == FileItemType.Directory)
                {
                    CopyDirectory(item.Path, target);
                }
                else
                {
                    Directory.CreateDirectory(dest);
                    File.Copy(item.Path, target, false);
                }
                ShowStatus($"已复制 '{item.Name}' 到 {dest}。", InfoBarSeverity.Success);
                NavigateTo(_currentPath);
            }
            catch (Exception ex)
            {
                ShowStatus($"复制失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void ContextMenu_MoveTo(FileItem item)
    {
        var dialog = new ContentDialog
        {
            Title = "移动到",
            Content = new TextBox { PlaceholderText = "输入目标路径...", Text = _currentPath },
            PrimaryButtonText = "移动",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var destBox = dialog.Content as TextBox;
            var dest = destBox?.Text?.Trim();
            if (string.IsNullOrEmpty(dest)) return;

            try
            {
                var target = System.IO.Path.Combine(dest, item.Name);
                if (item.Type == FileItemType.Directory)
                {
                    Directory.Move(item.Path, target);
                }
                else
                {
                    Directory.CreateDirectory(dest);
                    File.Move(item.Path, target);
                }
                ShowStatus($"已移动 '{item.Name}' 到 {dest}。", InfoBarSeverity.Success);
                NavigateTo(_currentPath);
            }
            catch (Exception ex)
            {
                ShowStatus($"移动失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void ContextMenu_Rename(FileItem item)
    {
        var textBox = new TextBox { Text = item.Name };
        var dialog = new ContentDialog
        {
            Title = "重命名",
            Content = textBox,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var newName = textBox.Text?.Trim();
            if (string.IsNullOrEmpty(newName) || newName == item.Name) return;

            try
            {
                var parentDir = System.IO.Path.GetDirectoryName(item.Path);
                var newPath = System.IO.Path.Combine(parentDir!, newName);
                if (item.Type == FileItemType.Directory)
                {
                    Directory.Move(item.Path, newPath);
                }
                else
                {
                    File.Move(item.Path, newPath);
                }
                ShowStatus($"已重命名 '{item.Name}' 为 '{newName}'。", InfoBarSeverity.Success);
                NavigateTo(_currentPath);
            }
            catch (Exception ex)
            {
                ShowStatus($"重命名失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void ContextMenu_Delete(FileItem item)
    {
        var dialog = new ContentDialog
        {
            Title = "确认删除",
            Content = $"确定要删除 '{item.Name}' 吗？",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                if (item.Type == FileItemType.Directory)
                {
                    Directory.Delete(item.Path, true);
                }
                else
                {
                    File.Delete(item.Path);
                }
                ShowStatus($"已删除 '{item.Name}'。", InfoBarSeverity.Success);
                NavigateTo(_currentPath);
            }
            catch (Exception ex)
            {
                ShowStatus($"删除失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void ContextMenu_Compress(FileItem item)
    {
        string zipPath;
        if (item.Type == FileItemType.Directory)
        {
            zipPath = item.Path + ".zip";
        }
        else
        {
            zipPath = System.IO.Path.ChangeExtension(item.Path, ".zip");
        }

        var dialog = new ContentDialog
        {
            Title = "压缩",
            Content = $"将 '{item.Name}' 压缩为:\n{System.IO.Path.GetFileName(zipPath)}",
            PrimaryButtonText = "压缩",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                if (item.Type == FileItemType.Directory)
                {
                    ZipFile.CreateFromDirectory(item.Path, zipPath);
                }
                else
                {
                    using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                    archive.CreateEntryFromFile(item.Path, item.Name);
                }
                ShowStatus($"已压缩 '{item.Name}' 为 {System.IO.Path.GetFileName(zipPath)}。", InfoBarSeverity.Success);
                NavigateTo(_currentPath);
            }
            catch (Exception ex)
            {
                ShowStatus($"压缩失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void ContextMenu_Extract(FileItem item)
    {
        await ExtractZipFile(item);
    }

    // ========== Built-in Text Editor ==========

    private static bool IsEditableTextFile(string extension)
    {
        return EditableExtensions.Contains(extension);
    }

    private async void OpenTextEditor(FileItem item)
    {
        try
        {
            var content = await Task.Run(() => File.ReadAllText(item.Path));

            var textBox = new TextBox
            {
                Text = content,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                IsSpellCheckEnabled = false,
                MinWidth = 600,
                Height = 400,
                VerticalContentAlignment = VerticalAlignment.Top
            };
            ScrollViewer.SetVerticalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetHorizontalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);

            var dialog = new ContentDialog
            {
                Title = $"编辑 - {item.Name}",
                Content = textBox,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var newContent = textBox.Text;
                await Task.Run(() => File.WriteAllText(item.Path, newContent));
                ShowStatus($"文件 '{item.Name}' 已保存。", InfoBarSeverity.Success);
                NavigateTo(_currentPath);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"无法打开文件: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    // ========== File List Interaction ==========

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStatusText();
    }

    private void FileListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (FileListView.SelectedItem is FileItem item)
        {
            if (item.Type == FileItemType.Directory)
            {
                NavigateTo(item.Path);
            }
            else if (IsEditableTextFile(item.Extension))
            {
                OpenTextEditor(item);
            }
        }
    }

    // ========== Status ==========

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        });
    }
}
