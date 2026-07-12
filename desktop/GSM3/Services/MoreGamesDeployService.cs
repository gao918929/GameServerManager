namespace GSM3.Services;

using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

public class MoreGamesDeployService
{
    private const string TModLoaderReleasesApi = "https://api.github.com/repos/tModLoader/tModLoader/releases/latest";

    private static readonly HttpClient Http = new();

    static MoreGamesDeployService()
    {
        if (Http.DefaultRequestHeaders.UserAgent.Count == 0)
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("GSM3-Desktop/1.0");
    }

    public event EventHandler<string>? OnLog;
    public event EventHandler<double>? OnProgress;

    public async Task<(bool Success, string? Error)> DeployTModLoaderAsync(
        string installDir, CancellationToken ct = default)
    {
        try
        {
            Log("正在获取 tModLoader 最新版本...");
            var release = await Http.GetFromJsonAsync<GitHubRelease>(TModLoaderReleasesApi, ct);
            if (release == null)
                return (false, "无法获取 tModLoader 版本信息");

            Log($"最新版本: {release.TagName}");

            var zipAsset = release.Assets?.FirstOrDefault(a =>
                a.Name != null && a.Name.Contains("tModLoader", StringComparison.OrdinalIgnoreCase)
                && a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            if (zipAsset == null || string.IsNullOrEmpty(zipAsset.BrowserDownloadUrl))
                return (false, "未找到 tModLoader 下载文件");

            Directory.CreateDirectory(installDir);
            var zipPath = Path.Combine(installDir, zipAsset.Name ?? "tModLoader.zip");

            Log($"正在下载 {zipAsset.Name}...");
            await DownloadFileAsync(zipAsset.BrowserDownloadUrl, zipPath, ct);

            Log("正在解压...");
            ZipFile.ExtractToDirectory(zipPath, installDir, overwriteFiles: true);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            Log("tModLoader 部署完成！");
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            return (false, "部署已取消");
        }
        catch (Exception ex)
        {
            return (false, $"tModLoader 部署失败: {ex.Message}");
        }
    }

    private async Task DownloadFileAsync(string url, string filePath, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength ?? -1;

        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;

            if (totalBytes > 0)
                OnProgress?.Invoke(this, (double)totalRead / totalBytes * 100);
        }
    }

    private void Log(string message)
    {
        Debug.WriteLine($"[MoreGamesDeploy] {message}");
        OnLog?.Invoke(this, message);
    }

    public static List<MoreGameEntry> GetAvailableGames()
    {
        return new List<MoreGameEntry>
        {
            new()
            {
                Id = "tmodloader",
                Name = "tModLoader",
                Description = "Terraria 模组加载器，支持 Windows/Linux/macOS",
                Category = "沙盒游戏",
                StartCommand = "start-tModLoaderServer.bat"
            },
            new()
            {
                Id = "bedrock",
                Name = "Minecraft 基岩版",
                Description = "Minecraft 基岩版专用服务器",
                Category = "沙盒游戏",
                StartCommand = "bedrock_server.exe"
            }
        };
    }
}

public class MoreGameEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string StartCommand { get; set; } = "";
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
