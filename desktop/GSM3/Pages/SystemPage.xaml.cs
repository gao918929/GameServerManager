using System.Collections.ObjectModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using GSM3.Services;

namespace GSM3.Pages;

public sealed partial class SystemPage : Page
{
    private readonly SystemMonitor _systemMonitor;
    private DispatcherTimer? _refreshTimer;

    private const int MaxHistoryPoints = 60;
    private readonly List<double> _cpuHistory = new();
    private readonly List<double> _memoryHistory = new();

    public SystemPage()
    {
        InitializeComponent();
        _systemMonitor = ServiceLocator.GetService<SystemMonitor>();
        Loaded += SystemPage_Loaded;
        Unloaded += SystemPage_Unloaded;
    }

    private async void SystemPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRealDataAsync();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await LoadRealDataAsync();
        _refreshTimer.Start();
    }

    private void SystemPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private async Task LoadRealDataAsync()
    {
        try
        {
            var cpu = await _systemMonitor.GetCpuUsageAsync();
            var memory = _systemMonitor.GetMemoryInfo();
            var disk = _systemMonitor.GetDiskInfo();
            var network = _systemMonitor.GetNetworkInfo();

            DispatcherQueue.TryEnqueue(() =>
            {
                CpuBar.Value = cpu.UsagePercent;
                CpuText.Text = $"{cpu.UsagePercent:F1}%";

                MemoryBar.Value = memory.UsagePercent;
                MemoryText.Text = $"{memory.UsagePercent:F1}%";
                var usedGB = memory.UsedBytes / (1024.0 * 1024 * 1024);
                var totalGB = memory.TotalBytes / (1024.0 * 1024 * 1024);
                MemoryDetailText.Text = $"{usedGB:F1} GB / {totalGB:F1} GB";

                DiskBar.Value = disk.UsagePercent;
                DiskText.Text = $"{disk.UsagePercent:F1}%";

                var uploadSpeed = FormatSpeed(network.SendSpeed);
                var downloadSpeed = FormatSpeed(network.ReceiveSpeed);
                UploadText.Text = uploadSpeed;
                DownloadText.Text = downloadSpeed;

                // Update performance history
                UpdateHistory(_cpuHistory, cpu.UsagePercent);
                UpdateHistory(_memoryHistory, memory.UsagePercent);
                RebuildChart(CpuChartPanel, _cpuHistory);
                RebuildChart(MemoryChartPanel, _memoryHistory);
                UpdateStatsLabels(_cpuHistory, CpuMinText, CpuAvgText, CpuMaxText);
                UpdateStatsLabels(_memoryHistory, MemMinText, MemAvgText, MemMaxText);
            });

            var processes = _systemMonitor.GetProcessList();
            var topProcesses = processes
                .OrderByDescending(p => p.MemoryBytes)
                .Take(50)
                .Select(p => new ProcessDisplayItem
                {
                    Pid = p.Pid,
                    Name = p.Name,
                    CpuPercent = 0,
                    MemoryDisplay = FormatBytes(p.MemoryBytes)
                })
                .ToList();

            var ports = _systemMonitor.GetActivePorts();
            var portItems = ports
                .Where(p => p.State == "LISTENING" || p.State == "Established" || p.Protocol == "UDP")
                .Take(100)
                .Select(p => new PortDisplayItem
                {
                    Protocol = p.Protocol,
                    LocalAddress = p.LocalAddress,
                    LocalPort = p.LocalPort,
                    RemoteAddress = p.RemoteAddress,
                    State = p.Protocol == "UDP" ? "LISTENING" : p.State
                })
                .ToList();

            DispatcherQueue.TryEnqueue(() =>
            {
                ProcessListView.ItemsSource = new ObservableCollection<ProcessDisplayItem>(topProcesses);
                PortListView.ItemsSource = new ObservableCollection<PortDisplayItem>(portItems);
            });
        }
        catch { }
    }

    private static void UpdateHistory(List<double> history, double value)
    {
        history.Add(value);
        while (history.Count > MaxHistoryPoints)
            history.RemoveAt(0);
    }

    private static void RebuildChart(StackPanel panel, List<double> history)
    {
        panel.Children.Clear();
        const double chartHeight = 100.0;
        const double barWidth = 5.0;

        foreach (var value in history)
        {
            var clampedValue = Math.Max(0, Math.Min(100, value));
            var barHeight = Math.Max(1, clampedValue / 100.0 * chartHeight);

            var bar = new Border
            {
                Width = barWidth,
                Height = barHeight,
                CornerRadius = new CornerRadius(1, 1, 0, 0),
                Background = GetBarBrush(clampedValue),
                VerticalAlignment = VerticalAlignment.Bottom
            };

            panel.Children.Add(bar);
        }
    }

    private static SolidColorBrush GetBarBrush(double value)
    {
        if (value >= 80)
            return new SolidColorBrush(ColorHelper.FromArgb(255, 232, 68, 68));   // Red
        if (value >= 60)
            return new SolidColorBrush(ColorHelper.FromArgb(255, 232, 185, 35));  // Yellow/Amber
        return new SolidColorBrush(ColorHelper.FromArgb(255, 72, 199, 116));      // Green
    }

    private static void UpdateStatsLabels(List<double> history, TextBlock minText, TextBlock avgText, TextBlock maxText)
    {
        if (history.Count == 0)
        {
            minText.Text = "最小: --";
            avgText.Text = "平均: --";
            maxText.Text = "最大: --";
            return;
        }

        var min = history.Min();
        var avg = history.Average();
        var max = history.Max();

        minText.Text = $"最小: {min:F1}%";
        avgText.Text = $"平均: {avg:F1}%";
        maxText.Text = $"最大: {max:F1}%";
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadRealDataAsync();
    }

    private void KillProcessButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessListView.SelectedItem is ProcessDisplayItem item)
        {
            var result = _systemMonitor.KillProcess(item.Pid);
            if (!result.Success)
            {
                _ = new ContentDialog
                {
                    Title = "终止失败",
                    Content = result.Error,
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot
                }.ShowAsync();
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes >= 1024L) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    private static string FormatSpeed(long bytesPerSec)
    {
        if (bytesPerSec >= 1024L * 1024) return $"{bytesPerSec / (1024.0 * 1024):F1} MB/s";
        if (bytesPerSec >= 1024L) return $"{bytesPerSec / 1024.0:F1} KB/s";
        return $"{bytesPerSec} B/s";
    }
}

public class ProcessDisplayItem
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public double CpuPercent { get; set; }
    public string MemoryDisplay { get; set; } = "";
}

public class PortDisplayItem
{
    public string Protocol { get; set; } = "";
    public string LocalAddress { get; set; } = "";
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = "";
    public string State { get; set; } = "";
    public string ProcessName { get; set; } = "";
}
