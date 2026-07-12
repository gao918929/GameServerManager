using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using GSM3.Pages;
using GSM3.Services;
using GSM3.Models;

namespace GSM3;

public sealed partial class MainWindow : Window
{
    private readonly UserManager _userManager;
    private readonly ConfigManager _configManager;
    private User? _currentUser;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        _userManager = ServiceLocator.GetService<UserManager>();
        _configManager = ServiceLocator.GetService<ConfigManager>();
        Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        await _configManager.InitializeAsync();
        LoadSettingsToUI();
        ApplySavedTheme();
        ActivityLog.Log("GSM3 已启动", "系统");
    }

    private void LoadSettingsToUI()
    {
        var config = _configManager.Config;
        PortBox.Value = config.Server.Port;
        HostBox.Text = config.Server.Host;
        MaxSessionsBox.Value = config.Terminal.MaxSessions;
        SessionTimeoutBox.Value = config.Terminal.TimeoutMinutes;
        GamePathBox.Text = config.Game.InstallPath;
        SteamCMDPathBox.Text = config.SteamCMD.InstallPath;
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        ShowLoginOrMain();
    }

    private async void ShowLoginOrMain()
    {
        await _userManager.InitializeAsync();

        if (!_userManager.HasUsers())
        {
            ShowRegisterFirst();
        }
        else
        {
            ShowLogin();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsCard.Visibility = SettingsCard.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        await _configManager.UpdateConfigAsync(config =>
        {
            config.Server.Port = (int)PortBox.Value;
            config.Server.Host = HostBox.Text.Trim();
            config.Terminal.MaxSessions = (int)MaxSessionsBox.Value;
            config.Terminal.TimeoutMinutes = (int)SessionTimeoutBox.Value;
            config.Game.InstallPath = GamePathBox.Text.Trim();
            config.SteamCMD.InstallPath = SteamCMDPathBox.Text.Trim();
        });

        SettingsCard.Visibility = Visibility.Collapsed;
        StartInfoBar.Severity = InfoBarSeverity.Success;
        StartInfoBar.Message = "设置已保存";
        StartInfoBar.IsOpen = true;
    }

    private void CancelSettings_Click(object sender, RoutedEventArgs e)
    {
        LoadSettingsToUI();
        SettingsCard.Visibility = Visibility.Collapsed;
    }

    private void ShowLogin()
    {
        StartPanel.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Visible;
        NavView.Visibility = Visibility.Collapsed;
        RegisterLink.Content = "没有账号？点击注册";
        LoginButton.Content = "登录";
        LoginButton.Tag = "login";
        LoginError.IsOpen = false;
        LoadSavedCredentials();
    }

    private void ShowRegisterFirst()
    {
        StartPanel.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Visible;
        NavView.Visibility = Visibility.Collapsed;
        LoginButton.Content = "注册管理员账号";
        LoginButton.Tag = "register";
        RegisterLink.Visibility = Visibility.Collapsed;
        LoginError.IsOpen = false;
    }

    private void ShowMainUI()
    {
        StartPanel.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Collapsed;
        NavView.Visibility = Visibility.Visible;
        AppTitleBar.Title = $"GSM3 - {_currentUser?.Username ?? ""}";

        // If first launch (welcome not completed), redirect to welcome wizard
        if (!_configManager.Config.WelcomeCompleted)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                NavFrame.Navigate(typeof(WelcomePage));
            });
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = LoginUsername.Text.Trim();
        var password = LoginPassword.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            LoginError.Message = "请输入用户名和密码";
            LoginError.IsOpen = true;
            return;
        }

        if (LoginButton.Tag as string == "register")
        {
            var (success, error, user) = await _userManager.RegisterAsync(username, password, UserRole.Admin);
            if (success)
            {
                _currentUser = user;
                await SaveCredentialsIfChecked(username, password);
                ShowMainUI();
            }
            else
            {
                LoginError.Message = error ?? "注册失败";
                LoginError.IsOpen = true;
            }
        }
        else
        {
            var (success, error, user) = await _userManager.LoginAsync(username, password);
            if (success)
            {
                _currentUser = user;
                await SaveCredentialsIfChecked(username, password);
                ShowMainUI();
            }
            else
            {
                LoginError.Message = error ?? "登录失败";
                LoginError.IsOpen = true;
            }
        }
    }

    private void LoginPassword_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            LoginButton_Click(sender, e);
    }

    private void RegisterLink_Click(object sender, RoutedEventArgs e)
    {
        if (LoginButton.Tag as string == "login")
        {
            LoginButton.Content = "注册";
            LoginButton.Tag = "register";
            RegisterLink.Content = "已有账号？点击登录";
        }
        else
        {
            LoginButton.Content = "登录";
            LoginButton.Tag = "login";
            RegisterLink.Content = "没有账号？点击注册";
        }
    }

    private void BackToStart_Click(object sender, RoutedEventArgs e)
    {
        LoginPanel.Visibility = Visibility.Collapsed;
        StartPanel.Visibility = Visibility.Visible;
        LoginUsername.Text = "";
        LoginPassword.Password = "";
        LoginError.IsOpen = false;
        RegisterLink.Visibility = Visibility.Visible;
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        NavFrame.GoBack();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag as string;
            var pageType = tag switch
            {
                "dashboard" => typeof(DashboardPage),
                "instances" => typeof(InstancesPage),
                "rcon" => typeof(RconPage),
                "playermanage" => typeof(PlayerManagePage),
                "serverstatus" => typeof(ServerStatusPage),
                "terminal" => typeof(TerminalPage),
                "logs" => typeof(LogViewerPage),
                "files" => typeof(FilesPage),
                "deploy" => typeof(GameDeployPage),
                "gameconfig" => typeof(GameConfigEditorPage),
                "scheduler" => typeof(SchedulerPage),
                "backup" => typeof(BackupPage),
                "system" => typeof(SystemPage),
                "environment" => typeof(EnvironmentPage),
                "frp" => typeof(FrpPage),
                "webhook" => typeof(WebhookPage),
                "cmdtemplate" => typeof(CommandTemplatePage),
                "users" => typeof(UsersPage),
                "about" => typeof(AboutPage),
                _ => typeof(DashboardPage)
            };
            NavFrame.Navigate(pageType);
        }
    }

    public User? CurrentUser => _currentUser;

    public void Logout()
    {
        _currentUser = null;
        StartPanel.Visibility = Visibility.Visible;
        LoginPanel.Visibility = Visibility.Collapsed;
        NavView.Visibility = Visibility.Collapsed;
        LoginUsername.Text = "";
        LoginPassword.Password = "";
    }

    private void ApplySavedTheme()
    {
        var theme = _configManager.Config.UI.Theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme;
        }
    }

    private void LoadSavedCredentials()
    {
        var cred = _configManager.Config.SavedCredentials;
        if (cred.RememberMe && !string.IsNullOrEmpty(cred.Username))
        {
            LoginUsername.Text = cred.Username;
            try
            {
                if (!string.IsNullOrEmpty(cred.PasswordBase64))
                    LoginPassword.Password = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cred.PasswordBase64));
            }
            catch { }
            RememberMeCheck.IsChecked = true;
        }
        else
        {
            RememberMeCheck.IsChecked = false;
        }
    }

    private async Task SaveCredentialsIfChecked(string username, string password)
    {
        var remember = RememberMeCheck.IsChecked == true;
        await _configManager.UpdateConfigAsync(config =>
        {
            config.SavedCredentials.RememberMe = remember;
            if (remember)
            {
                config.SavedCredentials.Username = username;
                config.SavedCredentials.PasswordBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
            }
            else
            {
                config.SavedCredentials.Username = "";
                config.SavedCredentials.PasswordBase64 = "";
            }
        });
    }
}
