using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using GSM3.Services;
using GSM3.Models;
using Windows.Storage.Pickers;

namespace GSM3.Pages;

public sealed partial class GameDeployPage : Page
{
    private static readonly Regex SteamProgressRegex = new(
        @"progress:\s*([\d.]+)\s*\(", RegexOptions.Compiled);
    // ── Steam game data model ─────────────────────────────────
    public class SteamGameItem
    {
        public string Name { get; set; } = "";
        public string NameCN { get; set; } = "";
        public int AppId { get; set; }
        public string Tip { get; set; } = "";
        public string Platform { get; set; } = "Windows, Linux";
        public int? MemoryGB { get; set; }
        public string AppIdDisplay => $"AppID: {AppId}";
        public string PlatformDisplay => Platform;
    }

    // ── All Steam games (from original installgame.json) ──────
    private static readonly List<SteamGameItem> AllSteamGames = new()
    {
        new() { Name = "Palworld", NameCN = "幻兽帕鲁", AppId = 2394010, Platform = "Windows, Linux", MemoryGB = 4 },
        new() { Name = "SCUM", NameCN = "人渣", AppId = 3792580, Platform = "Windows", MemoryGB = 6 },
        new() { Name = "Rust", NameCN = "腐蚀", AppId = 258550, Platform = "Windows, Linux", MemoryGB = 12 },
        new() { Name = "Satisfactory", NameCN = "幸福工厂", AppId = 1690800, Platform = "Windows, Linux" },
        new() { Name = "L4D2", NameCN = "求生之路2", AppId = 222860, Platform = "Windows, Linux" },
        new() { Name = "7 Days to Die", NameCN = "七日杀", AppId = 294420, Platform = "Windows, Linux" },
        new() { Name = "Unturned", NameCN = "未转变者", AppId = 1110390, Platform = "Windows, Linux" },
        new() { Name = "Don't Starve Together", NameCN = "饥荒联机版", AppId = 343050, Platform = "Windows, Linux" },
        new() { Name = "Project Zomboid", NameCN = "僵尸毁灭工程", AppId = 380870, Platform = "Windows, Linux" },
        new() { Name = "Valheim", NameCN = "英灵神殿", AppId = 896660, Platform = "Windows, Linux" },
        new() { Name = "Team Fortress 2", NameCN = "军团要塞2", AppId = 232250, Platform = "Windows, Linux" },
        new() { Name = "Insurgency Sandstorm", NameCN = "叛乱：沙漠风暴", AppId = 581330, Platform = "Windows" },
        new() { Name = "Killing Floor 2", NameCN = "杀戮空间2", AppId = 232130, Platform = "Windows, Linux" },
        new() { Name = "ARK: Survival Evolved", NameCN = "方舟：生存进化", AppId = 376030, Platform = "Windows, Linux", MemoryGB = 8 },
        new() { Name = "ARK: Survival Ascended", NameCN = "方舟：生存飞升", AppId = 2430930, Platform = "Windows" },
        new() { Name = "Squad", NameCN = "战术小队", AppId = 403240, Platform = "Windows, Linux" },
        new() { Name = "Insurgency 2014", NameCN = "叛乱2", AppId = 237410, Platform = "Windows, Linux" },
        new() { Name = "Last Oasis", NameCN = "最后的绿洲", AppId = 920720, Platform = "Windows, Linux" },
        new() { Name = "Euro Truck Simulator 2", NameCN = "欧洲卡车模拟2", AppId = 1948160, Platform = "Windows, Linux" },
        new() { Name = "American Truck Simulator", NameCN = "美国卡车模拟", AppId = 2239530, Platform = "Windows, Linux" },
        new() { Name = "ECO", NameCN = "生态生存", AppId = 739590, Platform = "Windows, Linux" },
        new() { Name = "Soulmask", NameCN = "灵魂面甲", AppId = 3017310, Platform = "Windows" },
        new() { Name = "MORDHAU", NameCN = "雷霆一击", AppId = 629800, Platform = "Windows, Linux" },
        new() { Name = "No More Room in Hell", NameCN = "地狱已满", AppId = 317670, Platform = "Windows, Linux" },
        new() { Name = "Hurtworld", NameCN = "伤害世界", AppId = 405100, Platform = "Windows, Linux" },
        new() { Name = "Half-Life", NameCN = "半条命", AppId = 90, Platform = "Windows, Linux" },
        new() { Name = "Half-Life 2 DM", NameCN = "半条命2", AppId = 232370, Platform = "Windows, Linux" },
        new() { Name = "Starbound", NameCN = "星际边界", AppId = 533830, Platform = "Windows, Linux" },
        new() { Name = "SCP: CB Multiplayer", NameCN = "SCP:收容失效", AppId = 1801280, Platform = "Windows" },
        new() { Name = "ASTRONEER", NameCN = "异星探险家", AppId = 728470, Platform = "Windows" },
        new() { Name = "Enshrouded", NameCN = "雾锁王国", AppId = 2278520, Platform = "Windows" },
        new() { Name = "The Forest", NameCN = "森林", AppId = 556450, Platform = "Windows" },
        new() { Name = "Sons Of The Forest", NameCN = "森林之子", AppId = 2465200, Platform = "Windows" },
        new() { Name = "V Rising", NameCN = "夜族崛起", AppId = 1829350, Platform = "Windows" },
        new() { Name = "Conan Exiles", NameCN = "流放者柯南", AppId = 443030, Platform = "Windows" },
        new() { Name = "No One Survived", NameCN = "无人生还", AppId = 2329680, Platform = "Windows" },
        new() { Name = "Space Engineers", NameCN = "太空工程师", AppId = 298740, Platform = "Windows" },
        new() { Name = "TerraTech Worlds", NameCN = "泰拉科技世界", AppId = 2533070, Platform = "Windows" },
        new() { Name = "Abiotic Factor", NameCN = "非生物因素", AppId = 2857200, Platform = "Windows" },
        new() { Name = "The Isle", NameCN = "恐龙岛", AppId = 412680, Platform = "Windows" },
        new() { Name = "Arma 3", NameCN = "武装突袭3", AppId = 233780, Platform = "Windows, Linux" },
        new() { Name = "DayZ", NameCN = "僵尸末日", AppId = 223350, Platform = "Windows" },
        new() { Name = "Necesse", NameCN = "奈斯启示录", AppId = 1169370, Platform = "Windows, Linux" },
        new() { Name = "Myth of Empires", NameCN = "帝国神话", AppId = 1794810, Platform = "Windows" },
        new() { Name = "Barotrauma", NameCN = "潜渊症", AppId = 1026340, Platform = "Windows, Linux" },
        new() { Name = "Garry's Mod", NameCN = "盖瑞模组", AppId = 4020, Platform = "Windows, Linux" },
        new() { Name = "Risk of Rain 2", NameCN = "护核纪元", AppId = 1963720, Platform = "Windows, Linux" },
        new() { Name = "The Front", NameCN = "前线", AppId = 2612550, Platform = "Windows" },
        new() { Name = "Icarus", NameCN = "翼星求生", AppId = 2089300, Platform = "Windows" },
        new() { Name = "Mindustry", NameCN = "像素工厂", AppId = 1183370, Platform = "Windows, Linux" },
        new() { Name = "Craftopia", NameCN = "创世理想乡", AppId = 1670340, Platform = "Windows, Linux", MemoryGB = 8 },
        new() { Name = "Night of the Dead", NameCN = "死亡之夜", AppId = 1420710, Platform = "Windows" },
        new() { Name = "Assetto Corsa", NameCN = "神力科莎", AppId = 302550, Platform = "Windows" },
        new() { Name = "Black Mesa", NameCN = "黑山基地", AppId = 346680, Platform = "Windows, Linux" },
        new() { Name = "FOUNDRY", NameCN = "铸造厂", AppId = 2915550, Platform = "Windows" },
        new() { Name = "Avorion", NameCN = "猎户座", AppId = 565060, Platform = "Windows, Linux" },
        new() { Name = "Counter-Strike 2", NameCN = "反恐精英2", AppId = 730, Platform = "Windows, Linux" },
        new() { Name = "The Riftbreaker", NameCN = "银河破裂者", AppId = 4114030, Platform = "Windows, Linux" },
        new() { Name = "VEIN", NameCN = "静脉", AppId = 2131400, Platform = "Windows, Linux", MemoryGB = 10 },
        new() { Name = "Longvinter", NameCN = "隆冬", AppId = 1639880, Platform = "Windows, Linux" },
        new() { Name = "Smalland", NameCN = "小小世界: 原野求生", AppId = 808040, Platform = "Windows, Linux" },
        new() { Name = "Windrose", NameCN = "风启之旅", AppId = 4129620, Platform = "Windows" },
        new() { Name = "Terraria", NameCN = "泰拉瑞亚", AppId = 105600, Platform = "Windows, Linux" },
        new() { Name = "Operation Harsh Doorstop", NameCN = "行动严峻考验", AppId = 950900, Platform = "Windows, Linux" },
        new() { Name = "Outworlder", NameCN = "外部世界", AppId = 1738800, Platform = "Windows" },
    };

    private readonly ObservableCollection<SteamGameItem> _filteredSteamGames = new();

    // ── Services ──────────────────────────────────────────────
    private readonly SteamCMDManager _steamCmdManager;
    private readonly MinecraftDeployService _mcDeployService;
    private readonly MoreGamesDeployService _moreGamesDeployService;
    private readonly ModrinthService _modrinthService;
    private readonly InstanceManager _instanceManager;
    private CancellationTokenSource? _mcCts;
    private CancellationTokenSource? _modrinthCts;

    // ── Minecraft state ───────────────────────────────────────
    private List<McServerCategory> _mcCategories = new();

    // ── Modrinth state ──────────────────────────────────────
    private List<ModrinthVersion> _modrinthVersions = new();

    public GameDeployPage()
    {
        InitializeComponent();
        _steamCmdManager = ServiceLocator.GetService<SteamCMDManager>();
        _mcDeployService = ServiceLocator.GetService<MinecraftDeployService>();
        _moreGamesDeployService = ServiceLocator.GetService<MoreGamesDeployService>();
        _modrinthService = ServiceLocator.GetService<ModrinthService>();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();

        _steamCmdManager.OnOutput += (_, msg) =>
            DispatcherQueue.TryEnqueue(() =>
            {
                SteamDeployStatusText.Text = msg;
                var match = SteamProgressRegex.Match(msg);
                if (match.Success && double.TryParse(match.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct))
                {
                    SteamDeployProgress.IsIndeterminate = false;
                    SteamDeployProgress.Value = pct;
                }
            });

        _mcDeployService.OnLog += (_, msg) =>
            DispatcherQueue.TryEnqueue(() => McDeployStatusText.Text = msg);
        _mcDeployService.OnProgress += (_, pct) =>
            DispatcherQueue.TryEnqueue(() =>
            {
                McDeployProgress.IsIndeterminate = false;
                McDeployProgress.Value = pct;
            });

        _moreGamesDeployService.OnLog += (_, msg) =>
            DispatcherQueue.TryEnqueue(() => MoreGameDeployStatusText.Text = msg);
        _moreGamesDeployService.OnProgress += (_, pct) =>
            DispatcherQueue.TryEnqueue(() =>
            {
                MoreGameDeployProgress.IsIndeterminate = false;
                MoreGameDeployProgress.Value = pct;
            });

        _modrinthService.OnLog += (_, msg) =>
            DispatcherQueue.TryEnqueue(() => ModrinthDeployStatusText.Text = msg);
        _modrinthService.OnProgress += (_, pct) =>
            DispatcherQueue.TryEnqueue(() =>
            {
                ModrinthDeployProgress.IsIndeterminate = false;
                ModrinthDeployProgress.Value = pct;
            });

        Loaded += GameDeployPage_Loaded;
    }

    private async void GameDeployPage_Loaded(object sender, RoutedEventArgs e)
    {
        FilterSteamGames("");
        SteamGameListView.ItemsSource = _filteredSteamGames;

        MoreGamesListView.ItemsSource = MoreGamesDeployService.GetAvailableGames();

        UpdateSteamCmdStatus();
        await LoadMinecraftCategoriesAsync();
    }

    // ══════════════════════════════════════════════════════════
    //  STEAM TAB
    // ══════════════════════════════════════════════════════════

    private void FilterSteamGames(string query)
    {
        _filteredSteamGames.Clear();
        var q = query?.Trim().ToLowerInvariant() ?? "";

        foreach (var g in AllSteamGames)
        {
            if (string.IsNullOrEmpty(q)
                || g.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || g.NameCN.Contains(q, StringComparison.OrdinalIgnoreCase)
                || g.AppId.ToString().Contains(q))
            {
                _filteredSteamGames.Add(g);
            }
        }
    }

    private void SteamSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterSteamGames(SteamSearchBox.Text);
    }

    private void UpdateSteamCmdStatus()
    {
        if (_steamCmdManager.CheckInstalled())
        {
            SteamCmdStatus.Severity = InfoBarSeverity.Success;
            SteamCmdStatus.Message = "SteamCMD 状态: 已安装";
            InstallSteamCmdButton.Content = "更新 SteamCMD";
        }
        else
        {
            SteamCmdStatus.Severity = InfoBarSeverity.Warning;
            SteamCmdStatus.Message = "SteamCMD 状态: 未安装";
            InstallSteamCmdButton.Content = "安装 SteamCMD";
        }
    }

    private async void InstallSteamCmdButton_Click(object sender, RoutedEventArgs e)
    {
        InstallSteamCmdButton.IsEnabled = false;
        SteamDeployProgress.Visibility = Visibility.Visible;
        SteamDeployProgress.IsIndeterminate = false;
        SteamDeployStatusText.Text = "正在安装 SteamCMD...";

        var progress = new Progress<double>(v =>
            DispatcherQueue.TryEnqueue(() => SteamDeployProgress.Value = v * 100));

        var (success, error) = await _steamCmdManager.InstallAsync(progress);

        SteamDeployProgress.Visibility = Visibility.Collapsed;
        InstallSteamCmdButton.IsEnabled = true;
        SteamDeployStatusText.Text = success ? "SteamCMD 安装成功。" : $"SteamCMD 安装失败: {error}";
        UpdateSteamCmdStatus();
    }

    private async void SteamDeployButton_Click(object sender, RoutedEventArgs e)
    {
        if (SteamGameListView.SelectedItem is not SteamGameItem game)
        {
            ShowStatus("请先选择一个游戏。", InfoBarSeverity.Warning);
            return;
        }

        var installPath = SteamInstallPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(installPath))
        {
            ShowStatus("请指定安装路径。", InfoBarSeverity.Warning);
            return;
        }

        if (!_steamCmdManager.CheckInstalled())
        {
            ShowStatus("请先安装 SteamCMD。", InfoBarSeverity.Error);
            return;
        }

        SteamDeployButton.IsEnabled = false;
        SteamDeployProgress.Visibility = Visibility.Visible;
        SteamDeployProgress.IsIndeterminate = true;
        SteamDeployStatusText.Text = $"正在部署 {game.NameCN}...";

        var validate = SteamValidateCheckBox.IsChecked == true;
        var (success, error) = await _steamCmdManager.UpdateGameAsync(game.AppId, installPath, validate: validate);

        SteamDeployProgress.Visibility = Visibility.Collapsed;
        SteamDeployProgress.IsIndeterminate = false;
        SteamDeployButton.IsEnabled = true;

        if (success)
        {
            SteamDeployStatusText.Text = $"{game.NameCN} 已成功部署到 {installPath}";
            ShowStatus($"{game.NameCN} 部署成功！", InfoBarSeverity.Success);

            if (SteamAutoCreateInstanceCheckBox.IsChecked == true)
            {
                await PromptCreateInstanceAsync(game.NameCN, installPath, "");
            }
        }
        else
        {
            SteamDeployStatusText.Text = $"部署失败: {error}";
            ShowStatus($"{game.NameCN} 部署失败。", InfoBarSeverity.Error);
        }
    }

    private async void BrowseSteamPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null) SteamInstallPathBox.Text = path;
    }

    // ══════════════════════════════════════════════════════════
    //  MINECRAFT TAB
    // ══════════════════════════════════════════════════════════

    private async Task LoadMinecraftCategoriesAsync()
    {
        McLoadingRing.IsActive = true;
        McLoadingRing.Visibility = Visibility.Visible;

        var classify = await _mcDeployService.GetServerClassifyAsync();
        if (classify != null)
        {
            _mcCategories = classify.ToCategories();
            McCategoryCombo.Items.Clear();
            foreach (var cat in _mcCategories)
                McCategoryCombo.Items.Add(cat.Name);

            if (_mcCategories.Count > 0)
                McCategoryCombo.SelectedIndex = 0;
        }
        else
        {
            McDeployStatusText.Text = "无法加载 Minecraft 服务器分类，请检查网络连接后点击刷新。";
        }

        McLoadingRing.IsActive = false;
        McLoadingRing.Visibility = Visibility.Collapsed;
    }

    private async void McRefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadMinecraftCategoriesAsync();
    }

    private void McCategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        McServerTypeCombo.Items.Clear();
        McVersionCombo.Items.Clear();

        var idx = McCategoryCombo.SelectedIndex;
        if (idx < 0 || idx >= _mcCategories.Count) return;

        var cat = _mcCategories[idx];
        foreach (var st in cat.ServerTypes)
            McServerTypeCombo.Items.Add(st);

        if (cat.ServerTypes.Count > 0)
            McServerTypeCombo.SelectedIndex = 0;
    }

    private async void McServerTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        McVersionCombo.Items.Clear();
        if (McServerTypeCombo.SelectedItem is not string serverType) return;

        McLoadingRing.IsActive = true;
        McLoadingRing.Visibility = Visibility.Visible;

        var versions = await _mcDeployService.GetAvailableVersionsAsync(serverType);
        foreach (var v in versions)
            McVersionCombo.Items.Add(v);

        if (versions.Count > 0)
            McVersionCombo.SelectedIndex = 0;

        McLoadingRing.IsActive = false;
        McLoadingRing.Visibility = Visibility.Collapsed;
    }

    private async void McDeployButton_Click(object sender, RoutedEventArgs e)
    {
        if (McServerTypeCombo.SelectedItem is not string serverType)
        {
            ShowStatus("请选择服务器核心。", InfoBarSeverity.Warning);
            return;
        }
        if (McVersionCombo.SelectedItem is not string version)
        {
            ShowStatus("请选择版本。", InfoBarSeverity.Warning);
            return;
        }

        var installPath = McInstallPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(installPath))
        {
            ShowStatus("请指定安装路径。", InfoBarSeverity.Warning);
            return;
        }

        _mcCts = new CancellationTokenSource();
        McDeployButton.IsEnabled = false;
        McCancelButton.IsEnabled = true;
        McDeployProgress.Visibility = Visibility.Visible;
        McDeployProgress.IsIndeterminate = true;
        McDeployStatusText.Text = $"正在部署 {serverType} {version}...";

        var (success, error, jarPath) = await _mcDeployService.DeployJavaServerAsync(
            serverType, version, installPath, _mcCts.Token);

        McDeployProgress.Visibility = Visibility.Collapsed;
        McDeployProgress.IsIndeterminate = false;
        McDeployButton.IsEnabled = true;
        McCancelButton.IsEnabled = false;

        if (success)
        {
            var jarName = jarPath != null ? Path.GetFileName(jarPath) : $"{serverType}-{version}.jar";
            var startCmd = MinecraftDeployService.GetStartCommand(serverType, jarName);
            McDeployStatusText.Text = $"{serverType} {version} 部署完成！";
            ShowStatus($"Minecraft {serverType} {version} 部署成功！", InfoBarSeverity.Success);

            if (McAutoCreateInstanceCheckBox.IsChecked == true)
            {
                await PromptCreateInstanceAsync(
                    $"Minecraft-{serverType}-{version}", installPath, startCmd, InstanceType.MinecraftJava);
            }
        }
        else
        {
            McDeployStatusText.Text = $"部署失败: {error}";
            ShowStatus($"Minecraft 部署失败: {error}", InfoBarSeverity.Error);
        }
    }

    private void McCancelButton_Click(object sender, RoutedEventArgs e)
    {
        _mcCts?.Cancel();
        McCancelButton.IsEnabled = false;
    }

    private async void BedrockDeployButton_Click(object sender, RoutedEventArgs e)
    {
        var installPath = BedrockInstallPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(installPath))
        {
            ShowStatus("请指定基岩版安装路径。", InfoBarSeverity.Warning);
            return;
        }

        var versionType = BedrockVersionTypeCombo.SelectedIndex == 0 ? "stable" : "preview";

        BedrockDeployButton.IsEnabled = false;
        BedrockDeployProgress.Visibility = Visibility.Visible;
        BedrockDeployProgress.IsIndeterminate = true;
        BedrockDeployStatusText.Text = "正在部署基岩版服务器...";

        _mcDeployService.OnLog += BedrockLogHandler;
        _mcDeployService.OnProgress += BedrockProgressHandler;

        var (success, error) = await _mcDeployService.DeployBedrockServerAsync(installPath, versionType);

        _mcDeployService.OnLog -= BedrockLogHandler;
        _mcDeployService.OnProgress -= BedrockProgressHandler;

        BedrockDeployProgress.Visibility = Visibility.Collapsed;
        BedrockDeployProgress.IsIndeterminate = false;
        BedrockDeployButton.IsEnabled = true;

        if (success)
        {
            BedrockDeployStatusText.Text = "基岩版服务器部署完成！";
            ShowStatus("Minecraft 基岩版服务器部署成功！", InfoBarSeverity.Success);

            if (McAutoCreateInstanceCheckBox.IsChecked == true)
            {
                await PromptCreateInstanceAsync(
                    "Minecraft-Bedrock", installPath, "bedrock_server.exe", InstanceType.MinecraftBedrock);
            }
        }
        else
        {
            BedrockDeployStatusText.Text = $"部署失败: {error}";
            ShowStatus($"基岩版部署失败: {error}", InfoBarSeverity.Error);
        }
    }

    private void BedrockLogHandler(object? sender, string msg) =>
        DispatcherQueue.TryEnqueue(() => BedrockDeployStatusText.Text = msg);

    private void BedrockProgressHandler(object? sender, double pct) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            BedrockDeployProgress.IsIndeterminate = false;
            BedrockDeployProgress.Value = pct;
        });

    private async void BrowseMcPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null) McInstallPathBox.Text = path;
    }

    private async void BrowseBedrockPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null) BedrockInstallPathBox.Text = path;
    }

    // ══════════════════════════════════════════════════════════
    //  MORE GAMES TAB
    // ══════════════════════════════════════════════════════════

    private async void MoreGameDeployButton_Click(object sender, RoutedEventArgs e)
    {
        if (MoreGamesListView.SelectedItem is not MoreGameEntry game)
        {
            ShowStatus("请先选择一个游戏。", InfoBarSeverity.Warning);
            return;
        }

        var installPath = MoreGameInstallPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(installPath))
        {
            ShowStatus("请指定安装路径。", InfoBarSeverity.Warning);
            return;
        }

        MoreGameDeployButton.IsEnabled = false;
        MoreGameDeployProgress.Visibility = Visibility.Visible;
        MoreGameDeployProgress.IsIndeterminate = true;
        MoreGameDeployStatusText.Text = $"正在部署 {game.Name}...";

        (bool success, string? error) result;

        switch (game.Id)
        {
            case "tmodloader":
                result = await _moreGamesDeployService.DeployTModLoaderAsync(installPath);
                break;
            case "bedrock":
                result = await _mcDeployService.DeployBedrockServerAsync(installPath);
                break;
            default:
                result = (false, "不支持的游戏类型");
                break;
        }

        MoreGameDeployProgress.Visibility = Visibility.Collapsed;
        MoreGameDeployProgress.IsIndeterminate = false;
        MoreGameDeployButton.IsEnabled = true;

        if (result.success)
        {
            MoreGameDeployStatusText.Text = $"{game.Name} 部署完成！";
            ShowStatus($"{game.Name} 部署成功！", InfoBarSeverity.Success);

            if (MoreGameAutoCreateInstanceCheckBox.IsChecked == true)
            {
                await PromptCreateInstanceAsync(game.Name, installPath, game.StartCommand);
            }
        }
        else
        {
            MoreGameDeployStatusText.Text = $"部署失败: {result.error}";
            ShowStatus($"{game.Name} 部署失败。", InfoBarSeverity.Error);
        }
    }

    private async void BrowseMoreGamePath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null) MoreGameInstallPathBox.Text = path;
    }

    // ══════════════════════════════════════════════════════════
    //  MODRINTH TAB
    // ══════════════════════════════════════════════════════════

    private async void ModrinthSearchButton_Click(object sender, RoutedEventArgs e)
    {
        var query = ModrinthSearchBox.Text?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            ShowStatus("请输入搜索关键词。", InfoBarSeverity.Warning);
            return;
        }

        ModrinthSearchButton.IsEnabled = false;
        ModrinthSearchRing.IsActive = true;
        ModrinthSearchRing.Visibility = Visibility.Visible;
        ModrinthDeployStatusText.Text = "正在搜索...";

        var result = await _modrinthService.SearchModpacksAsync(query);

        ModrinthSearchRing.IsActive = false;
        ModrinthSearchRing.Visibility = Visibility.Collapsed;
        ModrinthSearchButton.IsEnabled = true;

        if (result?.Hits != null && result.Hits.Count > 0)
        {
            ModrinthResultsListView.ItemsSource = result.Hits;
            ModrinthDeployStatusText.Text = $"找到 {result.TotalHits} 个整合包。";
        }
        else
        {
            ModrinthResultsListView.ItemsSource = null;
            ModrinthDeployStatusText.Text = "未找到相关整合包。";
        }

        ModrinthVersionCombo.ItemsSource = null;
        _modrinthVersions.Clear();
    }

    private async void ModrinthResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModrinthResultsListView.SelectedItem is not ModrinthProject project) return;

        ModrinthVersionCombo.ItemsSource = null;
        _modrinthVersions.Clear();
        ModrinthDeployStatusText.Text = $"正在加载 {project.Title} 的版本列表...";

        var versions = await _modrinthService.GetProjectVersionsAsync(project.ProjectId);

        if (versions != null && versions.Count > 0)
        {
            _modrinthVersions = versions;
            ModrinthVersionCombo.ItemsSource = _modrinthVersions;
            ModrinthVersionCombo.SelectedIndex = 0;
            ModrinthDeployStatusText.Text = $"已加载 {versions.Count} 个版本。";
        }
        else
        {
            ModrinthDeployStatusText.Text = "未找到可用版本。";
        }
    }

    private async void ModrinthDeployButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModrinthVersionCombo.SelectedItem is not ModrinthVersion version)
        {
            ShowStatus("请选择一个版本。", InfoBarSeverity.Warning);
            return;
        }

        var installPath = ModrinthInstallPathBox.Text?.Trim();
        if (string.IsNullOrEmpty(installPath))
        {
            ShowStatus("请指定安装路径。", InfoBarSeverity.Warning);
            return;
        }

        var projectName = (ModrinthResultsListView.SelectedItem as ModrinthProject)?.Title ?? "Modpack";

        _modrinthCts = new CancellationTokenSource();
        ModrinthDeployButton.IsEnabled = false;
        ModrinthCancelButton.IsEnabled = true;
        ModrinthDeployProgress.Visibility = Visibility.Visible;
        ModrinthDeployProgress.IsIndeterminate = true;
        ModrinthDeployStatusText.Text = $"正在部署 {projectName} {version.VersionNumber}...";

        var (success, error) = await _modrinthService.DeployModpackAsync(
            version.Id, installPath, _modrinthCts.Token);

        ModrinthDeployProgress.Visibility = Visibility.Collapsed;
        ModrinthDeployProgress.IsIndeterminate = false;
        ModrinthDeployButton.IsEnabled = true;
        ModrinthCancelButton.IsEnabled = false;

        if (success)
        {
            ModrinthDeployStatusText.Text = $"{projectName} {version.VersionNumber} 部署完成！";
            ShowStatus($"Modrinth 整合包 {projectName} 部署成功！", InfoBarSeverity.Success);

            if (ModrinthAutoCreateInstanceCheckBox.IsChecked == true)
            {
                await PromptCreateInstanceAsync(
                    $"Modpack-{projectName}", installPath, "", InstanceType.MinecraftJava);
            }
        }
        else
        {
            ModrinthDeployStatusText.Text = $"部署失败: {error}";
            ShowStatus($"整合包部署失败: {error}", InfoBarSeverity.Error);
        }
    }

    private void ModrinthCancelButton_Click(object sender, RoutedEventArgs e)
    {
        _modrinthCts?.Cancel();
        ModrinthCancelButton.IsEnabled = false;
    }

    private async void BrowseModrinthPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null) ModrinthInstallPathBox.Text = path;
    }

    // ══════════════════════════════════════════════════════════
    //  INSTANCE CREATION DIALOG
    // ══════════════════════════════════════════════════════════

    private async Task PromptCreateInstanceAsync(
        string defaultName, string workDir, string startCmd, InstanceType type = InstanceType.Generic)
    {
        NewInstanceNameBox.Text = defaultName;
        NewInstanceWorkDirBox.Text = workDir;
        NewInstanceStartCmdBox.Text = startCmd;

        CreateInstanceDialog.XamlRoot = XamlRoot;
        var result = await CreateInstanceDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var name = NewInstanceNameBox.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                ShowStatus("实例名称不能为空。", InfoBarSeverity.Warning);
                return;
            }

            try
            {
                await _instanceManager.InitializeAsync();
                var instance = new Instance
                {
                    Name = name,
                    WorkingDirectory = NewInstanceWorkDirBox.Text,
                    StartCommand = NewInstanceStartCmdBox.Text,
                    Type = type
                };
                await _instanceManager.CreateInstanceAsync(instance);

                ShowStatus($"实例 \"{name}\" 创建成功！", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowStatus($"创建实例失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    // ══════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
