using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GSM3.Services;
using Windows.Storage.Pickers;

namespace GSM3.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ConfigManager _configManager;

    public SettingsPage()
    {
        InitializeComponent();
        _configManager = ServiceLocator.GetService<ConfigManager>();
        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await _configManager.InitializeAsync();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var config = _configManager.GetConfig();

        SteamCmdPathTextBox.Text = config.SteamCMD.InstallPath ?? "";
        GameInstallPathTextBox.Text = config.Game.InstallPath ?? "";

        MaxSessionsNumberBox.Value = config.Terminal.MaxSessions;
        DefaultShellComboBox.SelectedIndex = config.Terminal.DefaultShell switch
        {
            "CMD" => 1,
            _ => 0
        };

        MaxLoginAttemptsNumberBox.Value = config.Auth.MaxLoginAttempts;
        LockoutMinutesNumberBox.Value = config.Auth.LockoutDurationMinutes;

        ThemeComboBox.SelectedIndex = config.UI.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem item)
        {
            var theme = item.Content?.ToString() switch
            {
                "浅色" => ElementTheme.Light,
                "深色" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            if (XamlRoot?.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = theme;
            }
        }
    }

    private async void BrowseSteamCmdPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null)
        {
            SteamCmdPathTextBox.Text = path;
        }
    }

    private async void BrowseGameInstallPath_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null)
        {
            GameInstallPathTextBox.Text = path;
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        await _configManager.UpdateConfigAsync(config =>
        {
            config.SteamCMD.InstallPath = SteamCmdPathTextBox.Text?.Trim() ?? "";
            config.Game.InstallPath = GameInstallPathTextBox.Text?.Trim() ?? "";

            config.Terminal.MaxSessions = (int)MaxSessionsNumberBox.Value;
            config.Terminal.DefaultShell = (DefaultShellComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PowerShell";

            config.Auth.MaxLoginAttempts = (int)MaxLoginAttemptsNumberBox.Value;
            config.Auth.LockoutDurationMinutes = (int)LockoutMinutesNumberBox.Value;

            config.UI.Theme = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
            {
                "浅色" => "Light",
                "深色" => "Dark",
                _ => "Default"
            };
        });

        SaveStatusInfoBar.Message = "设置已保存";
        SaveStatusInfoBar.Severity = InfoBarSeverity.Success;
        SaveStatusInfoBar.IsOpen = true;
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
