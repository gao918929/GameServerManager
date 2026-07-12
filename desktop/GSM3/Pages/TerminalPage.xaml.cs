using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using GSM3.Models;
using GSM3.Services;

namespace GSM3.Pages;

public sealed partial class TerminalPage : Page
{
    private readonly TerminalManager _terminalManager;
    private readonly InstanceManager _instanceManager;
    private readonly Dictionary<string, TerminalTabState> _tabStates = new();
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private int _sessionCounter;
    private string? _activeSessionId;

    public TerminalPage()
    {
        InitializeComponent();
        _terminalManager = ServiceLocator.GetService<TerminalManager>();
        _instanceManager = ServiceLocator.GetService<InstanceManager>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _terminalManager.OnOutput += TerminalManager_OnOutput;
        _terminalManager.OnSessionClosed += TerminalManager_OnSessionClosed;

        await LoadInstancesAsync();

        // Create initial tab
        if (TerminalTabView.TabItems.Count == 0)
        {
            CreateNewTab();
        }
    }

    private async Task LoadInstancesAsync()
    {
        try
        {
            await _instanceManager.InitializeAsync();
            var instances = _instanceManager.GetInstances();
            foreach (var instance in instances)
            {
                InstanceSelector.Items.Add(new ComboBoxItem
                {
                    Content = instance.Name,
                    Tag = instance
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load instances for terminal: {ex.Message}");
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _terminalManager.OnOutput -= TerminalManager_OnOutput;
        _terminalManager.OnSessionClosed -= TerminalManager_OnSessionClosed;
    }

    private void CreateNewTab()
    {
        _sessionCounter++;

        string? workingDirectory = null;
        string name;

        if (InstanceSelector.SelectedItem is ComboBoxItem item && item.Tag is Instance instance)
        {
            workingDirectory = instance.WorkingDirectory;
            name = instance.Name;
        }
        else
        {
            name = $"会话 {_sessionCounter}";
        }

        var session = _terminalManager.CreateSession(name, workingDirectory);

        // Create terminal output UI for this tab
        var outputBlock = new TextBlock
        {
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas, Courier New, monospace"),
            FontSize = 14,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 204, 204, 204)),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            Text = $"GSM3 终端就绪 - {name}\n"
        };

        var scrollViewer = new ScrollViewer
        {
            Padding = new Thickness(12),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = outputBlock
        };

        var border = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 12, 12, 12)),
            CornerRadius = new CornerRadius(8),
            Child = scrollViewer
        };

        var tabItem = new TabViewItem
        {
            Header = name,
            Content = border,
            Tag = session.Id,
            IconSource = new SymbolIconSource { Symbol = Symbol.Document }
        };

        var tabState = new TerminalTabState
        {
            SessionId = session.Id,
            Tab = tabItem,
            OutputBlock = outputBlock,
            ScrollViewer = scrollViewer,
            Output = new StringBuilder($"GSM3 终端就绪 - {name}\n")
        };

        _tabStates[session.Id] = tabState;
        TerminalTabView.TabItems.Add(tabItem);
        TerminalTabView.SelectedItem = tabItem;
        _activeSessionId = session.Id;
    }

    private void TerminalManager_OnOutput(object? sender, TerminalOutputEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_tabStates.TryGetValue(e.SessionId, out var state))
            {
                state.Output.AppendLine(e.Data);
                state.OutputBlock.Text = state.Output.ToString();

                // Auto-scroll to bottom
                state.ScrollViewer.UpdateLayout();
                state.ScrollViewer.ChangeView(null, state.ScrollViewer.ScrollableHeight, null);
            }
        });
    }

    private void TerminalManager_OnSessionClosed(object? sender, string sessionId)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_tabStates.TryGetValue(sessionId, out var state))
            {
                state.Output.AppendLine("\n[会话已结束]");
                state.OutputBlock.Text = state.Output.ToString();
            }
        });
    }

    private void SendCommand()
    {
        var command = CommandInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(command) || _activeSessionId == null)
            return;

        // Add to command history (avoid duplicating the last entry)
        if (_commandHistory.Count == 0 || _commandHistory[^1] != command)
        {
            _commandHistory.Add(command);
            if (_commandHistory.Count > 100)
                _commandHistory.RemoveAt(0);
        }
        _historyIndex = _commandHistory.Count;
        UpdateHistoryList();

        // Send to the active PowerShell session
        _terminalManager.SendInput(_activeSessionId, command);

        CommandInput.Text = string.Empty;
        CommandInput.Focus(FocusState.Programmatic);
    }

    private void UpdateHistoryList()
    {
        var reversed = _commandHistory.AsEnumerable().Reverse().Take(50).ToList();
        HistoryList.ItemsSource = reversed;
        HistoryEmptyText.Visibility = reversed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Tab event handlers ───────────────────────────────────────

    private void TabView_AddTabButtonClick(TabView sender, object args)
    {
        CreateNewTab();
    }

    private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        var sessionId = args.Tab.Tag as string;
        if (sessionId != null)
        {
            _terminalManager.CloseSession(sessionId);
            _tabStates.Remove(sessionId);
        }
        sender.TabItems.Remove(args.Tab);

        // If all tabs are closed, create a fresh one
        if (sender.TabItems.Count == 0)
        {
            CreateNewTab();
        }
    }

    private void TabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TerminalTabView.SelectedItem is TabViewItem selected)
        {
            _activeSessionId = selected.Tag as string;
        }
    }

    // ── Input event handlers ─────────────────────────────────────

    private void CommandInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            SendCommand();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Up)
        {
            if (_commandHistory.Count > 0 && _historyIndex > 0)
            {
                _historyIndex--;
                CommandInput.Text = _commandHistory[_historyIndex];
                CommandInput.SelectionStart = CommandInput.Text.Length;
            }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Down)
        {
            if (_historyIndex < _commandHistory.Count - 1)
            {
                _historyIndex++;
                CommandInput.Text = _commandHistory[_historyIndex];
                CommandInput.SelectionStart = CommandInput.Text.Length;
            }
            else
            {
                _historyIndex = _commandHistory.Count;
                CommandInput.Text = "";
            }
            e.Handled = true;
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SendCommand();
    }

    private void ClearScreenButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeSessionId != null && _tabStates.TryGetValue(_activeSessionId, out var state))
        {
            state.Output.Clear();
            state.OutputBlock.Text = "";
        }
    }

    private void QuickCommand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string command && _activeSessionId != null)
        {
            if (_commandHistory.Count == 0 || _commandHistory[^1] != command)
            {
                _commandHistory.Add(command);
            }
            _historyIndex = _commandHistory.Count;
            UpdateHistoryList();

            _terminalManager.SendInput(_activeSessionId, command);
        }
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is string command)
        {
            CommandInput.Text = command;
            CommandInput.Focus(FocusState.Programmatic);
            CommandInput.SelectionStart = command.Length;
            HistoryFlyout.Hide();
        }
    }

    // ── Internal state ───────────────────────────────────────────

    private sealed class TerminalTabState
    {
        public string SessionId { get; set; } = "";
        public TabViewItem Tab { get; set; } = null!;
        public TextBlock OutputBlock { get; set; } = null!;
        public ScrollViewer ScrollViewer { get; set; } = null!;
        public StringBuilder Output { get; set; } = new();
    }
}
