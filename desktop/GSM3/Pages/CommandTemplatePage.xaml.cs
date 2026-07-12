using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using GSM3.Services;

namespace GSM3.Pages;

public sealed partial class CommandTemplatePage : Page
{
    private readonly CommandTemplateService _templateService;
    private readonly InstanceManager _instanceManager;

    public CommandTemplatePage()
    {
        InitializeComponent();
        _templateService = ServiceLocator.GetService<CommandTemplateService>();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();
        Loaded += CommandTemplatePage_Loaded;
    }

    private async void CommandTemplatePage_Loaded(object sender, RoutedEventArgs e)
    {
        await _templateService.InitializeAsync();
        RefreshTemplateList();
    }

    private void RefreshTemplateList()
    {
        TemplateListPanel.Children.Clear();

        var templates = _templateService.Templates;
        var grouped = templates
            .GroupBy(t => t.Category)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            // Category header
            var categoryHeader = new TextBlock
            {
                Text = group.Key,
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                Margin = new Thickness(0, 8, 0, 4)
            };
            TemplateListPanel.Children.Add(categoryHeader);

            foreach (var template in group)
            {
                var card = CreateTemplateCard(template);
                TemplateListPanel.Children.Add(card);
            }
        }

        if (!templates.Any())
        {
            var emptyText = new TextBlock
            {
                Text = "暂无命令模板，点击「新建模板」创建一个。",
                Foreground = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            };
            TemplateListPanel.Children.Add(emptyText);
        }
    }

    private Border CreateTemplateCard(CommandTemplate template)
    {
        var card = new Border
        {
            Padding = new Thickness(16),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 128, 128, 128)),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left side: template info
        var infoPanel = new StackPanel { Spacing = 4 };

        var nameText = new TextBlock
        {
            Text = template.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 16
        };
        infoPanel.Children.Add(nameText);

        if (!string.IsNullOrEmpty(template.Description))
        {
            var descText = new TextBlock
            {
                Text = template.Description,
                Foreground = new SolidColorBrush(Colors.Gray),
                TextWrapping = TextWrapping.Wrap
            };
            infoPanel.Children.Add(descText);
        }

        var metaPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 4, 0, 0) };

        var commandCountText = new TextBlock
        {
            Text = $"{template.Commands.Count} 条命令",
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.Gray)
        };
        metaPanel.Children.Add(commandCountText);

        if (template.IsBuiltIn)
        {
            var builtInBadge = new Border
            {
                Background = new SolidColorBrush(ColorHelper.FromArgb(30, 0, 120, 212)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2)
            };
            builtInBadge.Child = new TextBlock
            {
                Text = "内置",
                FontSize = 11,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 212))
            };
            metaPanel.Children.Add(builtInBadge);
        }

        infoPanel.Children.Add(metaPanel);
        Grid.SetColumn(infoPanel, 0);
        mainGrid.Children.Add(infoPanel);

        // Right side: action buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var viewButton = new Button { Content = "查看" };
        viewButton.Click += (_, _) => ShowTemplateDetails(template);
        buttonPanel.Children.Add(viewButton);

        var executeButton = new Button
        {
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };
        var execContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        execContent.Children.Add(new FontIcon { Glyph = "", FontSize = 14 });
        execContent.Children.Add(new TextBlock { Text = "执行" });
        executeButton.Content = execContent;
        executeButton.Click += async (_, _) => await ExecuteTemplate(template);
        buttonPanel.Children.Add(executeButton);

        if (!template.IsBuiltIn)
        {
            var editButton = new Button();
            editButton.Content = new FontIcon { Glyph = "", FontSize = 14 };
            editButton.Click += async (_, _) => await EditTemplate(template);
            buttonPanel.Children.Add(editButton);

            var deleteButton = new Button();
            deleteButton.Content = new FontIcon { Glyph = "", FontSize = 14 };
            deleteButton.Click += async (_, _) => await DeleteTemplate(template);
            buttonPanel.Children.Add(deleteButton);
        }

        Grid.SetColumn(buttonPanel, 1);
        mainGrid.Children.Add(buttonPanel);

        card.Child = mainGrid;
        return card;
    }

    private async void ShowTemplateDetails(CommandTemplate template)
    {
        var commandList = string.Join("\n", template.Commands.Select((c, i) => $"{i + 1}. {c}"));

        var content = new StackPanel { Spacing = 12, MinWidth = 400 };
        content.Children.Add(new TextBlock
        {
            Text = template.Description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Colors.Gray)
        });
        content.Children.Add(new TextBlock
        {
            Text = $"分类: {template.Category}",
            FontSize = 13,
            Foreground = new SolidColorBrush(Colors.Gray)
        });
        content.Children.Add(new TextBlock
        {
            Text = "命令列表:",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 0)
        });

        var commandBox = new TextBox
        {
            Text = string.Join("\n", template.Commands),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120,
            FontFamily = new FontFamily("Consolas")
        };
        content.Children.Add(commandBox);

        var dialog = new ContentDialog
        {
            Title = template.Name,
            Content = content,
            CloseButtonText = "关闭",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async Task ExecuteTemplate(CommandTemplate template)
    {
        var instances = _instanceManager.GetInstances()
            .Where(i => i.Status == Models.InstanceStatus.Running)
            .ToList();

        if (!instances.Any())
        {
            await new ContentDialog
            {
                Title = "无可用实例",
                Content = "没有正在运行的实例。请先启动一个实例后再执行命令模板。",
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            }.ShowAsync();
            return;
        }

        // Let user pick an instance
        var instanceCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "选择目标实例"
        };
        foreach (var inst in instances)
        {
            instanceCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{inst.Name} ({inst.Id[..8]}...)",
                Tag = inst.Id
            });
        }
        if (instances.Count == 1)
            instanceCombo.SelectedIndex = 0;

        var content = new StackPanel { Spacing = 12, MinWidth = 400 };
        content.Children.Add(new TextBlock { Text = "选择要执行命令的目标实例:" });
        content.Children.Add(instanceCombo);
        content.Children.Add(new TextBlock
        {
            Text = $"将依次发送 {template.Commands.Count} 条命令",
            Foreground = new SolidColorBrush(Colors.Gray),
            FontSize = 13
        });

        // Show commands preview
        var previewBox = new TextBox
        {
            Text = string.Join("\n", template.Commands),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            FontFamily = new FontFamily("Consolas")
        };
        content.Children.Add(previewBox);

        var dialog = new ContentDialog
        {
            Title = $"执行: {template.Name}",
            Content = content,
            PrimaryButtonText = "执行",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        if (instanceCombo.SelectedItem is not ComboBoxItem selected || selected.Tag is not string instanceId)
        {
            await new ContentDialog
            {
                Title = "未选择实例",
                Content = "请选择一个目标实例。",
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            }.ShowAsync();
            return;
        }

        // Execute commands
        int sentCount = 0;
        int skipCount = 0;
        foreach (var command in template.Commands)
        {
            // Skip comment lines (delay hints)
            if (command.TrimStart().StartsWith('#'))
            {
                skipCount++;
                continue;
            }

            var sent = _instanceManager.SendInput(instanceId, command);
            if (sent)
                sentCount++;
        }

        await new ContentDialog
        {
            Title = "执行完成",
            Content = $"已发送 {sentCount} 条命令到实例。" +
                      (skipCount > 0 ? $"\n跳过 {skipCount} 条注释行。" : ""),
            CloseButtonText = "确定",
            XamlRoot = XamlRoot
        }.ShowAsync();
    }

    private async void AddTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowTemplateEditorDialog(null);
    }

    private async Task EditTemplate(CommandTemplate template)
    {
        await ShowTemplateEditorDialog(template);
    }

    private async Task ShowTemplateEditorDialog(CommandTemplate? existing)
    {
        bool isNew = existing == null;

        var nameBox = new TextBox
        {
            PlaceholderText = "模板名称",
            Text = existing?.Name ?? "",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var descBox = new TextBox
        {
            PlaceholderText = "模板描述",
            Text = existing?.Description ?? "",
            Margin = new Thickness(0, 0, 0, 8)
        };

        var categoryBox = new ComboBox
        {
            IsEditable = true,
            PlaceholderText = "分类",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };

        // Add existing categories
        var categories = _templateService.GetCategories();
        foreach (var cat in categories)
        {
            categoryBox.Items.Add(cat);
        }
        if (existing != null)
        {
            categoryBox.Text = existing.Category;
        }
        else if (categories.Any())
        {
            categoryBox.SelectedIndex = 0;
        }

        var commandsBox = new TextBox
        {
            PlaceholderText = "每行一条命令\n以 # 开头的行为注释/延迟提示",
            Text = existing != null ? string.Join("\n", existing.Commands) : "",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 160,
            FontFamily = new FontFamily("Consolas")
        };

        var content = new StackPanel { Spacing = 4, MinWidth = 450 };
        content.Children.Add(new TextBlock { Text = "名称:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(nameBox);
        content.Children.Add(new TextBlock { Text = "描述:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(descBox);
        content.Children.Add(new TextBlock { Text = "分类:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(categoryBox);
        content.Children.Add(new TextBlock { Text = "命令:", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(commandsBox);

        var dialog = new ContentDialog
        {
            Title = isNew ? "新建命令模板" : "编辑命令模板",
            Content = content,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var name = nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await new ContentDialog
            {
                Title = "错误",
                Content = "模板名称不能为空。",
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            }.ShowAsync();
            return;
        }

        var commands = commandsBox.Text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.TrimEnd())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (!commands.Any())
        {
            await new ContentDialog
            {
                Title = "错误",
                Content = "请至少输入一条命令。",
                CloseButtonText = "确定",
                XamlRoot = XamlRoot
            }.ShowAsync();
            return;
        }

        var category = categoryBox.Text?.Trim();
        if (string.IsNullOrEmpty(category))
            category = "通用";

        if (isNew)
        {
            var newTemplate = new CommandTemplate
            {
                Name = name,
                Description = descBox.Text.Trim(),
                Category = category,
                Commands = commands,
                IsBuiltIn = false
            };
            await _templateService.AddTemplateAsync(newTemplate);
        }
        else
        {
            existing!.Name = name;
            existing.Description = descBox.Text.Trim();
            existing.Category = category;
            existing.Commands = commands;
            await _templateService.UpdateTemplateAsync(existing);
        }

        RefreshTemplateList();
    }

    private async Task DeleteTemplate(CommandTemplate template)
    {
        var dialog = new ContentDialog
        {
            Title = "确认删除",
            Content = $"确定要删除模板「{template.Name}」吗？此操作不可撤销。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _templateService.DeleteTemplateAsync(template.Id);
            RefreshTemplateList();
        }
    }
}
