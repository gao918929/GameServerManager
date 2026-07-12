using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using GSM3.Services;

namespace GSM3;

public partial class App : Application
{
    private Window? _window;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool SetEnvironmentVariable(string lpName, string lpValue);

    public App()
    {
        Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
        InitializeComponent();
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ConfigManager>();
        services.AddSingleton<UserManager>();
        services.AddSingleton<InstanceManager>();
        services.AddSingleton<TerminalManager>();
        services.AddSingleton<SystemMonitor>();
        services.AddSingleton<BackupManager>();
        services.AddSingleton<SchedulerManager>();
        services.AddSingleton<FileManager>();
        services.AddSingleton<RconManager>();
        services.AddSingleton<SteamCMDManager>();
        services.AddSingleton<MinecraftDeployService>();
        services.AddSingleton<MoreGamesDeployService>();
        services.AddSingleton<ModrinthService>();
        services.AddSingleton<GameConfigService>();
        services.AddSingleton<FrpManager>();
        services.AddSingleton<ServerQueryService>();
        services.AddSingleton<WebhookService>();
        services.AddSingleton<ImportExportService>();
        services.AddSingleton<UpdateChecker>();
        services.AddSingleton<CommandTemplateService>();

        var provider = services.BuildServiceProvider();
        ServiceLocator.Initialize(provider);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    public static Window? MainAppWindow => (Current as App)?._window;
}
