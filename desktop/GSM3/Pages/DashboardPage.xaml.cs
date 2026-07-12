using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GSM3.Services;
using GSM3.Models;

namespace GSM3.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly SystemMonitor _systemMonitor;
    private readonly InstanceManager _instanceManager;
    private DispatcherTimer? _refreshTimer;

    // Alert tracking
    private int _cpuHighCount;
    private bool _cpuAlertShown;
    private bool _memoryAlertShown;
    private bool _diskAlertShown;
    private bool _firstLoad = true;

    public DashboardPage()
    {
        InitializeComponent();
        _systemMonitor = ServiceLocator.GetService<SystemMonitor>();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();
        Loaded += DashboardPage_Loaded;
        Unloaded += DashboardPage_Unloaded;
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_firstLoad)
        {
            _firstLoad = false;
            ActivityLog.Log("仪表盘已加载", "系统");

            // Subscribe to instance status changes for activity logging
            _instanceManager.OnInstanceStatusChanged += OnInstanceStatusChanged;
        }

        await RefreshAllAsync();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await RefreshAllAsync();
        _refreshTimer.Start();
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private void OnInstanceStatusChanged(object? sender, InstanceStatusEventArgs args)
    {
        var statusText = args.NewStatus switch
        {
            InstanceStatus.Running => "已启动",
            InstanceStatus.Stopped => "已停止",
            InstanceStatus.Starting => "正在启动",
            InstanceStatus.Stopping => "正在停止",
            InstanceStatus.Crashed => "异常退出",
            _ => args.NewStatus.ToString()
        };

        var instance = _instanceManager.GetInstance(args.InstanceId);
        var name = instance?.Name ?? args.InstanceId;
        ActivityLog.Log($"实例 \"{name}\" {statusText}", "实例");
    }

    private async Task RefreshAllAsync()
    {
        try
        {
            await RefreshInstancesAsync();
            await RefreshSystemAsync();
            RefreshUptime();
            RefreshNetworkAsync();
            RefreshActivityLog();
        }
        catch { }
    }

    private async Task RefreshInstancesAsync()
    {
        await _instanceManager.InitializeAsync();
        var instances = _instanceManager.GetInstances();

        var total = instances.Count;
        var running = instances.Count(i => i.Status == InstanceStatus.Running);
        var stopped = instances.Count(i => i.Status == InstanceStatus.Stopped);
        var error = instances.Count(i => i.Status == InstanceStatus.Crashed);

        DispatcherQueue.TryEnqueue(() =>
        {
            TotalCountText.Text = total.ToString();
            RunningCountText.Text = running.ToString();
            StoppedCountText.Text = stopped.ToString();
            ErrorCountText.Text = error.ToString();

            // Show Quick Start card only when no instances exist
            QuickStartCard.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private async Task RefreshSystemAsync()
    {
        var cpu = await _systemMonitor.GetCpuUsageAsync();
        var memory = _systemMonitor.GetMemoryInfo();
        var disk = _systemMonitor.GetDiskInfo();

        DispatcherQueue.TryEnqueue(() =>
        {
            // Update gauges
            CpuProgressBar.Value = cpu.UsagePercent;
            CpuPercentText.Text = $"{cpu.UsagePercent:F1}%";

            MemoryProgressBar.Value = memory.UsagePercent;
            var usedGB = memory.UsedBytes / (1024.0 * 1024 * 1024);
            var totalGB = memory.TotalBytes / (1024.0 * 1024 * 1024);
            MemoryPercentText.Text = $"{memory.UsagePercent:F1}% ({usedGB:F1}/{totalGB:F1} GB)";

            DiskProgressBar.Value = disk.UsagePercent;
            var diskUsedGB = disk.UsedBytes / (1024.0 * 1024 * 1024);
            var diskTotalGB = disk.TotalBytes / (1024.0 * 1024 * 1024);
            DiskPercentText.Text = $"{disk.UsagePercent:F1}% ({diskUsedGB:F1}/{diskTotalGB:F1} GB)";

            // -- CPU alert: 3 consecutive checks > 90% --
            if (cpu.UsagePercent > 90)
            {
                _cpuHighCount++;
                if (_cpuHighCount >= 3 && !_cpuAlertShown)
                {
                    CpuAlertInfoBar.Message = $"CPU 使用率已连续 {_cpuHighCount} 次检测超过 90%，当前: {cpu.UsagePercent:F1}%";
                    CpuAlertInfoBar.IsOpen = true;
                    _cpuAlertShown = true;
                }
            }
            else
            {
                _cpuHighCount = 0;
                if (_cpuAlertShown)
                {
                    CpuAlertInfoBar.IsOpen = false;
                    _cpuAlertShown = false;
                }
            }

            // -- Memory alert: > 90% --
            if (memory.UsagePercent > 90)
            {
                if (!_memoryAlertShown)
                {
                    MemoryAlertInfoBar.Message = $"内存使用率超过 90%，当前: {memory.UsagePercent:F1}% ({usedGB:F1}/{totalGB:F1} GB)";
                    MemoryAlertInfoBar.IsOpen = true;
                    _memoryAlertShown = true;
                }
            }
            else
            {
                if (_memoryAlertShown)
                {
                    MemoryAlertInfoBar.IsOpen = false;
                    _memoryAlertShown = false;
                }
            }

            // -- Disk alert: > 95% --
            if (disk.UsagePercent > 95)
            {
                if (!_diskAlertShown)
                {
                    DiskAlertInfoBar.Message = $"磁盘使用率超过 95%，当前: {disk.UsagePercent:F1}% ({diskUsedGB:F1}/{diskTotalGB:F1} GB)";
                    DiskAlertInfoBar.IsOpen = true;
                    _diskAlertShown = true;
                }
            }
            else
            {
                if (_diskAlertShown)
                {
                    DiskAlertInfoBar.IsOpen = false;
                    _diskAlertShown = false;
                }
            }
        });
    }

    private void RefreshUptime()
    {
        var uptimeMs = Environment.TickCount64;
        var uptime = TimeSpan.FromMilliseconds(uptimeMs);
        string uptimeStr;

        if (uptime.TotalDays >= 1)
            uptimeStr = $"{(int)uptime.TotalDays} 天 {uptime.Hours} 小时 {uptime.Minutes} 分钟";
        else if (uptime.TotalHours >= 1)
            uptimeStr = $"{uptime.Hours} 小时 {uptime.Minutes} 分钟";
        else
            uptimeStr = $"{uptime.Minutes} 分钟";

        DispatcherQueue.TryEnqueue(() =>
        {
            UptimeText.Text = $"系统运行: {uptimeStr}";
        });
    }

    private void RefreshNetworkAsync()
    {
        var network = _systemMonitor.GetNetworkInfo();

        DispatcherQueue.TryEnqueue(() =>
        {
            UploadSpeedText.Text = FormatSpeed(network.SendSpeed);
            DownloadSpeedText.Text = FormatSpeed(network.ReceiveSpeed);
            TotalSentText.Text = FormatBytes(network.BytesSent);
            TotalReceivedText.Text = FormatBytes(network.BytesReceived);
        });
    }

    private void RefreshActivityLog()
    {
        var entries = ActivityLog.GetRecent(10);

        DispatcherQueue.TryEnqueue(() =>
        {
            // Clear existing entries (keep the NoActivityText as first child)
            while (ActivityLogPanel.Children.Count > 1)
                ActivityLogPanel.Children.RemoveAt(1);

            if (entries.Count == 0)
            {
                NoActivityText.Visibility = Visibility.Visible;
                return;
            }

            NoActivityText.Visibility = Visibility.Collapsed;

            foreach (var entry in entries)
            {
                var entryPanel = new Grid
                {
                    ColumnSpacing = 12,
                    Padding = new Thickness(0, 4, 0, 4)
                };
                entryPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                entryPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                entryPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var timeText = new TextBlock
                {
                    Text = entry.Timestamp.ToString("HH:mm:ss"),
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(timeText, 0);

                var categoryText = new TextBlock
                {
                    Text = $"[{entry.Category}]",
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(categoryText, 1);

                var messageText = new TextBlock
                {
                    Text = entry.Message,
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(messageText, 2);

                entryPanel.Children.Add(timeText);
                entryPanel.Children.Add(categoryText);
                entryPanel.Children.Add(messageText);

                ActivityLogPanel.Children.Add(entryPanel);
            }
        });
    }

    // -- Quick Start card navigation --

    private void QuickDeploy_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(GameDeployPage));
    }

    private void QuickCreate_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(InstancesPage));
    }

    private void QuickAction_CreateInstance(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(InstancesPage));
    }

    private void QuickAction_OpenTerminal(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(TerminalPage));
    }

    private void QuickAction_FileManager(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(FilesPage));
    }

    private void QuickAction_SystemMonitor(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SystemPage));
    }

    // -- Formatting helpers --

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024.0 * 1024):F2} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    private static string FormatSpeed(long bytesPerSec)
    {
        if (bytesPerSec >= 1024L * 1024)
            return $"{bytesPerSec / (1024.0 * 1024):F2} MB/s";
        if (bytesPerSec >= 1024)
            return $"{bytesPerSec / 1024.0:F1} KB/s";
        return $"{bytesPerSec} B/s";
    }
}
