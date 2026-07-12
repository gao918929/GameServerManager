using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GSM3.Services;
using GSM3.Models;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Globalization;

namespace GSM3.Pages;

public sealed partial class GameConfigEditorPage : Page
{
    private readonly GameConfigService _configService;
    private readonly InstanceManager _instanceManager;
    private GameConfigSchema? _currentSchema;
    private string? _loadedConfigFilePath;
    private Instance? _selectedInstance;
    private string? _instanceConfigFilePath;

    // Maps field key -> the UI control holding the value
    private readonly Dictionary<string, FrameworkElement> _fieldControls = new();

    public GameConfigEditorPage()
    {
        InitializeComponent();
        _configService = ServiceLocator.GetService<GameConfigService>();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();
        Loaded += GameConfigEditorPage_Loaded;
    }

    private async void GameConfigEditorPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadAvailableConfigs();
        await LoadInstancesAsync();
    }

    private async Task LoadInstancesAsync()
    {
        try
        {
            await _instanceManager.InitializeAsync();
            var instances = _instanceManager.GetInstances();
            InstanceComboBox.Items.Clear();

            foreach (var instance in instances)
            {
                InstanceComboBox.Items.Add(new ComboBoxItem
                {
                    Content = $"{instance.Name} ({instance.Type})",
                    Tag = instance.Id
                });
            }

            if (instances.Count == 0)
            {
                InstanceComboBox.PlaceholderText = "没有可用的实例";
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"加载实例列表失败: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void LoadAvailableConfigs()
    {
        var configs = _configService.GetAvailableConfigs();
        GameSelector.Items.Clear();

        foreach (var configPath in configs)
        {
            try
            {
                var schema = _configService.LoadSchema(configPath);
                GameSelector.Items.Add(new ComboBoxItem
                {
                    Content = schema.Meta.GameName,
                    Tag = configPath
                });
            }
            catch
            {
                // Skip files that fail to parse
            }
        }

        if (GameSelector.Items.Count == 0)
        {
            ShowStatus("未找到游戏配置方案文件。请确保 GameConfigs 目录中有 YAML 配置文件。", InfoBarSeverity.Warning);
        }
    }

    private void GameSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GameSelector.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string yamlPath)
        {
            try
            {
                _currentSchema = _configService.LoadSchema(yamlPath);
                _loadedConfigFilePath = null;
                BuildConfigUI(_currentSchema);

                LoadConfigButton.IsEnabled = true;
                SaveConfigButton.IsEnabled = true;
                ResetDefaultsButton.IsEnabled = true;

                ConfigFileInfoPanel.Visibility = Visibility.Visible;
                ConfigFileInfoText.Text = $"配置文件: {_currentSchema.Meta.ConfigFile}  |  解析器: {_currentSchema.Meta.Parser}";

                // Update instance config path if an instance is selected
                if (_selectedInstance != null)
                {
                    _instanceConfigFilePath = FindConfigFileInInstanceDir(_selectedInstance, _currentSchema);
                    UpdateInstanceButtons();
                }

                ShowStatus($"已加载 {_currentSchema.Meta.GameName} 的配置方案，共 {_currentSchema.Sections.Sum(s => s.Fields.Count)} 个配置项。", InfoBarSeverity.Informational);
            }
            catch (Exception ex)
            {
                ShowStatus($"加载配置方案失败: {ex.Message}", InfoBarSeverity.Error);
            }
        }
    }

    private void BuildConfigUI(GameConfigSchema schema)
    {
        FieldsContainer.Children.Clear();
        _fieldControls.Clear();

        foreach (var section in schema.Sections)
        {
            // Section header
            var sectionPanel = new StackPanel { Spacing = 12 };

            var sectionHeader = new TextBlock
            {
                Text = section.Name,
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                Margin = new Thickness(0, 8, 0, 0)
            };
            sectionPanel.Children.Add(sectionHeader);

            // Section card
            var sectionCard = new Border
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 128, 128, 128)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
            };

            var fieldsPanel = new StackPanel { Spacing = 16 };

            foreach (var field in section.Fields)
            {
                var fieldControl = CreateFieldControl(field);
                if (fieldControl != null)
                {
                    fieldsPanel.Children.Add(fieldControl);
                }
            }

            sectionCard.Child = fieldsPanel;
            sectionPanel.Children.Add(sectionCard);
            FieldsContainer.Children.Add(sectionPanel);
        }
    }

    private FrameworkElement? CreateFieldControl(GameConfigField field)
    {
        FrameworkElement control;

        switch (field.Type.ToLowerInvariant())
        {
            case "boolean":
                control = CreateBooleanField(field);
                break;
            case "number":
                control = CreateNumberField(field);
                break;
            case "select":
                control = CreateSelectField(field);
                break;
            case "string":
            default:
                control = CreateStringField(field);
                break;
        }

        _fieldControls[field.Key] = control;
        return control;
    }

    private FrameworkElement CreateStringField(GameConfigField field)
    {
        var textBox = new TextBox
        {
            Header = field.Label,
            Text = field.Default,
            PlaceholderText = string.IsNullOrEmpty(field.Default) ? "留空" : field.Default,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        ToolTipService.SetToolTip(textBox, $"{field.Label}\n键名: {field.Key}\n默认值: {(string.IsNullOrEmpty(field.Default) ? "(空)" : field.Default)}");

        return textBox;
    }

    private FrameworkElement CreateNumberField(GameConfigField field)
    {
        double defaultValue = 0;
        double.TryParse(field.Default, NumberStyles.Any, CultureInfo.InvariantCulture, out defaultValue);

        var numberBox = new NumberBox
        {
            Header = field.Label,
            Value = defaultValue,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        if (field.Min.HasValue)
            numberBox.Minimum = field.Min.Value;
        if (field.Max.HasValue)
            numberBox.Maximum = field.Max.Value;
        if (field.Step.HasValue)
            numberBox.SmallChange = field.Step.Value;
        else
        {
            // Infer step from default value: if it has a decimal, use 0.1, otherwise 1
            numberBox.SmallChange = field.Default.Contains('.') ? 0.1 : 1;
        }

        var tooltipParts = new List<string> { field.Label, $"键名: {field.Key}", $"默认值: {field.Default}" };
        if (field.Min.HasValue) tooltipParts.Add($"最小值: {field.Min.Value}");
        if (field.Max.HasValue) tooltipParts.Add($"最大值: {field.Max.Value}");
        ToolTipService.SetToolTip(numberBox, string.Join("\n", tooltipParts));

        return numberBox;
    }

    private FrameworkElement CreateBooleanField(GameConfigField field)
    {
        bool defaultVal = field.Default.Equals("true", StringComparison.OrdinalIgnoreCase);

        var container = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(0, 4, 0, 4)
        };
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = field.Label,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(label, 0);

        var toggle = new ToggleSwitch
        {
            IsOn = defaultVal,
            OnContent = "启用",
            OffContent = "禁用",
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(toggle, 1);

        container.Children.Add(label);
        container.Children.Add(toggle);

        ToolTipService.SetToolTip(container, $"{field.Label}\n键名: {field.Key}\n默认值: {(defaultVal ? "启用" : "禁用")}");

        // Store the toggle as the control, but wrap in a tagged container
        container.Tag = toggle;
        _fieldControls[field.Key] = container;

        return container;
    }

    private FrameworkElement CreateSelectField(GameConfigField field)
    {
        var comboBox = new ComboBox
        {
            Header = field.Label,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        int selectedIndex = -1;
        for (int i = 0; i < field.Options.Count; i++)
        {
            var option = field.Options[i];
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = option.Label,
                Tag = option.Value
            });
            if (option.Value.Equals(field.Default, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = i;
            }
        }

        if (selectedIndex >= 0)
            comboBox.SelectedIndex = selectedIndex;
        else if (comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;

        ToolTipService.SetToolTip(comboBox, $"{field.Label}\n键名: {field.Key}\n默认值: {field.Default}");

        return comboBox;
    }

    private Dictionary<string, string> GetCurrentValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (_currentSchema == null)
            return values;

        foreach (var section in _currentSchema.Sections)
        {
            foreach (var field in section.Fields)
            {
                if (!_fieldControls.TryGetValue(field.Key, out var control))
                    continue;

                string value = GetControlValue(field, control);
                values[field.Key] = value;
            }
        }

        return values;
    }

    private string GetControlValue(GameConfigField field, FrameworkElement control)
    {
        switch (field.Type.ToLowerInvariant())
        {
            case "boolean":
                if (control is Grid grid && grid.Tag is ToggleSwitch toggle)
                    return toggle.IsOn ? "true" : "false";
                break;
            case "number":
                if (control is NumberBox numberBox)
                {
                    if (double.IsNaN(numberBox.Value))
                        return field.Default;
                    // Preserve decimal format from default
                    if (field.Default.Contains('.'))
                        return numberBox.Value.ToString("F6", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
                    return ((long)numberBox.Value).ToString();
                }
                break;
            case "select":
                if (control is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
                    return selectedItem.Tag as string ?? field.Default;
                break;
            case "string":
            default:
                if (control is TextBox textBox)
                    return textBox.Text;
                break;
        }

        return field.Default;
    }

    private void SetControlValue(GameConfigField field, FrameworkElement control, string value)
    {
        switch (field.Type.ToLowerInvariant())
        {
            case "boolean":
                if (control is Grid grid && grid.Tag is ToggleSwitch toggle)
                    toggle.IsOn = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
            case "number":
                if (control is NumberBox numberBox)
                {
                    if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numVal))
                        numberBox.Value = numVal;
                }
                break;
            case "select":
                if (control is ComboBox comboBox)
                {
                    for (int i = 0; i < comboBox.Items.Count; i++)
                    {
                        if (comboBox.Items[i] is ComboBoxItem item &&
                            (item.Tag as string)?.Equals(value, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            comboBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
                break;
            case "string":
            default:
                if (control is TextBox textBox)
                    textBox.Text = value;
                break;
        }
    }

    private void LoadValuesIntoUI(Dictionary<string, string> values)
    {
        if (_currentSchema == null) return;

        foreach (var section in _currentSchema.Sections)
        {
            foreach (var field in section.Fields)
            {
                if (_fieldControls.TryGetValue(field.Key, out var control) &&
                    values.TryGetValue(field.Key, out var value))
                {
                    SetControlValue(field, control, value);
                }
            }
        }
    }

    private async void LoadConfig_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSchema == null)
        {
            ShowStatus("请先选择游戏配置方案。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var picker = new FileOpenPicker();

            // Get the window handle for the picker
            var window = App.MainAppWindow;
            if (window != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _loadedConfigFilePath = file.Path;
                var values = _configService.ReadConfig(file.Path, _currentSchema);
                LoadValuesIntoUI(values);
                ShowStatus($"已从 {file.Name} 加载配置。", InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"加载配置文件失败: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSchema == null)
        {
            ShowStatus("请先选择游戏配置方案。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var picker = new FileSavePicker();

            var window = App.MainAppWindow;
            if (window != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.SuggestedFileName = Path.GetFileName(_currentSchema.Meta.ConfigFile);

            // Determine file extension from the config file name
            var ext = Path.GetExtension(_currentSchema.Meta.ConfigFile);
            if (string.IsNullOrEmpty(ext)) ext = ".cfg";
            picker.FileTypeChoices.Add("配置文件", new List<string> { ext });

            // If we previously loaded a file, suggest saving to same location
            if (!string.IsNullOrEmpty(_loadedConfigFilePath))
            {
                picker.SuggestedFileName = Path.GetFileName(_loadedConfigFilePath);
            }

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var values = GetCurrentValues();
                _configService.SaveConfig(file.Path, _currentSchema, values);
                _loadedConfigFilePath = file.Path;
                ShowStatus($"配置已保存到 {file.Name}。", InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"保存配置文件失败: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSchema == null)
        {
            ShowStatus("请先选择游戏配置方案。", InfoBarSeverity.Warning);
            return;
        }

        // Reset all fields to schema defaults
        foreach (var section in _currentSchema.Sections)
        {
            foreach (var field in section.Fields)
            {
                if (_fieldControls.TryGetValue(field.Key, out var control))
                {
                    SetControlValue(field, control, field.Default);
                }
            }
        }

        ShowStatus("所有配置项已恢复为默认值。", InfoBarSeverity.Informational);
    }

    private void InstanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (InstanceComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string instanceId)
        {
            _selectedInstance = _instanceManager.GetInstance(instanceId);
            if (_selectedInstance == null) return;

            // Try to auto-select matching schema
            AutoSelectSchemaForInstance(_selectedInstance);

            // Scan for config files in the instance directory
            _instanceConfigFilePath = FindConfigFileInInstanceDir(_selectedInstance, _currentSchema);

            UpdateInstanceButtons();
        }
        else
        {
            _selectedInstance = null;
            _instanceConfigFilePath = null;
            UpdateInstanceButtons();
        }
    }

    private void AutoSelectSchemaForInstance(Instance instance)
    {
        for (int i = 0; i < GameSelector.Items.Count; i++)
        {
            if (GameSelector.Items[i] is ComboBoxItem item && item.Content is string gameName)
            {
                if (IsSchemaMatchForInstanceType(gameName, instance.Type))
                {
                    if (GameSelector.SelectedIndex != i)
                    {
                        GameSelector.SelectedIndex = i;
                    }
                    return;
                }
            }
        }
    }

    private static bool IsSchemaMatchForInstanceType(string gameName, InstanceType type)
    {
        var nameL = gameName.ToLowerInvariant();
        return type switch
        {
            InstanceType.MinecraftJava => nameL.Contains("minecraft java") || nameL == "我的世界_java",
            InstanceType.MinecraftBedrock => nameL.Contains("基岩") || nameL.Contains("bedrock"),
            _ => false
        };
    }

    private string? FindConfigFileInInstanceDir(Instance instance, GameConfigSchema? schema)
    {
        if (string.IsNullOrEmpty(instance.WorkingDirectory) || !Directory.Exists(instance.WorkingDirectory))
            return null;

        // If we have a schema, look for the specific config file
        if (schema != null)
        {
            var configPath = Path.Combine(instance.WorkingDirectory, schema.Meta.ConfigFile);
            if (File.Exists(configPath))
                return configPath;
        }

        // Type-based detection
        switch (instance.Type)
        {
            case InstanceType.MinecraftJava:
            case InstanceType.MinecraftBedrock:
                var serverProps = Path.Combine(instance.WorkingDirectory, "server.properties");
                if (File.Exists(serverProps))
                    return serverProps;
                break;
            case InstanceType.Generic:
                foreach (var pattern in new[] { "*.properties", "*.cfg", "*.ini" })
                {
                    var files = Directory.GetFiles(instance.WorkingDirectory, pattern);
                    if (files.Length > 0)
                        return files[0];
                }
                break;
        }

        return null;
    }

    private void UpdateInstanceButtons()
    {
        bool hasInstance = _selectedInstance != null;
        bool hasSchema = _currentSchema != null;

        LoadInstanceConfigButton.IsEnabled = hasInstance && hasSchema;
        SaveToInstanceButton.IsEnabled = hasInstance && hasSchema;

        if (hasInstance && _instanceConfigFilePath != null)
        {
            ShowStatus($"在实例目录中找到配置文件: {Path.GetFileName(_instanceConfigFilePath)}", InfoBarSeverity.Informational);
        }
        else if (hasInstance && hasSchema && _instanceConfigFilePath == null)
        {
            ShowStatus("实例目录中未找到配置文件，保存时将创建新文件。", InfoBarSeverity.Informational);
        }
    }

    private void LoadInstanceConfig_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSchema == null || _selectedInstance == null)
        {
            ShowStatus("请先选择实例和游戏配置方案。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var configPath = _instanceConfigFilePath
                ?? Path.Combine(_selectedInstance.WorkingDirectory, _currentSchema.Meta.ConfigFile);

            if (!File.Exists(configPath))
            {
                ShowStatus($"配置文件不存在: {configPath}", InfoBarSeverity.Warning);
                return;
            }

            _loadedConfigFilePath = configPath;
            var values = _configService.ReadConfig(configPath, _currentSchema);
            LoadValuesIntoUI(values);
            ShowStatus($"已从实例 \"{_selectedInstance.Name}\" 加载配置: {Path.GetFileName(configPath)}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"加载实例配置失败: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void SaveToInstance_Click(object sender, RoutedEventArgs e)
    {
        if (_currentSchema == null || _selectedInstance == null)
        {
            ShowStatus("请先选择实例和游戏配置方案。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var configPath = _instanceConfigFilePath
                ?? Path.Combine(_selectedInstance.WorkingDirectory, _currentSchema.Meta.ConfigFile);

            var values = GetCurrentValues();
            _configService.SaveConfig(configPath, _currentSchema, values);
            _loadedConfigFilePath = configPath;
            _instanceConfigFilePath = configPath;
            ShowStatus($"配置已保存到实例 \"{_selectedInstance.Name}\": {Path.GetFileName(configPath)}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"保存实例配置失败: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
