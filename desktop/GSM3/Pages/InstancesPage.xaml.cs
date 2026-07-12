using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GSM3.Models;
using GSM3.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace GSM3.Pages;

public sealed partial class InstancesPage : Page
{
    private readonly InstanceManager _instanceManager;
    private readonly SteamCMDManager _steamCmdManager;
    private readonly ObservableCollection<Instance> _instances = new();

    public InstancesPage()
    {
        InitializeComponent();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();
        _steamCmdManager = ServiceLocator.GetService<SteamCMDManager>();
        InstanceListView.ItemsSource = _instances;
        Loaded += InstancesPage_Loaded;
    }

    private async void InstancesPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _instanceManager.InitializeAsync();
            RefreshInstanceList();
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load instances: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void RefreshInstanceList()
    {
        var instances = _instanceManager.GetInstances();
        _instances.Clear();
        foreach (var instance in instances)
        {
            _instances.Add(instance);
        }
    }

    private async void CreateInstanceButton_Click(object sender, RoutedEventArgs e)
    {
        // Reset dialog fields
        InstanceNameTextBox.Text = "";
        GameTypeComboBox.SelectedIndex = -1;
        WorkingDirectoryTextBox.Text = "";
        ExecutablePathTextBox.Text = "";
        LaunchArgumentsTextBox.Text = "";
        PortNumberBox.Value = 25565;
        RconPortNumberBox.Value = 25575;
        SteamAppIdTextBox.Text = "";
        AutoRestartToggle.IsOn = false;
        AutoRestartMaxRetriesBox.Value = 3;
        AutoRestartDelayBox.Value = 10;

        CreateInstanceDialog.XamlRoot = XamlRoot;
        var result = await CreateInstanceDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var name = InstanceNameTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowStatus("Instance name is required.", InfoBarSeverity.Warning);
                return;
            }

            var instance = new Instance
            {
                Name = name,
                WorkingDirectory = WorkingDirectoryTextBox.Text?.Trim() ?? "",
                ProgramPath = ExecutablePathTextBox.Text?.Trim() ?? "",
                StartCommand = LaunchArgumentsTextBox.Text?.Trim() ?? "",
                Type = ParseGameType(GameTypeComboBox.SelectedItem as ComboBoxItem),
                Port = (int)PortNumberBox.Value,
                SteamAppId = SteamAppIdTextBox.Text?.Trim() ?? "",
                AutoRestart = AutoRestartToggle.IsOn,
                AutoRestartMaxRetries = (int)AutoRestartMaxRetriesBox.Value,
                AutoRestartDelaySeconds = (int)AutoRestartDelayBox.Value,
            };

            try
            {
                await _instanceManager.CreateInstanceAsync(instance);
                RefreshInstanceList();
                ShowStatus($"Instance '{name}' created successfully.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to create instance: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var instance = InstanceListView.SelectedItem as Instance;
        if (instance == null)
        {
            ShowStatus("Please select an instance to start.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            ShowStatus($"Starting '{instance.Name}'...", InfoBarSeverity.Informational);
            var success = await _instanceManager.StartInstanceAsync(instance.Id);
            if (success)
            {
                ShowStatus($"Instance '{instance.Name}' started.", InfoBarSeverity.Success);
            }
            else
            {
                ShowStatus($"Failed to start '{instance.Name}'.", InfoBarSeverity.Error);
            }
            RefreshInstanceList();
        }
        catch (Exception ex)
        {
            ShowStatus($"Error starting instance: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        var instance = InstanceListView.SelectedItem as Instance;
        if (instance == null)
        {
            ShowStatus("Please select an instance to stop.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            ShowStatus($"Stopping '{instance.Name}'...", InfoBarSeverity.Informational);
            var success = await _instanceManager.StopInstanceAsync(instance.Id);
            if (success)
            {
                ShowStatus($"Instance '{instance.Name}' stopped.", InfoBarSeverity.Success);
            }
            else
            {
                ShowStatus($"Failed to stop '{instance.Name}'.", InfoBarSeverity.Error);
            }
            RefreshInstanceList();
        }
        catch (Exception ex)
        {
            ShowStatus($"Error stopping instance: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void RestartButton_Click(object sender, RoutedEventArgs e)
    {
        var instance = InstanceListView.SelectedItem as Instance;
        if (instance == null)
        {
            ShowStatus("Please select an instance to restart.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            ShowStatus($"Restarting '{instance.Name}'...", InfoBarSeverity.Informational);
            var success = await _instanceManager.RestartInstanceAsync(instance.Id);
            if (success)
            {
                ShowStatus($"Instance '{instance.Name}' restarted.", InfoBarSeverity.Success);
            }
            else
            {
                ShowStatus($"Failed to restart '{instance.Name}'.", InfoBarSeverity.Error);
            }
            RefreshInstanceList();
        }
        catch (Exception ex)
        {
            ShowStatus($"Error restarting instance: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var instance = InstanceListView.SelectedItem as Instance;
        if (instance == null)
        {
            ShowStatus("Please select an instance to delete.", InfoBarSeverity.Warning);
            return;
        }

        var confirmDialog = new ContentDialog
        {
            Title = "Confirm Delete",
            Content = $"Are you sure you want to delete '{instance.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var dialogResult = await confirmDialog.ShowAsync();
        if (dialogResult != ContentDialogResult.Primary)
            return;

        try
        {
            var success = await _instanceManager.DeleteInstanceAsync(instance.Id);
            if (success)
            {
                RefreshInstanceList();
                ShowStatus($"Instance '{instance.Name}' deleted.", InfoBarSeverity.Success);
            }
            else
            {
                ShowStatus($"Failed to delete '{instance.Name}'.", InfoBarSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error deleting instance: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    // ── Batch Operations ──────────────────────────────────────

    private async void StartAllButton_Click(object sender, RoutedEventArgs e)
    {
        var instances = _instanceManager.GetInstances();
        if (instances.Count == 0)
        {
            ShowStatus("没有可启动的实例。", InfoBarSeverity.Warning);
            return;
        }

        ShowStatus("正在启动所有实例...", InfoBarSeverity.Informational);
        var successCount = 0;
        var failCount = 0;

        foreach (var inst in instances)
        {
            if (inst.Status == InstanceStatus.Running) continue;
            try
            {
                var success = await _instanceManager.StartInstanceAsync(inst.Id);
                if (success) successCount++;
                else failCount++;
            }
            catch
            {
                failCount++;
            }
        }

        RefreshInstanceList();
        ShowStatus($"批量启动完成：成功 {successCount} 个，失败 {failCount} 个。",
            failCount > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
    }

    private async void StopAllButton_Click(object sender, RoutedEventArgs e)
    {
        var instances = _instanceManager.GetInstances();
        if (instances.Count == 0)
        {
            ShowStatus("没有可停止的实例。", InfoBarSeverity.Warning);
            return;
        }

        ShowStatus("正在停止所有实例...", InfoBarSeverity.Informational);
        var successCount = 0;
        var failCount = 0;

        foreach (var inst in instances)
        {
            if (inst.Status != InstanceStatus.Running) continue;
            try
            {
                var success = await _instanceManager.StopInstanceAsync(inst.Id);
                if (success) successCount++;
                else failCount++;
            }
            catch
            {
                failCount++;
            }
        }

        RefreshInstanceList();
        ShowStatus($"批量停止完成：成功 {successCount} 个，失败 {failCount} 个。",
            failCount > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
    }

    private async void RestartAllButton_Click(object sender, RoutedEventArgs e)
    {
        var instances = _instanceManager.GetInstances();
        if (instances.Count == 0)
        {
            ShowStatus("没有可重启的实例。", InfoBarSeverity.Warning);
            return;
        }

        ShowStatus("正在重启所有实例...", InfoBarSeverity.Informational);
        var successCount = 0;
        var failCount = 0;

        foreach (var inst in instances)
        {
            try
            {
                var success = await _instanceManager.RestartInstanceAsync(inst.Id);
                if (success) successCount++;
                else failCount++;
            }
            catch
            {
                failCount++;
            }
        }

        RefreshInstanceList();
        ShowStatus($"批量重启完成：成功 {successCount} 个，失败 {failCount} 个。",
            failCount > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
    }

    // ── Game Update via SteamCMD ─────────────────────────────

    private async void UpdateGameButton_Click(object sender, RoutedEventArgs e)
    {
        var instance = InstanceListView.SelectedItem as Instance;
        if (instance == null)
        {
            ShowStatus("请先选择要更新的实例。", InfoBarSeverity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(instance.SteamAppId))
        {
            ShowStatus("该实例未设置 Steam App ID，无法更新。", InfoBarSeverity.Warning);
            return;
        }

        if (!int.TryParse(instance.SteamAppId, out var appId))
        {
            ShowStatus("Steam App ID 格式无效。", InfoBarSeverity.Error);
            return;
        }

        if (!_steamCmdManager.CheckInstalled())
        {
            ShowStatus("SteamCMD 未安装，请先在设置中安装。", InfoBarSeverity.Error);
            return;
        }

        var wasRunning = instance.Status == InstanceStatus.Running;
        var installDir = string.IsNullOrEmpty(instance.WorkingDirectory)
            ? Path.GetDirectoryName(instance.ProgramPath) ?? ""
            : instance.WorkingDirectory;

        try
        {
            // Stop instance if running
            if (wasRunning)
            {
                ShowStatus($"正在停止 '{instance.Name}' 以进行更新...", InfoBarSeverity.Informational);
                await _instanceManager.StopInstanceAsync(instance.Id);
                RefreshInstanceList();
            }

            ShowStatus($"正在更新 '{instance.Name}' (App ID: {appId})...", InfoBarSeverity.Informational);
            var result = await _steamCmdManager.UpdateGameAsync(appId, installDir);

            if (result.Success)
            {
                ShowStatus($"'{instance.Name}' 更新成功。", InfoBarSeverity.Success);

                // Restart if it was running before update
                if (wasRunning)
                {
                    ShowStatus($"正在重新启动 '{instance.Name}'...", InfoBarSeverity.Informational);
                    await _instanceManager.StartInstanceAsync(instance.Id);
                    ShowStatus($"'{instance.Name}' 更新并重启成功。", InfoBarSeverity.Success);
                }
            }
            else
            {
                ShowStatus($"更新失败：{result.Error}", InfoBarSeverity.Error);
            }

            RefreshInstanceList();
        }
        catch (Exception ex)
        {
            ShowStatus($"更新出错：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private static InstanceType ParseGameType(ComboBoxItem? item)
    {
        var content = item?.Content?.ToString() ?? "";
        return content switch
        {
            "Minecraft" => InstanceType.MinecraftJava,
            _ => InstanceType.Generic
        };
    }

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
