using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HomeFocus.Shared;

namespace HomeFocus.App;

public sealed class MainWindow : Window
{
    readonly AgentClient agent = new();
    readonly StackPanel grantsPanel = new();
    readonly StackPanel tasksPanel = new();
    readonly TextBlock statusText = new();
    readonly DispatcherTimer timer;
    AgentStatus? status;

    public MainWindow()
    {
        Title = "HomeFocus"; Width = 760; Height = 720; MinWidth = 680; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(246,247,249));

        var root = new DockPanel { Margin = new Thickness(28) };
        var header = new Grid { Margin = new Thickness(0,0,0,22) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = "HomeFocus", FontSize = 30, FontWeight = FontWeights.Bold });
        statusText.Foreground = Brushes.DimGray; statusText.Margin = new Thickness(0,4,0,0); title.Children.Add(statusText);
        header.Children.Add(title);
        var parent = new Button { Content = "Parent Settings", Padding = new Thickness(15,8,15,8), MinWidth = 130 };
        parent.Click += async (_,__) => await OpenParentSettingsAsync();
        Grid.SetColumn(parent, 1); header.Children.Add(parent);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var body = new StackPanel();
        body.Children.Add(SectionTitle("Active access"));
        grantsPanel.Margin = new Thickness(0,8,0,24); body.Children.Add(grantsPanel);
        body.Children.Add(SectionTitle("Earn access"));
        tasksPanel.Margin = new Thickness(0,8,0,0); body.Children.Add(tasksPanel);
        scroll.Content = body; root.Children.Add(scroll); Content = root;

        Loaded += async (_,__) => await RefreshAsync();
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += async (_,__) => await RefreshAsync(); timer.Start();
    }

    static TextBlock SectionTitle(string text) => new() { Text = text, FontSize = 20, FontWeight = FontWeights.SemiBold };

    async Task RefreshAsync()
    {
        try
        {
            var r = await agent.SendAsync(new AgentRequest { Type = "status" });
            if (!r.Ok || r.Status is null) throw new InvalidOperationException(r.Error ?? "No status.");
            status = r.Status;
            statusText.Text = status.Enabled ? "Internet is limited to approved access" : "HomeFocus is currently unlocked";
            Render();
            if (!status.PinConfigured) await InitializePinAsync();
        }
        catch (Exception ex)
        {
            statusText.Text = "HomeFocus Agent is unavailable — internet remains fail-closed";
            grantsPanel.Children.Clear();
            tasksPanel.Children.Clear();
            tasksPanel.Children.Add(new TextBlock { Text = ex.Message, Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap });
        }
    }

    void Render()
    {
        if (status is null) return;
        grantsPanel.Children.Clear();
        if (status.ActiveGrants.Count == 0)
            grantsPanel.Children.Add(new TextBlock { Text = "No earned access is active.", Foreground = Brushes.Gray });
        foreach (var g in status.ActiveGrants)
        {
            var task = status.Tasks.Select(x => x.Task).FirstOrDefault(x => x.Id == g.SourceTaskId);
            var remain = g.ExpiresAt - DateTimeOffset.Now; if (remain < TimeSpan.Zero) remain = TimeSpan.Zero;
            var row = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Margin = new Thickness(0,0,0,8) };
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = task?.Name ?? "Earned access", FontWeight = FontWeights.SemiBold, FontSize = 15 });
            left.Children.Add(new TextBlock { Text = $"{Math.Ceiling(remain.TotalMinutes)} min remaining", Foreground = Brushes.DimGray }); grid.Children.Add(left);
            var end = new Button { Content = "End", Padding = new Thickness(12,5,12,5), Tag = g.Id };
            end.Click += async (s,__) => { if (s is Button b && b.Tag is string id) await agent.SendAsync(new AgentRequest { Type="endGrant", GrantId=id }); };
            Grid.SetColumn(end,1); grid.Children.Add(end); row.Child = grid; grantsPanel.Children.Add(row);
        }

        tasksPanel.Children.Clear();
        foreach (var ta in status.Tasks)
        {
            var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0,0,0,10) };
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = ta.Task.Name, FontSize = 17, FontWeight = FontWeights.SemiBold });
            left.Children.Add(new TextBlock { Text = $"Earn {ta.Task.RewardMinutes} min · {DescribeTask(ta.Task)}", Foreground = Brushes.DimGray, Margin = new Thickness(0,3,0,0) });
            if (!ta.Available) left.Children.Add(new TextBlock { Text = ta.Reason, Foreground = Brushes.DarkOrange, Margin = new Thickness(0,3,0,0) });
            grid.Children.Add(left);
            var go = new Button { Content = "Start", Padding = new Thickness(18,8,18,8), IsEnabled = ta.Available, Tag = ta.Task };
            go.Click += (_,__) => { if (go.Tag is TaskDefinition task) StartTask(task); };
            Grid.SetColumn(go,1); grid.Children.Add(go); card.Child=grid; tasksPanel.Children.Add(card);
        }
    }

    static string DescribeTask(TaskDefinition t) => t.Kind == TaskKind.Ice ? $"{t.IceClicks} clicks" : $"{t.PassagesRequired} passage{(t.PassagesRequired==1?"":"s")}";

    void StartTask(TaskDefinition task)
    {
        if (status is null) return;
        var runner = new TaskRunnerWindow(agent, task, status.PassageCollections) { Owner = this };
        runner.ShowDialog();
        _ = RefreshAsync();
    }

    async Task InitializePinAsync()
    {
        if (status?.PinConfigured != false) return;
        var dialog = new PromptWindow("Set Parent PIN", "Create a 4–12 digit parent PIN. This protects HomeFocus settings and cannot be replaced by a child task.") { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var r = await agent.SendAsync(new AgentRequest { Type = "initializePin", Pin = dialog.Value });
        if (!r.Ok) MessageBox.Show(this, r.Error ?? "Could not set PIN.", "HomeFocus", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    async Task OpenParentSettingsAsync()
    {
        var prompt = new PromptWindow("Parent Settings", "Enter the parent PIN.") { Owner = this };
        if (prompt.ShowDialog() != true) return;
        var r = await agent.SendAsync(new AgentRequest { Type = "getConfig", Pin = prompt.Value });
        if (!r.Ok || r.Config is null) { MessageBox.Show(this, r.Error ?? "Incorrect PIN."); return; }
        var w = new ParentSettingsWindow(agent, r.Config, prompt.Value) { Owner = this };
        w.ShowDialog(); await RefreshAsync();
    }
}
