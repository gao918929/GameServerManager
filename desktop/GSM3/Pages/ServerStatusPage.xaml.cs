using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using GSM3.Models;
using GSM3.Services;

namespace GSM3.Pages;

public sealed partial class ServerStatusPage : Page
{
    private readonly ServerQueryService _queryService;
    private readonly InstanceManager _instanceManager;
    private readonly ObservableCollection<SavedQueryDisplayItem> _savedItems = new();
    private readonly ObservableCollection<InstanceDisplayItem> _instanceItems = new();
    private DispatcherTimer? _autoRefreshTimer;

    public ServerStatusPage()
    {
        InitializeComponent();
        _queryService = ServiceLocator.GetService<ServerQueryService>();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();
        SavedQueryListView.ItemsSource = _savedItems;
        InstanceListView.ItemsSource = _instanceItems;
        Loaded += ServerStatusPage_Loaded;
        Unloaded += ServerStatusPage_Unloaded;
    }

    private void ServerStatusPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshSavedQueryList();
        PopulateInstanceList();
    }

    private void ServerStatusPage_Unloaded(object sender, RoutedEventArgs e)
    {
        StopAutoRefresh();
    }

    // ── Instance status ──────────────────────────────────────────────────────

    private void PopulateInstanceList()
    {
        _instanceItems.Clear();
        var instances = _instanceManager.GetInstances();
        var hasItems = false;

        foreach (var inst in instances)
        {
            if (inst.Port <= 0) continue;
            hasItems = true;

            _instanceItems.Add(new InstanceDisplayItem
            {
                InstanceId = inst.Id,
                DisplayName = inst.Name,
                Port = inst.Port,
                InstanceType = inst.Type,
                PortText = $":{inst.Port}",
                TypeText = inst.Type switch
                {
                    InstanceType.MinecraftJava => "MC Java",
                    InstanceType.MinecraftBedrock => "MC Bedrock",
                    _ => "Generic"
                },
                StatusSummary = "未查询",
                PingText = "-",
                StatusColor = new SolidColorBrush(ColorHelper.FromArgb(255, 128, 128, 128)),
            });
        }

        NoInstancesText.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void RefreshInstancesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshInstancesButton.IsEnabled = false;
        ShowStatus("正在刷新所有实例...", InfoBarSeverity.Informational);

        try
        {
            await RefreshAllInstancesAsync();
            ShowStatus("实例刷新完成", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"实例刷新失败: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            RefreshInstancesButton.IsEnabled = true;
        }
    }

    private async Task RefreshAllInstancesAsync()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < _instanceItems.Count; i++)
        {
            var item = _instanceItems[i];
            var index = i;
            tasks.Add(QueryAndUpdateInstanceAsync(item, index));
        }
        await Task.WhenAll(tasks);
    }

    private async Task QueryAndUpdateInstanceAsync(InstanceDisplayItem item, int index)
    {
        try
        {
            ServerQueryResult result;

            if (item.InstanceType == InstanceType.MinecraftJava ||
                item.InstanceType == InstanceType.MinecraftBedrock)
            {
                result = await _queryService.QueryMinecraftAsync("127.0.0.1", item.Port);
            }
            else
            {
                result = await _queryService.QueryTcpPingAsync("127.0.0.1", item.Port);
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (index >= _instanceItems.Count) return;

                item.StatusSummary = result.IsOnline
                    ? (string.IsNullOrEmpty(result.ServerName) ? "在线" : result.ServerName)
                    : "离线";
                item.PingText = result.IsOnline ? $"{result.PingMs}ms" : "-";
                item.StatusColor = result.IsOnline
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 16))
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));

                _instanceItems[index] = item;
            });
        }
        catch
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (index >= _instanceItems.Count) return;

                item.StatusSummary = "查询失败";
                item.PingText = "-";
                item.StatusColor = new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));
                _instanceItems[index] = item;
            });
        }
    }

    private async void QueryInstanceItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string instanceId)
        {
            for (int i = 0; i < _instanceItems.Count; i++)
            {
                if (_instanceItems[i].InstanceId == instanceId)
                {
                    await QueryAndUpdateInstanceAsync(_instanceItems[i], i);
                    break;
                }
            }
        }
    }

    // ── Manual query ────────────────────────────────────────────────────────

    private async void QueryButton_Click(object sender, RoutedEventArgs e)
    {
        var host = HostBox.Text.Trim();
        var port = (int)PortBox.Value;
        var protocolItem = ProtocolComboBox.SelectedItem as ComboBoxItem;
        var protocol = protocolItem?.Tag?.ToString() ?? "A2S";

        if (string.IsNullOrWhiteSpace(host))
        {
            ShowStatus("请输入主机地址", InfoBarSeverity.Warning);
            return;
        }

        QueryButton.IsEnabled = false;
        ShowStatus("正在查询...", InfoBarSeverity.Informational);

        try
        {
            ServerQueryResult result;

            if (protocol == "MC")
                result = await _queryService.QueryMinecraftAsync(host, port);
            else if (protocol == "TCP")
                result = await _queryService.QueryTcpPingAsync(host, port);
            else
                result = await _queryService.QueryA2SInfoAsync(host, port);

            DisplayResult(result);

            if (result.IsOnline)
                ShowStatus("查询成功", InfoBarSeverity.Success);
            else
                ShowStatus("服务器离线或无法连接", InfoBarSeverity.Warning);
        }
        catch (Exception ex)
        {
            ShowStatus($"查询失败: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            QueryButton.IsEnabled = true;
        }
    }

    private void DisplayResult(ServerQueryResult result)
    {
        ResultCard.Visibility = Visibility.Visible;

        ResultServerName.Text = string.IsNullOrEmpty(result.ServerName) ? "-" : result.ServerName;
        ResultGameName.Text = string.IsNullOrEmpty(result.GameName) ? "-" : result.GameName;
        ResultMap.Text = string.IsNullOrEmpty(result.Map) ? "-" : result.Map;
        ResultPlayers.Text = $"{result.CurrentPlayers} / {result.MaxPlayers}";
        ResultVersion.Text = string.IsNullOrEmpty(result.Version) ? "-" : result.Version;
        ResultPing.Text = $"{result.PingMs} ms";
        ResultProtocol.Text = result.Protocol switch
        {
            "MC" => "Minecraft",
            "TCP" => "TCP Ping",
            _ => "A2S_INFO (Source)"
        };

        if (result.Protocol == "A2S" && result.Bots > 0)
        {
            BotsPanel.Visibility = Visibility.Visible;
            ResultBots.Text = result.Bots.ToString();
        }
        else
        {
            BotsPanel.Visibility = Visibility.Collapsed;
        }

        if (result.IsOnline)
        {
            OnlineStatusText.Text = "在线";
            OnlineStatusBadge.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 16));
        }
        else
        {
            OnlineStatusText.Text = "离线";
            OnlineStatusBadge.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));
        }
    }

    // ── Save query ──────────────────────────────────────────────────────────

    private void SaveQueryButton_Click(object sender, RoutedEventArgs e)
    {
        var host = HostBox.Text.Trim();
        var port = (int)PortBox.Value;
        var protocolItem = ProtocolComboBox.SelectedItem as ComboBoxItem;
        var protocol = protocolItem?.Tag?.ToString() ?? "A2S";

        if (string.IsNullOrWhiteSpace(host))
        {
            ShowStatus("请输入主机地址", InfoBarSeverity.Warning);
            return;
        }

        var query = new SavedQuery
        {
            Host = host,
            Port = port,
            Protocol = protocol,
            Label = $"{host}:{port}",
        };

        _queryService.AddSavedQuery(query);
        RefreshSavedQueryList();
        ShowStatus("查询已保存", InfoBarSeverity.Success);
    }

    // ── Saved query list ────────────────────────────────────────────────────

    private void RefreshSavedQueryList()
    {
        var queries = _queryService.GetSavedQueries();
        _savedItems.Clear();
        foreach (var q in queries)
        {
            _savedItems.Add(new SavedQueryDisplayItem
            {
                Id = q.Id,
                Host = q.Host,
                Port = q.Port,
                Protocol = q.Protocol,
                DisplayName = string.IsNullOrEmpty(q.Label) ? $"{q.Host}:{q.Port}" : q.Label,
                StatusSummary = "未查询",
                PlayersText = "-",
                PingText = "-",
                StatusColor = new SolidColorBrush(ColorHelper.FromArgb(255, 128, 128, 128)),
            });
        }
    }

    private async void RefreshAllButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshAllButton.IsEnabled = false;
        ShowStatus("正在刷新所有查询...", InfoBarSeverity.Informational);

        try
        {
            await RefreshAllSavedQueriesAsync();
            ShowStatus("全部刷新完成", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"刷新失败: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            RefreshAllButton.IsEnabled = true;
        }
    }

    private async Task RefreshAllSavedQueriesAsync()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < _savedItems.Count; i++)
        {
            var item = _savedItems[i];
            var index = i;
            tasks.Add(QueryAndUpdateItemAsync(item, index));
        }
        await Task.WhenAll(tasks);
    }

    private async Task QueryAndUpdateItemAsync(SavedQueryDisplayItem item, int index)
    {
        try
        {
            ServerQueryResult result;
            if (item.Protocol == "MC")
                result = await _queryService.QueryMinecraftAsync(item.Host, item.Port);
            else if (item.Protocol == "TCP")
                result = await _queryService.QueryTcpPingAsync(item.Host, item.Port);
            else
                result = await _queryService.QueryA2SInfoAsync(item.Host, item.Port);

            DispatcherQueue.TryEnqueue(() =>
            {
                if (index >= _savedItems.Count) return;

                item.StatusSummary = result.IsOnline
                    ? (string.IsNullOrEmpty(result.ServerName) ? "在线" : result.ServerName)
                    : "离线";
                item.PlayersText = result.IsOnline
                    ? $"{result.CurrentPlayers}/{result.MaxPlayers}"
                    : "-";
                item.PingText = result.IsOnline ? $"{result.PingMs}ms" : "-";
                item.StatusColor = result.IsOnline
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 16, 124, 16))
                    : new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));

                // Force list refresh by replacing the item
                _savedItems[index] = item;
            });
        }
        catch
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (index >= _savedItems.Count) return;

                item.StatusSummary = "查询失败";
                item.PlayersText = "-";
                item.PingText = "-";
                item.StatusColor = new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));
                _savedItems[index] = item;
            });
        }
    }

    private async void QuerySavedItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            for (int i = 0; i < _savedItems.Count; i++)
            {
                if (_savedItems[i].Id == id)
                {
                    await QueryAndUpdateItemAsync(_savedItems[i], i);
                    break;
                }
            }
        }
    }

    private void DeleteSavedItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            _queryService.RemoveSavedQuery(id);
            RefreshSavedQueryList();
            ShowStatus("已删除", InfoBarSeverity.Success);
        }
    }

    // ── Auto-refresh ────────────────────────────────────────────────────────

    private void AutoRefreshToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshToggle.IsOn)
            StartAutoRefresh();
        else
            StopAutoRefresh();
    }

    private void RefreshIntervalBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        // Restart timer if auto-refresh is active
        if (_autoRefreshTimer != null && AutoRefreshToggle.IsOn)
        {
            StopAutoRefresh();
            StartAutoRefresh();
        }
    }

    private void StartAutoRefresh()
    {
        StopAutoRefresh();
        var intervalSeconds = (int)RefreshIntervalBox.Value;
        if (intervalSeconds < 5) intervalSeconds = 5;

        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(intervalSeconds)
        };
        _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
        _autoRefreshTimer.Start();
    }

    private void StopAutoRefresh()
    {
        if (_autoRefreshTimer != null)
        {
            _autoRefreshTimer.Stop();
            _autoRefreshTimer.Tick -= AutoRefreshTimer_Tick;
            _autoRefreshTimer = null;
        }
    }

    private async void AutoRefreshTimer_Tick(object? sender, object e)
    {
        await RefreshAllSavedQueriesAsync();
        await RefreshAllInstancesAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}

public class InstanceDisplayItem
{
    public string InstanceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Port { get; set; }
    public InstanceType InstanceType { get; set; }
    public string PortText { get; set; } = "";
    public string TypeText { get; set; } = "";
    public string StatusSummary { get; set; } = "";
    public string PingText { get; set; } = "-";
    public SolidColorBrush StatusColor { get; set; } = new(ColorHelper.FromArgb(255, 128, 128, 128));
}

public class SavedQueryDisplayItem
{
    public string Id { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Protocol { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string StatusSummary { get; set; } = "";
    public string PlayersText { get; set; } = "-";
    public string PingText { get; set; } = "-";
    public SolidColorBrush StatusColor { get; set; } = new(ColorHelper.FromArgb(255, 128, 128, 128));
}
