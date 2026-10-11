using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using HomeFocus.Shared;

namespace HomeFocus.App;

public sealed class MainWindow : Window
{
    readonly AgentClient agent = new();
    readonly StackPanel grantsPanel = new();
    readonly StackPanel tasksPanel = new();
    readonly TextBlock statusText = new();
    readonly TextBlock statusPillText = new();
    readonly Border statusPill = new();
    readonly DispatcherTimer timer;
    AgentStatus? status;
    bool refreshing;
    bool parentPinPromptOpen;

    public MainWindow()
    {
        Title = "HomeFocus";
        Width = 900;
        Height = 760;
        MinWidth = 760;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new Grid { Margin = new Thickness(30) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());

        var header = BuildHeader();
        root.Children.Add(header);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 24, 0, 0)
        };
        Grid.SetRow(scroll, 1);

        var body = new StackPanel();
        body.Children.Add(SectionHeader("Active access", "Temporary access you have earned right now."));
        grantsPanel.Margin = new Thickness(0, 10, 0, 28);
        body.Children.Add(grantsPanel);

        body.Children.Add(SectionHeader("Earn access", "Choose a task to unlock one of your parent-approved rewards."));
        tasksPanel.Margin = new Thickness(0, 10, 0, 0);
        body.Children.Add(tasksPanel);

        scroll.Content = body;
        root.Children.Add(scroll);
        Content = root;

        Loaded += async (_,__) => await RefreshAsync();
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_,__) => await RefreshAsync();
        timer.Start();
    }

    UIElement BuildHeader()
    {
        var card = Card();
        card.Padding = new Thickness(22);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = "HomeFocus",
            FontSize = 32,
            FontWeight = FontWeights.Bold
        });
        statusText.Text = "Connecting to HomeFocus…";
        statusText.Foreground = Brush("TextSecondary");
        statusText.FontSize = 14;
        statusText.Margin = new Thickness(0, 4, 0, 0);
        left.Children.Add(statusText);
        grid.Children.Add(left);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        statusPill.CornerRadius = new CornerRadius(999);
        statusPill.Padding = new Thickness(12, 7, 12, 7);
        statusPill.Margin = new Thickness(0, 0, 10, 0);
        statusPill.Child = statusPillText;
        statusPillText.FontWeight = FontWeights.SemiBold;
        statusPillText.FontSize = 13;
        right.Children.Add(statusPill);

        var parent = new Button
        {
            Content = "Parent Settings",
            Tag = "Primary",
            MinWidth = 136
        };
        parent.Click += async (_,__) => await OpenParentSettingsAsync();
        right.Children.Add(parent);

        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        card.Child = grid;
        return card;
    }

    static UIElement SectionHeader(string title, string subtitle)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 13,
            Foreground = Brush("TextSecondary"),
            Margin = new Thickness(0, 3, 0, 0)
        });
        return panel;
    }

    static Border Card()
    {
        return new Border
        {
            Background = Brush("CardBackground"),
            BorderBrush = Brush("ControlBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Effect = new DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 2,
                Opacity = 0.055,
                Color = Colors.Black
            }
        };
    }

    static Brush Brush(string key)
        => Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;

    async Task RefreshAsync()
    {
        if (refreshing) return;
        refreshing = true;
        try
        {
            var r = await agent.SendAsync(new AgentRequest { Type = "status" });
            if (!r.Ok || r.Status is null) throw new InvalidOperationException(r.Error ?? "No status.");
            status = r.Status;

            // Only change this user's proxy after the background agent is confirmed reachable.
            if (status.Enabled) ProxySettings.Enable();
            else ProxySettings.Disable();

            statusText.Text = status.Enabled
                ? "Internet is limited to approved access and earned time."
                : "Filtering is currently off.";
            SetStatusPill(status.Enabled ? "Protected" : "Filtering off", status.Enabled ? "success" : "neutral");
            Render();

            if (!status.PinConfigured)
                await InitializePinAsync();
        }
        catch (Exception ex)
        {
            status = null;
            statusText.Text = "The HomeFocus background agent is unavailable.";
            SetStatusPill("Agent offline", "danger");
            grantsPanel.Children.Clear();
            tasksPanel.Children.Clear();
            tasksPanel.Children.Add(InfoCard(
                "HomeFocus needs attention",
                "The background agent did not answer. Filtering should not be enabled until this is fixed.\n\n" + ex.Message,
                "danger"));
        }
        finally { refreshing = false; }
    }

    void SetStatusPill(string text, string tone)
    {
        statusPillText.Text = text;
        switch (tone)
        {
            case "success":
                statusPill.Background = new SolidColorBrush(Color.FromRgb(232, 246, 235));
                statusPillText.Foreground = Brush("SuccessBrush");
                break;
            case "danger":
                statusPill.Background = new SolidColorBrush(Color.FromRgb(254, 236, 234));
                statusPillText.Foreground = Brush("DangerBrush");
                break;
            default:
                statusPill.Background = Brush("SubtleBrush");
                statusPillText.Foreground = Brush("TextSecondary");
                break;
        }
    }

    void Render()
    {
        if (status is null) return;

        grantsPanel.Children.Clear();
        if (status.ActiveGrants.Count == 0)
        {
            grantsPanel.Children.Add(InfoCard(
                "No earned access is active",
                "Complete one of the tasks below when you want recreational access.",
                "neutral"));
        }
        else
        {
            foreach (var grant in status.ActiveGrants)
                grantsPanel.Children.Add(GrantCard(grant));
        }

        tasksPanel.Children.Clear();
        if (status.Tasks.Count == 0)
        {
            tasksPanel.Children.Add(InfoCard(
                "No tasks configured",
                "A parent can add Ice or Typing tasks in Parent Settings.",
                "neutral"));
            return;
        }

        foreach (var availability in status.Tasks)
            tasksPanel.Children.Add(TaskCard(availability));
    }

    Border GrantCard(AccessGrant grant)
    {
        var task = status?.Tasks.Select(x => x.Task).FirstOrDefault(x => x.Id == grant.SourceTaskId);
        var remain = grant.ExpiresAt - DateTimeOffset.Now;
        if (remain < TimeSpan.Zero) remain = TimeSpan.Zero;

        var card = Card();
        card.Padding = new Thickness(18);
        card.Margin = new Thickness(0, 0, 0, 10);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = task?.Name ?? "Earned access",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold
        });
        left.Children.Add(new TextBlock
        {
            Text = RemainingText(remain),
            Foreground = Brush("TextSecondary"),
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0)
        });
        grid.Children.Add(left);

        var end = new Button
        {
            Content = "End access",
            Tag = "Danger",
            MinWidth = 104,
            VerticalAlignment = VerticalAlignment.Center
        };
        end.Click += async (_,__) =>
        {
            await agent.SendAsync(new AgentRequest { Type = "endGrant", GrantId = grant.Id });
            await RefreshAsync();
        };
        Grid.SetColumn(end, 1);
        grid.Children.Add(end);
        card.Child = grid;
        return card;
    }

    Border TaskCard(TaskAvailability availability)
    {
        var task = availability.Task;
        var card = Card();
        card.Padding = new Thickness(18);
        card.Margin = new Thickness(0, 0, 0, 10);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        left.Children.Add(new TextBlock
        {
            Text = task.Name,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });

        left.Children.Add(new TextBlock
        {
            Text = $"Earn {task.RewardMinutes} minutes  ·  {DescribeTask(task)}",
            Foreground = Brush("TextSecondary"),
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0)
        });

        if (!availability.Available)
        {
            left.Children.Add(new TextBlock
            {
                Text = availability.Reason,
                Foreground = Brush("WarningBrush"),
                FontSize = 12,
                Margin = new Thickness(0, 6, 0, 0)
            });
        }
        grid.Children.Add(left);

        var start = new Button
        {
            Content = availability.Available ? "Start" : "Unavailable",
            Tag = availability.Available ? "Primary" : null,
            MinWidth = 106,
            IsEnabled = availability.Available,
            VerticalAlignment = VerticalAlignment.Center
        };
        start.Click += (_,__) => StartTask(task);
        Grid.SetColumn(start, 1);
        grid.Children.Add(start);

        card.Child = grid;
        return card;
    }

    static Border InfoCard(string title, string message, string tone)
    {
        var card = Card();
        card.Padding = new Thickness(18);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = tone == "danger" ? Brush("DangerBrush") : Brush("TextPrimary")
        });
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("TextSecondary"),
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0)
        });
        card.Child = panel;
        return card;
    }

    static string RemainingText(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m remaining";
        return $"{Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes))} min remaining";
    }

    static string DescribeTask(TaskDefinition task)
        => task.Kind == TaskKind.Ice
            ? $"{task.IceClicks} ice clicks"
            : $"{task.PassagesRequired} passage{(task.PassagesRequired == 1 ? "" : "s")}";

    void StartTask(TaskDefinition task)
    {
        if (status is null) return;
        var runner = new TaskRunnerWindow(agent, task, status.PassageCollections) { Owner = this };
        runner.ShowDialog();
        _ = RefreshAsync();
    }

    async Task InitializePinAsync()
    {
        if (status?.PinConfigured != false || parentPinPromptOpen) return;
        parentPinPromptOpen = true;
        try
        {
            var dialog = new PromptWindow(
                "Set Parent PIN",
                "Create a 4–12 digit parent PIN. This protects HomeFocus settings.") { Owner = this };

            if (dialog.ShowDialog() != true) return;
            var r = await agent.SendAsync(new AgentRequest { Type = "initializePin", Pin = dialog.Value });
            if (!r.Ok)
                MessageBox.Show(this, r.Error ?? "Could not set PIN.", "HomeFocus", MessageBoxButton.OK, MessageBoxImage.Error);
            else if (status is not null)
                status.PinConfigured = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not set PIN: " + ex.Message, "HomeFocus", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { parentPinPromptOpen = false; }
    }

    async Task OpenParentSettingsAsync()
    {
        try
        {
            var rStatus = await agent.SendAsync(new AgentRequest { Type = "status" });
            if (!rStatus.Ok || rStatus.Status is null)
                throw new InvalidOperationException(rStatus.Error ?? "Agent did not respond.");

            status = rStatus.Status;
            if (!status.PinConfigured)
            {
                await InitializePinAsync();
                if (status?.PinConfigured != true) return;
            }

            var prompt = new PromptWindow("Parent Settings", "Enter the parent PIN.") { Owner = this };
            if (prompt.ShowDialog() != true) return;

            var r = await agent.SendAsync(new AgentRequest { Type = "getConfig", Pin = prompt.Value });
            if (!r.Ok || r.Config is null)
            {
                MessageBox.Show(this, r.Error ?? "Incorrect PIN.", "HomeFocus", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new ParentSettingsWindow(agent, r.Config, prompt.Value) { Owner = this };
            window.ShowDialog();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "HomeFocus Agent could not be reached. Parent Settings requires the background agent.\n\n" +
                "Try restarting the computer or reinstalling HomeFocus.\n\nDetails: " + ex.Message,
                "HomeFocus connection error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
