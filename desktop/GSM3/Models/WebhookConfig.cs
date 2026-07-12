namespace GSM3.Models;

public enum WebhookType { Discord, Telegram, DingTalk, Custom }

[Flags]
public enum WebhookEvent
{
    None = 0,
    OnCrash = 1,
    OnStart = 2,
    OnStop = 4
}

public class WebhookConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public WebhookType WebhookType { get; set; } = WebhookType.Custom;
    public WebhookEvent Events { get; set; } = WebhookEvent.None;
    public bool Enabled { get; set; } = true;
    public string InstanceId { get; set; } = "";
}
