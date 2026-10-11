using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using HomeFocus.Shared;

namespace HomeFocus.App;

internal sealed class ParentSettingsWindow : Window
{
    readonly AgentClient agent;
    readonly string pin;
    readonly HomeFocusConfig config;

    readonly CheckBox enabled = new() { Content = "Enable HomeFocus filtering" };
    readonly CheckBox allowLan = new() { Content = "Allow local network (printers / NAS)" };
    readonly TextBox whitelist = MultiBox(190);

    readonly ListBox rewardList = new();
    readonly TextBox rewardName = new();
    readonly TextBox rewardDomains = MultiBox(150);
    readonly CheckBox rewardAll = new() { Content = "Allow all internet while this reward is active" };
    readonly TextBox rewardPrograms = MultiBox(82);

    readonly ListBox taskList = new();
    readonly TextBox taskName = new();
    readonly ComboBox taskKind = new();
    readonly ComboBox taskReward = new();
    readonly TextBox rewardMinutes = new();
    readonly TextBox iceClicks = new();
    readonly ComboBox taskCollection = new();
    readonly TextBox passageCount = new();
    readonly TextBox cooldown = new();
    readonly TextBox dailyCap = new();

    readonly ListBox collectionList = new();
    readonly TextBox collectionName = new();
    readonly TextBox collectionPassages = MultiBox(260);

    readonly CheckBox updatesEnabled = new() { Content = "Check GitHub for updates" };
    readonly CheckBox autoInstall = new() { Content = "Install verified updates automatically" };
    readonly TextBox manifestUrl = new();
    readonly TextBox updateHours = new();

    RewardGroup? editingReward;
    TaskDefinition? editingTask;
    PassageCollection? editingCollection;

    RewardGroup? SelectedReward => rewardList.SelectedItem as RewardGroup;
    TaskDefinition? SelectedTask => taskList.SelectedItem as TaskDefinition;
    PassageCollection? SelectedCollection => collectionList.SelectedItem as PassageCollection;

    public ParentSettingsWindow(AgentClient agent, HomeFocusConfig original, string pin)
    {
        this.agent = agent;
        this.pin = pin;
        config = JsonSerializer.Deserialize<HomeFocusConfig>(
            JsonSerializer.Serialize(original, JsonDefaults.Options),
            JsonDefaults.Options) ?? original;

        Title = "HomeFocus Parent Settings";
        Width = 980;
        Height = 780;
        MinWidth = 860;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Build();
        LoadConfig();
    }

    static Brush Brush(string key)
        => Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;

    static TextBox MultiBox(double height) => new()
    {
        AcceptsReturn = true,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        TextWrapping = TextWrapping.Wrap,
        Height = height
    };

    static TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, 13, 0, 6)
    };

    static TextBlock Help(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brush("TextSecondary"),
        FontSize = 12.5,
        Margin = new Thickness(0, 8, 0, 0)
    };

    static Border Card(UIElement child, Thickness? padding = null)
    {
        return new Border
        {
            Background = Brush("CardBackground"),
            BorderBrush = Brush("ControlBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = padding ?? new Thickness(20),
            Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 2,
                Opacity = 0.045,
                Color = Colors.Black
            },
            Child = child
        };
    }

    static Button SmallButton(string text, string? tag = null)
        => new()
        {
            Content = text,
            Tag = tag,
            MinWidth = 78,
            Padding = new Thickness(12, 7, 12, 7)
        };

    void Build()
    {
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(2, 0, 2, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "Parent Settings",
            FontSize = 28,
            FontWeight = FontWeights.Bold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Whitelist, rewards, tasks, passages and updates",
            Foreground = Brush("TextSecondary"),
            FontSize = 13.5,
            Margin = new Thickness(0, 4, 0, 0)
        });
        header.Children.Add(heading);

        var pinButton = SmallButton("Change PIN");
        pinButton.Click += async (_,__) => await ChangePinAsync();
        Grid.SetColumn(pinButton, 1);
        pinButton.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(pinButton);
        root.Children.Add(header);

        var tabs = new TabControl
        {
            Margin = new Thickness(0, 0, 0, 18)
        };
        tabs.Items.Add(new TabItem { Header = "General", Content = GeneralTab() });
        tabs.Items.Add(new TabItem { Header = "Rewards", Content = RewardsTab() });
        tabs.Items.Add(new TabItem { Header = "Tasks", Content = TasksTab() });
        tabs.Items.Add(new TabItem { Header = "Passages", Content = PassagesTab() });
        tabs.Items.Add(new TabItem { Header = "Updates", Content = UpdatesTab() });
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var checkUpdate = SmallButton("Check Update Now");
        checkUpdate.Click += async (_,__) => await CheckUpdateAsync();
        footer.Children.Add(checkUpdate);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = SmallButton("Cancel");
        cancel.Margin = new Thickness(0, 0, 8, 0);
        cancel.Click += (_,__) => Close();

        var save = SmallButton("Save Changes", "Primary");
        save.MinWidth = 120;
        save.IsDefault = true;
        save.Click += async (_,__) => await SaveAsync();

        actions.Children.Add(cancel);
        actions.Children.Add(save);
        Grid.SetColumn(actions, 1);
        footer.Children.Add(actions);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
    }

    UIElement GeneralTab()
    {
        var body = new StackPanel { Margin = new Thickness(2, 14, 2, 2) };

        var filtering = new StackPanel();
        filtering.Children.Add(new TextBlock
        {
            Text = "Filtering",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });
        enabled.Margin = new Thickness(0, 12, 0, 0);
        filtering.Children.Add(enabled);
        filtering.Children.Add(allowLan);
        filtering.Children.Add(Help(
            "When enabled, HomeFocus uses a default-deny Windows Firewall policy. " +
            "Approved sites and earned rewards are allowed through the local HomeFocus proxy."));
        body.Children.Add(Card(filtering));

        var always = new StackPanel();
        always.Children.Add(new TextBlock
        {
            Text = "Always available",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });
        always.Children.Add(Help("One domain per line. These sites remain available even when recreational access is locked."));
        always.Children.Add(FieldLabel("Allowed domains"));
        always.Children.Add(whitelist);

        var alwaysCard = Card(always);
        alwaysCard.Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(alwaysCard);

        return new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    UIElement RewardsTab()
    {
        var grid = SplitGrid();

        var left = new DockPanel();
        var listTitle = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        listTitle.Children.Add(new TextBlock { Text = "Reward groups", FontSize = 16, FontWeight = FontWeights.SemiBold });
        listTitle.Children.Add(Help("What becomes available when a task is completed."));
        DockPanel.SetDock(listTitle, Dock.Top);
        left.Children.Add(listTitle);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var add = SmallButton("Add", "Primary");
        var del = SmallButton("Delete", "Danger");
        del.Margin = new Thickness(8, 0, 0, 0);
        add.Click += (_,__) => AddReward();
        del.Click += (_,__) => DeleteReward();
        buttons.Children.Add(add);
        buttons.Children.Add(del);
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(buttons);

        rewardList.SelectionChanged += (_,__) =>
        {
            if (editingReward is not null) PersistRewardFields(editingReward);
            editingReward = SelectedReward;
            LoadReward();
            RefreshCombos();
        };
        left.Children.Add(rewardList);

        var leftCard = Card(left);
        leftCard.Margin = new Thickness(0, 0, 12, 0);
        grid.Children.Add(leftCard);

        var form = new StackPanel();
        form.Children.Add(new TextBlock { Text = "Reward details", FontSize = 16, FontWeight = FontWeights.SemiBold });
        form.Children.Add(FieldLabel("Name"));
        form.Children.Add(rewardName);
        form.Children.Add(FieldLabel("Domains"));
        form.Children.Add(rewardDomains);
        form.Children.Add(rewardAll);
        form.Children.Add(FieldLabel("Optional program paths"));
        form.Children.Add(rewardPrograms);
        form.Children.Add(Help("Direct program access is broader than domain-only access. Use it only for apps you trust."));

        var formScroll = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var formCard = Card(formScroll);
        formCard.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(formCard, 1);
        grid.Children.Add(formCard);

        return grid;
    }

    UIElement TasksTab()
    {
        var grid = SplitGrid();

        var left = new DockPanel();
        var listTitle = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        listTitle.Children.Add(new TextBlock { Text = "Access tasks", FontSize = 16, FontWeight = FontWeights.SemiBold });
        listTitle.Children.Add(Help("Each task exchanges effort for a specific reward duration."));
        DockPanel.SetDock(listTitle, Dock.Top);
        left.Children.Add(listTitle);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var add = SmallButton("Add", "Primary");
        var del = SmallButton("Delete", "Danger");
        del.Margin = new Thickness(8, 0, 0, 0);
        add.Click += (_,__) => AddTask();
        del.Click += (_,__) => DeleteTask();
        buttons.Children.Add(add);
        buttons.Children.Add(del);
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(buttons);

        taskList.SelectionChanged += (_,__) =>
        {
            if (editingTask is not null) PersistTaskFields(editingTask);
            editingTask = SelectedTask;
            LoadTask();
        };
        left.Children.Add(taskList);

        var leftCard = Card(left);
        leftCard.Margin = new Thickness(0, 0, 12, 0);
        grid.Children.Add(leftCard);

        taskKind.ItemsSource = Enum.GetValues<TaskKind>();

        var form = new StackPanel();
        form.Children.Add(new TextBlock { Text = "Task details", FontSize = 16, FontWeight = FontWeights.SemiBold });
        form.Children.Add(FieldLabel("Task name"));
        form.Children.Add(taskName);
        form.Children.Add(FieldLabel("Task type"));
        form.Children.Add(taskKind);
        form.Children.Add(FieldLabel("Reward group"));
        form.Children.Add(taskReward);

        var durationGrid = TwoFields(
            "Reward minutes", rewardMinutes,
            "Daily reward cap", dailyCap);
        form.Children.Add(durationGrid);

        form.Children.Add(FieldLabel("Ice clicks"));
        form.Children.Add(iceClicks);
        form.Children.Add(Help("Used only for Ice tasks."));

        form.Children.Add(FieldLabel("Passage collection"));
        form.Children.Add(taskCollection);

        var typingGrid = TwoFields(
            "Passages required", passageCount,
            "Cooldown minutes", cooldown);
        form.Children.Add(typingGrid);

        var formScroll = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var formCard = Card(formScroll);
        formCard.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(formCard, 1);
        grid.Children.Add(formCard);

        return grid;
    }

    UIElement PassagesTab()
    {
        var grid = SplitGrid();

        var left = new DockPanel();
        var listTitle = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        listTitle.Children.Add(new TextBlock { Text = "Passage collections", FontSize = 16, FontWeight = FontWeights.SemiBold });
        listTitle.Children.Add(Help("Scripture mastery, vocabulary, spelling or any custom text."));
        DockPanel.SetDock(listTitle, Dock.Top);
        left.Children.Add(listTitle);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var add = SmallButton("Add", "Primary");
        var del = SmallButton("Delete", "Danger");
        del.Margin = new Thickness(8, 0, 0, 0);
        add.Click += (_,__) => AddCollection();
        del.Click += (_,__) => DeleteCollection();
        buttons.Children.Add(add);
        buttons.Children.Add(del);
        DockPanel.SetDock(buttons, Dock.Bottom);
        left.Children.Add(buttons);

        collectionList.SelectionChanged += (_,__) =>
        {
            if (editingCollection is not null) PersistCollectionFields(editingCollection);
            editingCollection = SelectedCollection;
            LoadCollection();
        };
        left.Children.Add(collectionList);

        var leftCard = Card(left);
        leftCard.Margin = new Thickness(0, 0, 12, 0);
        grid.Children.Add(leftCard);

        var form = new StackPanel();
        form.Children.Add(new TextBlock { Text = "Collection details", FontSize = 16, FontWeight = FontWeights.SemiBold });
        form.Children.Add(FieldLabel("Collection name"));
        form.Children.Add(collectionName);
        form.Children.Add(FieldLabel("Passages"));
        form.Children.Add(Help("Separate passages with one blank line. Tasks choose them randomly."));
        form.Children.Add(collectionPassages);

        var starter = SmallButton("Load Bible + Book of Mormon examples");
        starter.Margin = new Thickness(0, 10, 0, 0);
        starter.Click += (_,__) =>
            collectionPassages.Text = string.Join(
                Environment.NewLine + Environment.NewLine,
                HomeFocusConfig.CreateDefault().PassageCollections.First(c => c.Id == "scripture").Passages);
        form.Children.Add(starter);

        var formScroll = new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var formCard = Card(formScroll);
        formCard.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(formCard, 1);
        grid.Children.Add(formCard);

        return grid;
    }

    UIElement UpdatesTab()
    {
        var form = new StackPanel { Margin = new Thickness(2, 14, 2, 2) };

        var updatePanel = new StackPanel();
        updatePanel.Children.Add(new TextBlock
        {
            Text = "Automatic updates",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });
        updatesEnabled.Margin = new Thickness(0, 12, 0, 0);
        updatePanel.Children.Add(updatesEnabled);
        updatePanel.Children.Add(autoInstall);
        updatePanel.Children.Add(FieldLabel("Manifest URL"));
        updatePanel.Children.Add(manifestUrl);
        updatePanel.Children.Add(FieldLabel("Check every N hours"));
        updatePanel.Children.Add(updateHours);
        updatePanel.Children.Add(Help(
            "HomeFocus reads update-manifest.json from GitHub main, downloads the release package, " +
            "and verifies its SHA-256 before installing it."));

        form.Children.Add(Card(updatePanel));
        return new ScrollViewer
        {
            Content = form,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    static Grid SplitGrid()
    {
        var grid = new Grid { Margin = new Thickness(2, 14, 2, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        return grid;
    }

    static Grid TwoFields(string leftLabel, Control leftControl, string rightLabel, Control rightControl)
    {
        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var left = new StackPanel();
        left.Children.Add(new TextBlock { Text = leftLabel, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        left.Children.Add(leftControl);
        grid.Children.Add(left);

        var right = new StackPanel();
        right.Children.Add(new TextBlock { Text = rightLabel, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        right.Children.Add(rightControl);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        return grid;
    }

    void LoadConfig()
    {
        enabled.IsChecked = config.Enabled;
        allowLan.IsChecked = config.AllowLocalNetwork;
        whitelist.Text = string.Join(Environment.NewLine, config.AlwaysAllowedDomains);

        rewardList.ItemsSource = config.RewardGroups;
        rewardList.DisplayMemberPath = "Name";
        taskList.ItemsSource = config.Tasks;
        taskList.DisplayMemberPath = "Name";
        collectionList.ItemsSource = config.PassageCollections;
        collectionList.DisplayMemberPath = "Name";

        updatesEnabled.IsChecked = config.Updates.Enabled;
        autoInstall.IsChecked = config.Updates.AutoInstall;
        manifestUrl.Text = config.Updates.ManifestUrl;
        updateHours.Text = config.Updates.CheckEveryHours.ToString();

        RefreshCombos();
        if (config.RewardGroups.Count > 0) rewardList.SelectedIndex = 0;
        if (config.Tasks.Count > 0) taskList.SelectedIndex = 0;
        if (config.PassageCollections.Count > 0) collectionList.SelectedIndex = 0;
    }

    void RefreshCombos()
    {
        // Keep the selected IDs stable while reward/collection names are edited.
        // Older builds reset ItemsSource and could silently save blank task links.
        var rewardId = (taskReward.SelectedItem as RewardGroup)?.Id ?? SelectedTask?.RewardGroupId;
        var collectionId = (taskCollection.SelectedItem as PassageCollection)?.Id ?? SelectedTask?.PassageCollectionId;

        if (taskReward.ItemsSource is null) taskReward.ItemsSource = config.RewardGroups;
        else taskReward.Items.Refresh();
        taskReward.DisplayMemberPath = "Name";

        if (taskCollection.ItemsSource is null) taskCollection.ItemsSource = config.PassageCollections;
        else taskCollection.Items.Refresh();
        taskCollection.DisplayMemberPath = "Name";

        taskReward.SelectedItem = config.RewardGroups.FirstOrDefault(r => r.Id == rewardId);
        taskCollection.SelectedItem = config.PassageCollections.FirstOrDefault(c => c.Id == collectionId);
    }

    void PersistRewardFields(RewardGroup x)
    {
        x.Name = rewardName.Text.Trim();
        x.Domains = Lines(rewardDomains.Text);
        x.AllowAllInternet = rewardAll.IsChecked == true;
        x.ProgramPaths = Lines(rewardPrograms.Text);
    }

    void PersistReward()
    {
        var x = SelectedReward;
        if (x is null) return;
        PersistRewardFields(x);
        rewardList.Items.Refresh();
        RefreshCombos();
    }

    void LoadReward()
    {
        rewardName.Text = SelectedReward?.Name ?? "";
        rewardDomains.Text = string.Join(Environment.NewLine, SelectedReward?.Domains ?? new());
        rewardAll.IsChecked = SelectedReward?.AllowAllInternet ?? false;
        rewardPrograms.Text = string.Join(Environment.NewLine, SelectedReward?.ProgramPaths ?? new());
    }

    void AddReward()
    {
        PersistReward();
        var x = new RewardGroup { Name = "New reward" };
        config.RewardGroups.Add(x);
        rewardList.Items.Refresh();
        RefreshCombos();
        rewardList.SelectedItem = x;
    }

    void DeleteReward()
    {
        if (SelectedReward is not { } x) return;
        config.RewardGroups.Remove(x);
        rewardList.Items.Refresh();
        RefreshCombos();
    }

    void PersistTaskFields(TaskDefinition x)
    {
        x.Name = taskName.Text.Trim();
        x.Kind = taskKind.SelectedItem is TaskKind k ? k : TaskKind.Ice;
        x.RewardGroupId = (taskReward.SelectedItem as RewardGroup)?.Id ?? x.RewardGroupId;
        x.RewardMinutes = Num(rewardMinutes.Text, 10, 1, 1440);
        x.IceClicks = Num(iceClicks.Text, 100, 1, 100000);
        x.PassageCollectionId = (taskCollection.SelectedItem as PassageCollection)?.Id ?? x.PassageCollectionId;
        x.PassagesRequired = Num(passageCount.Text, 1, 1, 100);
        x.CooldownMinutes = Num(cooldown.Text, 0, 0, 10080);
        x.DailyRewardCapMinutes = Num(dailyCap.Text, 120, 0, 1440);
    }

    void PersistTask()
    {
        var x = SelectedTask;
        if (x is null) return;
        PersistTaskFields(x);
        taskList.Items.Refresh();
    }

    void LoadTask()
    {
        var x = SelectedTask;
        if (x is null) return;

        taskName.Text = x.Name;
        taskKind.SelectedItem = x.Kind;
        taskReward.SelectedItem = config.RewardGroups.FirstOrDefault(r => r.Id == x.RewardGroupId);
        rewardMinutes.Text = x.RewardMinutes.ToString();
        iceClicks.Text = x.IceClicks.ToString();
        taskCollection.SelectedItem = config.PassageCollections.FirstOrDefault(c => c.Id == x.PassageCollectionId);
        passageCount.Text = x.PassagesRequired.ToString();
        cooldown.Text = x.CooldownMinutes.ToString();
        dailyCap.Text = x.DailyRewardCapMinutes.ToString();
    }

    void AddTask()
    {
        PersistTask();
        var x = new TaskDefinition
        {
            Name = "New task",
            RewardGroupId = config.RewardGroups.FirstOrDefault()?.Id ?? "",
            PassageCollectionId = config.PassageCollections.FirstOrDefault()?.Id ?? ""
        };
        config.Tasks.Add(x);
        taskList.Items.Refresh();
        taskList.SelectedItem = x;
    }

    void DeleteTask()
    {
        if (SelectedTask is not { } x) return;
        config.Tasks.Remove(x);
        taskList.Items.Refresh();
    }

    void PersistCollectionFields(PassageCollection x)
    {
        x.Name = collectionName.Text.Trim();
        x.Passages = collectionPassages.Text
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    void PersistCollection()
    {
        var x = SelectedCollection;
        if (x is null) return;
        PersistCollectionFields(x);
        collectionList.Items.Refresh();
        RefreshCombos();
    }

    void LoadCollection()
    {
        collectionName.Text = SelectedCollection?.Name ?? "";
        collectionPassages.Text = string.Join(
            Environment.NewLine + Environment.NewLine,
            SelectedCollection?.Passages ?? new());
    }

    void AddCollection()
    {
        PersistCollection();
        var x = new PassageCollection { Name = "New collection" };
        config.PassageCollections.Add(x);
        collectionList.Items.Refresh();
        RefreshCombos();
        collectionList.SelectedItem = x;
    }

    void DeleteCollection()
    {
        if (SelectedCollection is not { } x) return;
        config.PassageCollections.Remove(x);
        collectionList.Items.Refresh();
        RefreshCombos();
    }

    async Task SaveAsync()
    {
        PersistTask();
        PersistCollection();
        PersistReward();

        config.Enabled = enabled.IsChecked == true;
        config.AllowLocalNetwork = allowLan.IsChecked == true;
        config.AlwaysAllowedDomains = Lines(whitelist.Text);
        config.Updates.Enabled = updatesEnabled.IsChecked == true;
        config.Updates.AutoInstall = autoInstall.IsChecked == true;
        config.Updates.ManifestUrl = manifestUrl.Text.Trim();
        config.Updates.CheckEveryHours = Num(updateHours.Text, 6, 1, 168);

        try
        {
            foreach (var task in config.Tasks)
            {
                if (!config.RewardGroups.Any(g => g.Id == task.RewardGroupId))
                    throw new InvalidOperationException(
                        $"Task '{task.Name}' has no valid reward group. Select one in the Tasks tab.");

                if (task.Kind == TaskKind.Typing &&
                    !config.PassageCollections.Any(c =>
                        c.Id == task.PassageCollectionId &&
                        c.Passages.Any(p => !string.IsNullOrWhiteSpace(p))))
                    throw new InvalidOperationException(
                        $"Task '{task.Name}' needs a passage collection with at least one passage.");
            }

            var r = await agent.SendAsync(new AgentRequest
            {
                Type = "saveConfig",
                Pin = pin,
                Config = config
            });

            if (!r.Ok) throw new InvalidOperationException(r.Error);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "HomeFocus", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    async Task ChangePinAsync()
    {
        var prompt = new PromptWindow("Change PIN", "Enter a new 4–12 digit parent PIN.") { Owner = this };
        if (prompt.ShowDialog() != true) return;

        var r = await agent.SendAsync(new AgentRequest
        {
            Type = "changePin",
            Pin = pin,
            TaskId = prompt.Value
        });

        MessageBox.Show(
            this,
            r.Ok ? "PIN changed. Reopen Parent Settings with the new PIN." : r.Error ?? "Could not change PIN.");

        if (r.Ok) Close();
    }

    async Task CheckUpdateAsync()
    {
        var r = await agent.SendAsync(new AgentRequest { Type = "checkUpdate", Pin = pin });
        MessageBox.Show(
            this,
            r.Ok
                ? (r.Status?.AvailableVersion is { } v
                    ? $"Update {v} is available or downloaded according to your update policy."
                    : "No newer update reported.")
                : r.Error ?? "Update check failed.");
    }

    static List<string> Lines(string text)
        => text.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    static int Num(string text, int fallback, int min, int max)
        => int.TryParse(text, out var value) ? Math.Clamp(value, min, max) : fallback;
}
