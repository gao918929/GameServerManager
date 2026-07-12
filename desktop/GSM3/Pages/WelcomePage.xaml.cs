using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using GSM3.Services;

namespace GSM3.Pages;

public sealed partial class WelcomePage : Page
{
    private readonly ConfigManager _configManager;
    private int _currentStep = 1;
    private const int TotalSteps = 4;

    public WelcomePage()
    {
        InitializeComponent();
        _configManager = ServiceLocator.GetService<ConfigManager>();
        Loaded += WelcomePage_Loaded;
    }

    private void WelcomePage_Loaded(object sender, RoutedEventArgs e)
    {
        // Load existing config values into wizard fields
        var config = _configManager.Config;
        WizardSteamCMDPath.Text = config.SteamCMD.InstallPath;
        WizardGameInstallPath.Text = config.Game.InstallPath;

        UpdateStepUI();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 2)
        {
            // Save paths before moving to step 3
            SavePaths();
        }

        if (_currentStep < TotalSteps)
        {
            _currentStep++;
            UpdateStepUI();
        }
        else
        {
            // Step 4 "完成" button -> go to dashboard
            CompleteWizard();
            Frame.Navigate(typeof(DashboardPage));
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;
            UpdateStepUI();
        }
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        CompleteWizard();
        Frame.Navigate(typeof(DashboardPage));
    }

    private void DeployNow_Click(object sender, RoutedEventArgs e)
    {
        SavePaths();
        CompleteWizard();
        Frame.Navigate(typeof(GameDeployPage));
    }

    private async void CompleteWizard()
    {
        await _configManager.UpdateConfigAsync(config =>
        {
            config.WelcomeCompleted = true;
        });
        ActivityLog.Log("初始设置向导已完成", "系统");
    }

    private async void SavePaths()
    {
        var steamPath = WizardSteamCMDPath.Text.Trim();
        var gamePath = WizardGameInstallPath.Text.Trim();

        await _configManager.UpdateConfigAsync(config =>
        {
            if (!string.IsNullOrEmpty(steamPath))
                config.SteamCMD.InstallPath = steamPath;
            if (!string.IsNullOrEmpty(gamePath))
                config.Game.InstallPath = gamePath;
        });
    }

    private void UpdateStepUI()
    {
        // Hide all step panels
        Step1Panel.Visibility = Visibility.Collapsed;
        Step2Panel.Visibility = Visibility.Collapsed;
        Step3Panel.Visibility = Visibility.Collapsed;
        Step4Panel.Visibility = Visibility.Collapsed;

        // Show current step
        switch (_currentStep)
        {
            case 1:
                Step1Panel.Visibility = Visibility.Visible;
                break;
            case 2:
                Step2Panel.Visibility = Visibility.Visible;
                break;
            case 3:
                Step3Panel.Visibility = Visibility.Visible;
                break;
            case 4:
                Step4Panel.Visibility = Visibility.Visible;
                break;
        }

        // Update back button visibility
        BackButton.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;

        // Update next button text
        NextButton.Content = _currentStep switch
        {
            3 => "跳过部署",
            4 => "前往仪表盘",
            _ => "下一步"
        };

        // Update skip button visibility (hide on last step)
        SkipButton.Visibility = _currentStep < TotalSteps ? Visibility.Visible : Visibility.Collapsed;

        // Update step indicators
        StepIndicatorText.Text = $"步骤 {_currentStep} / {TotalSteps}";
        UpdateDots();
    }

    private void UpdateDots()
    {
        var activeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue);
        var inactiveBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);

        Dot1.Fill = _currentStep >= 1 ? activeBrush : inactiveBrush;
        Dot2.Fill = _currentStep >= 2 ? activeBrush : inactiveBrush;
        Dot3.Fill = _currentStep >= 3 ? activeBrush : inactiveBrush;
        Dot4.Fill = _currentStep >= 4 ? activeBrush : inactiveBrush;
    }
}
