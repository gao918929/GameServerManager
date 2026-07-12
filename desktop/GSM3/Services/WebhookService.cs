namespace GSM3.Services;

using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using GSM3.Models;

public class WebhookService
{
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GSM3");
    private static readonly string WebhooksPath = Path.Combine(DataDir, "webhooks.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly object _listLock = new();
    private List<WebhookConfig> _webhooks = new();

    public List<WebhookConfig> Webhooks
    {
        get { lock (_listLock) { return _webhooks.ToList(); } }
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(DataDir);

        if (File.Exists(WebhooksPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(WebhooksPath);
                var webhooks = JsonSerializer.Deserialize<List<WebhookConfig>>(json, JsonOptions);
                if (webhooks != null)
                    _webhooks = webhooks;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load webhooks: {ex.Message}");
            }
        }
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(DataDir);
            var json = JsonSerializer.Serialize(_webhooks, JsonOptions);
            await File.WriteAllTextAsync(WebhooksPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save webhooks: {ex.Message}");
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task AddWebhookAsync(WebhookConfig config)
    {
        config.Id = Guid.NewGuid().ToString();
        lock (_listLock) { _webhooks.Add(config); }
        await SaveAsync();
    }

    public async Task UpdateWebhookAsync(WebhookConfig config)
    {
        lock (_listLock)
        {
            var index = _webhooks.FindIndex(w => w.Id == config.Id);
            if (index >= 0)
                _webhooks[index] = config;
        }
        await SaveAsync();
    }

    public async Task DeleteWebhookAsync(string id)
    {
        lock (_listLock) { _webhooks.RemoveAll(w => w.Id == id); }
        await SaveAsync();
    }

    public WebhookConfig? GetWebhook(string id)
    {
        lock (_listLock) { return _webhooks.FirstOrDefault(w => w.Id == id); }
    }

    public async Task<(bool Success, string? Error)> SendNotificationAsync(
        string title, string message, WebhookType type, string url)
    {
        try
        {
            string jsonPayload = type switch
            {
                WebhookType.Discord => JsonSerializer.Serialize(new
                {
                    content = $"**{title}**\n{message}"
                }),
                WebhookType.Telegram => JsonSerializer.Serialize(new
                {
                    text = $"{title}\n{message}"
                }),
                WebhookType.DingTalk => JsonSerializer.Serialize(new
                {
                    msgtype = "text",
                    text = new { content = $"{title}\n{message}" }
                }),
                WebhookType.Custom => JsonSerializer.Serialize(new
                {
                    title,
                    message,
                    timestamp = DateTime.UtcNow.ToString("o")
                }),
                _ => JsonSerializer.Serialize(new { title, message })
            };

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
                return (true, null);

            var responseBody = await response.Content.ReadAsStringAsync();
            return (false, $"HTTP {(int)response.StatusCode}: {responseBody}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? Error)> TestWebhookAsync(WebhookConfig config)
    {
        return await SendNotificationAsync(
            "GSM3 测试通知",
            $"Webhook \"{config.Name}\" 测试成功！\n时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            config.WebhookType,
            config.Url);
    }

    public void SubscribeToEvents(InstanceManager instanceManager)
    {
        instanceManager.OnInstanceStatusChanged += async (_, e) =>
        {
            WebhookEvent? eventType = null;
            string? title = null;
            string? message = null;

            switch (e.NewStatus)
            {
                case InstanceStatus.Running when e.PreviousStatus == InstanceStatus.Starting:
                    eventType = WebhookEvent.OnStart;
                    title = "服务器已启动";
                    message = $"实例 {e.InstanceId} 已成功启动。";
                    break;
                case InstanceStatus.Stopped when e.PreviousStatus == InstanceStatus.Stopping:
                    eventType = WebhookEvent.OnStop;
                    title = "服务器已停止";
                    message = $"实例 {e.InstanceId} 已停止。";
                    break;
                case InstanceStatus.Crashed:
                    eventType = WebhookEvent.OnCrash;
                    title = "服务器崩溃";
                    message = $"实例 {e.InstanceId} 已崩溃！请检查服务器状态。";
                    break;
            }

            if (eventType == null || title == null || message == null)
                return;

            // Try to get the instance name for a better message
            try
            {
                var im = ServiceLocator.TryGetService<InstanceManager>();
                var instance = im?.GetInstance(e.InstanceId);
                if (instance != null && !string.IsNullOrEmpty(instance.Name))
                {
                    message = message.Replace(e.InstanceId, $"{instance.Name} ({e.InstanceId})");
                }
            }
            catch { /* ignore */ }

            List<WebhookConfig> snapshot;
            lock (_listLock) { snapshot = _webhooks.ToList(); }

            foreach (var webhook in snapshot)
            {
                if (!webhook.Enabled) continue;
                if (!webhook.Events.HasFlag(eventType.Value)) continue;

                // Instance filtering: skip if webhook targets a specific instance
                // that doesn't match the event's instance
                if (!string.IsNullOrEmpty(webhook.InstanceId) &&
                    webhook.InstanceId != e.InstanceId)
                    continue;

                try
                {
                    await SendNotificationAsync(title, message, webhook.WebhookType, webhook.Url);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to send webhook '{webhook.Name}': {ex.Message}");
                }
            }
        };
    }
}
