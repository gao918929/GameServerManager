using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GSM3.Models;
using GSM3.Services;

namespace GSM3.Pages;

public sealed partial class WebhookPage : Page
{
    private readonly WebhookService _webhookService;
    private readonly InstanceManager _instanceManager;
    private string? _editingWebhookId;

    public WebhookPage()
    {
        InitializeComponent();
        _webhookService = ServiceLocator.GetService<WebhookService>();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();
        Loaded += WebhookPage_Loaded;
    }

    private async void WebhookPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadWebhooksAsync();
    }

    private async Task LoadWebhooksAsync()
    {
        await _webhookService.InitializeAsync();
        WebhookListView.ItemsSource = null;
        WebhookListView.ItemsSource = _webhookService.Webhooks;
    }

    private async void AddWebhookButton_Click(object sender, RoutedEventArgs e)
    {
        _editingWebhookId = null;
        WebhookDialog.Title = "添加 Webhook";

        // Clear dialog fields
        DialogNameBox.Text = string.Empty;
        DialogUrlBox.Text = string.Empty;
        DialogTypeCombo.SelectedIndex = 0;
        DialogEventCrash.IsChecked = false;
        DialogEventStart.IsChecked = false;
        DialogEventStop.IsChecked = false;

        PopulateInstanceCombo();
        DialogInstanceCombo.SelectedIndex = 0;

        WebhookDialog.XamlRoot = this.XamlRoot;
        await WebhookDialog.ShowAsync();
    }

    private async void EditWebhookButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string webhookId) return;

        var webhook = _webhookService.GetWebhook(webhookId);
        if (webhook == null) return;

        _editingWebhookId = webhookId;
        WebhookDialog.Title = "编辑 Webhook";

        DialogNameBox.Text = webhook.Name;
        DialogUrlBox.Text = webhook.Url;
        DialogTypeCombo.SelectedIndex = webhook.WebhookType switch
        {
            WebhookType.Discord => 0,
            WebhookType.Telegram => 1,
            WebhookType.DingTalk => 2,
            WebhookType.Custom => 3,
            _ => 0
        };
        DialogEventCrash.IsChecked = webhook.Events.HasFlag(WebhookEvent.OnCrash);
        DialogEventStart.IsChecked = webhook.Events.HasFlag(WebhookEvent.OnStart);
        DialogEventStop.IsChecked = webhook.Events.HasFlag(WebhookEvent.OnStop);

        PopulateInstanceCombo();
        SelectInstanceInCombo(webhook.InstanceId);

        WebhookDialog.XamlRoot = this.XamlRoot;
        await WebhookDialog.ShowAsync();
    }

    private async void WebhookDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var name = DialogNameBox.Text?.Trim();
        var url = DialogUrlBox.Text?.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
        {
            args.Cancel = true;
            ShowStatus("请填写名称和 URL。", InfoBarSeverity.Warning);
            return;
        }

        var selectedType = DialogTypeCombo.SelectedIndex switch
        {
            0 => WebhookType.Discord,
            1 => WebhookType.Telegram,
            2 => WebhookType.DingTalk,
            3 => WebhookType.Custom,
            _ => WebhookType.Custom
        };

        var events = WebhookEvent.None;
        if (DialogEventCrash.IsChecked == true) events |= WebhookEvent.OnCrash;
        if (DialogEventStart.IsChecked == true) events |= WebhookEvent.OnStart;
        if (DialogEventStop.IsChecked == true) events |= WebhookEvent.OnStop;

        var selectedInstanceId = GetSelectedInstanceId();

        if (_editingWebhookId != null)
        {
            var existing = _webhookService.GetWebhook(_editingWebhookId);
            if (existing != null)
            {
                existing.Name = name;
                existing.Url = url;
                existing.WebhookType = selectedType;
                existing.Events = events;
                existing.InstanceId = selectedInstanceId;
                await _webhookService.UpdateWebhookAsync(existing);
                ShowStatus($"Webhook \"{name}\" 已更新。", InfoBarSeverity.Success);
            }
        }
        else
        {
            var config = new WebhookConfig
            {
                Name = name,
                Url = url,
                WebhookType = selectedType,
                Events = events,
                Enabled = true,
                InstanceId = selectedInstanceId
            };
            await _webhookService.AddWebhookAsync(config);
            ShowStatus($"Webhook \"{name}\" 已添加。", InfoBarSeverity.Success);
        }

        await LoadWebhooksAsync();
    }

    private async void DeleteWebhookButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string webhookId) return;

        var webhook = _webhookService.GetWebhook(webhookId);
        if (webhook == null) return;

        var dialog = new ContentDialog
        {
            Title = "确认删除",
            Content = $"确定要删除 Webhook \"{webhook.Name}\" 吗？",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _webhookService.DeleteWebhookAsync(webhookId);
            ShowStatus($"Webhook \"{webhook.Name}\" 已删除。", InfoBarSeverity.Success);
            await LoadWebhooksAsync();
        }
    }

    private async void TestWebhookButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string webhookId) return;

        var webhook = _webhookService.GetWebhook(webhookId);
        if (webhook == null) return;

        btn.IsEnabled = false;
        ShowStatus($"正在发送测试通知到 \"{webhook.Name}\"...", InfoBarSeverity.Informational);

        var (success, error) = await _webhookService.TestWebhookAsync(webhook);

        if (success)
        {
            ShowStatus($"测试通知已成功发送到 \"{webhook.Name}\"。", InfoBarSeverity.Success);
        }
        else
        {
            ShowStatus($"测试通知发送失败: {error}", InfoBarSeverity.Error);
        }

        btn.IsEnabled = true;
    }

    private async void WebhookToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle) return;
        if (toggle.DataContext is not WebhookConfig webhook) return;

        webhook.Enabled = toggle.IsOn;
        await _webhookService.UpdateWebhookAsync(webhook);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadWebhooksAsync();
        ShowStatus("已刷新 Webhook 列表。", InfoBarSeverity.Informational);
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private void PopulateInstanceCombo()
    {
        DialogInstanceCombo.Items.Clear();
        DialogInstanceCombo.Items.Add(new ComboBoxItem { Content = "全部实例", Tag = "" });

        var instances = _instanceManager.GetInstances();
        foreach (var instance in instances)
        {
            DialogInstanceCombo.Items.Add(new ComboBoxItem
            {
                Content = instance.Name,
                Tag = instance.Id
            });
        }
    }

    private void SelectInstanceInCombo(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            DialogInstanceCombo.SelectedIndex = 0;
            return;
        }

        for (var i = 0; i < DialogInstanceCombo.Items.Count; i++)
        {
            if (DialogInstanceCombo.Items[i] is ComboBoxItem item &&
                item.Tag is string tag && tag == instanceId)
            {
                DialogInstanceCombo.SelectedIndex = i;
                return;
            }
        }

        // Instance not found (deleted?), default to all
        DialogInstanceCombo.SelectedIndex = 0;
    }

    private string GetSelectedInstanceId()
    {
        if (DialogInstanceCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            return tag;
        return "";
    }

    private void InstanceLabel_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock) return;
        if (textBlock.DataContext is not WebhookConfig webhook) return;

        if (string.IsNullOrEmpty(webhook.InstanceId))
        {
            textBlock.Text = "全部实例";
        }
        else
        {
            var instance = _instanceManager.GetInstance(webhook.InstanceId);
            textBlock.Text = instance != null ? instance.Name : "未知实例";
        }
    }
}
