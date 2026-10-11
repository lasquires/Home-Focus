using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using HomeFocus.Shared;

namespace HomeFocus.App;

internal sealed class ParentSettingsWindow : Window
{
    readonly AgentClient agent; readonly string pin; readonly HomeFocusConfig config;
    readonly CheckBox enabled = new() { Content = "Enable HomeFocus default-deny internet filtering" };
    readonly CheckBox allowLan = new() { Content = "Allow local network (printers/NAS; less strict)" };
    readonly TextBox whitelist = MultiBox(180);
    readonly ListBox rewardList = new(); readonly TextBox rewardName = new(); readonly TextBox rewardDomains = MultiBox(120); readonly CheckBox rewardAll = new(){Content="Allow all internet while this reward is active"}; readonly TextBox rewardPrograms=MultiBox(70);
    readonly ListBox taskList = new(); readonly TextBox taskName = new(); readonly ComboBox taskKind = new(); readonly ComboBox taskReward = new(); readonly TextBox rewardMinutes=new(); readonly TextBox iceClicks=new(); readonly ComboBox taskCollection=new(); readonly TextBox passageCount=new(); readonly TextBox cooldown=new(); readonly TextBox dailyCap=new();
    readonly ListBox collectionList=new(); readonly TextBox collectionName=new(); readonly TextBox collectionPassages=MultiBox(220);
    readonly CheckBox updatesEnabled=new(){Content="Check GitHub for updates"}; readonly CheckBox autoInstall=new(){Content="Install verified updates automatically"}; readonly TextBox manifestUrl=new(); readonly TextBox updateHours=new();

    RewardGroup? SelectedReward => rewardList.SelectedItem as RewardGroup;
    TaskDefinition? SelectedTask => taskList.SelectedItem as TaskDefinition;
    PassageCollection? SelectedCollection => collectionList.SelectedItem as PassageCollection;

    public ParentSettingsWindow(AgentClient agent, HomeFocusConfig original, string pin)
    {
        this.agent=agent; this.pin=pin;
        config=JsonSerializer.Deserialize<HomeFocusConfig>(JsonSerializer.Serialize(original,JsonDefaults.Options),JsonDefaults.Options) ?? original;
        Title="HomeFocus Parent Settings"; Width=900; Height=720; MinWidth=800; MinHeight=620; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Build(); LoadConfig();
    }

    static TextBox MultiBox(double height) => new(){AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,TextWrapping=TextWrapping.Wrap,Height=height};
    static TextBlock Label(string text)=>new(){Text=text,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,4)};
    static Grid TwoCol()
    {
        var g=new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(250)}); g.ColumnDefinitions.Add(new ColumnDefinition()); return g;
    }

    void Build()
    {
        var root=new DockPanel{Margin=new Thickness(18)};
        var footer=new Grid{Margin=new Thickness(0,14,0,0)}; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var leftButtons=new StackPanel{Orientation=Orientation.Horizontal};
        var pinButton=new Button{Content="Change PIN",Padding=new Thickness(12,6,12,6)}; pinButton.Click += async (_,__)=>await ChangePinAsync();
        var checkUpdate=new Button{Content="Check Update Now",Padding=new Thickness(12,6,12,6),Margin=new Thickness(8,0,0,0)}; checkUpdate.Click += async (_,__)=>await CheckUpdateAsync();
        leftButtons.Children.Add(pinButton); leftButtons.Children.Add(checkUpdate); footer.Children.Add(leftButtons);
        var right=new StackPanel{Orientation=Orientation.Horizontal}; var cancel=new Button{Content="Cancel",MinWidth=90,Margin=new Thickness(0,0,8,0)}; cancel.Click+=(_,__)=>Close();
        var save=new Button{Content="Save",MinWidth=100,Padding=new Thickness(12,6,12,6),IsDefault=true}; save.Click+=async(_,__)=>await SaveAsync(); right.Children.Add(cancel); right.Children.Add(save); Grid.SetColumn(right,1); footer.Children.Add(right);
        DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);

        var tabs=new TabControl();
        tabs.Items.Add(new TabItem{Header="General",Content=GeneralTab()});
        tabs.Items.Add(new TabItem{Header="Rewards",Content=RewardsTab()});
        tabs.Items.Add(new TabItem{Header="Tasks",Content=TasksTab()});
        tabs.Items.Add(new TabItem{Header="Passages",Content=PassagesTab()});
        tabs.Items.Add(new TabItem{Header="Updates",Content=UpdatesTab()});
        root.Children.Add(tabs); Content=root;
    }

    UIElement GeneralTab()
    {
        var p=new StackPanel{Margin=new Thickness(18)}; p.Children.Add(enabled); p.Children.Add(allowLan); p.Children.Add(Label("Always-allowed domains (one per line)")); p.Children.Add(whitelist);
        p.Children.Add(new TextBlock{Text="HomeFocus is default-deny while enabled. Browsers use the local HomeFocus proxy; direct web traffic and VPN clients are blocked by Windows Firewall unless explicitly granted.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,0),Foreground=System.Windows.Media.Brushes.DimGray}); return p;
    }

    UIElement RewardsTab()
    {
        var g=TwoCol(); var left=new DockPanel{Margin=new Thickness(12)}; var buttons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)}; var add=new Button{Content="Add",MinWidth=70}; var del=new Button{Content="Delete",MinWidth=70,Margin=new Thickness(8,0,0,0)}; add.Click+=(_,__)=>AddReward(); del.Click+=(_,__)=>DeleteReward(); buttons.Children.Add(add);buttons.Children.Add(del);DockPanel.SetDock(buttons,Dock.Bottom);left.Children.Add(buttons); rewardList.SelectionChanged+=(_,__)=>LoadReward();left.Children.Add(rewardList);g.Children.Add(left);
        var form=new StackPanel{Margin=new Thickness(16)}; form.Children.Add(Label("Name"));form.Children.Add(rewardName);form.Children.Add(Label("Domains (one per line)"));form.Children.Add(rewardDomains);form.Children.Add(rewardAll);form.Children.Add(Label("Optional program paths allowed directly while active"));form.Children.Add(rewardPrograms); Grid.SetColumn(form,1);g.Children.Add(form); return g;
    }

    UIElement TasksTab()
    {
        var g=TwoCol(); var left=new DockPanel{Margin=new Thickness(12)}; var buttons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)}; var add=new Button{Content="Add",MinWidth=70};var del=new Button{Content="Delete",MinWidth=70,Margin=new Thickness(8,0,0,0)};add.Click+=(_,__)=>AddTask();del.Click+=(_,__)=>DeleteTask();buttons.Children.Add(add);buttons.Children.Add(del);DockPanel.SetDock(buttons,Dock.Bottom);left.Children.Add(buttons);taskList.SelectionChanged+=(_,__)=>LoadTask();left.Children.Add(taskList);g.Children.Add(left);
        taskKind.ItemsSource=Enum.GetValues<TaskKind>();
        var f=new StackPanel{Margin=new Thickness(16)};f.Children.Add(Label("Task name"));f.Children.Add(taskName);f.Children.Add(Label("Task type"));f.Children.Add(taskKind);f.Children.Add(Label("Reward group"));f.Children.Add(taskReward);f.Children.Add(Label("Reward minutes"));f.Children.Add(rewardMinutes);f.Children.Add(Label("Ice clicks (Ice tasks)"));f.Children.Add(iceClicks);f.Children.Add(Label("Passage collection (Typing tasks)"));f.Children.Add(taskCollection);f.Children.Add(Label("Passages required"));f.Children.Add(passageCount);f.Children.Add(Label("Cooldown minutes"));f.Children.Add(cooldown);f.Children.Add(Label("Daily reward cap minutes"));f.Children.Add(dailyCap);var scroll=new ScrollViewer{Content=f,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(scroll,1);g.Children.Add(scroll);return g;
    }

    UIElement PassagesTab()
    {
        var g=TwoCol(); var left=new DockPanel{Margin=new Thickness(12)};var buttons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)};var add=new Button{Content="Add",MinWidth=70};var del=new Button{Content="Delete",MinWidth=70,Margin=new Thickness(8,0,0,0)};add.Click+=(_,__)=>AddCollection();del.Click+=(_,__)=>DeleteCollection();buttons.Children.Add(add);buttons.Children.Add(del);DockPanel.SetDock(buttons,Dock.Bottom);left.Children.Add(buttons);collectionList.SelectionChanged+=(_,__)=>LoadCollection();left.Children.Add(collectionList);g.Children.Add(left);
        var f=new StackPanel{Margin=new Thickness(16)};f.Children.Add(Label("Collection name"));f.Children.Add(collectionName);f.Children.Add(Label("Passages — separate passages with a blank line"));f.Children.Add(collectionPassages);f.Children.Add(new TextBlock{Text="Scripture Passages can include Bible and Book of Mormon verses in one collection. Add or replace any passages here, using one blank line between them. Tasks randomly choose distinct passages.",TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.DimGray,Margin=new Thickness(0,10,0,0)});Grid.SetColumn(f,1);g.Children.Add(f);return g;
    }

    UIElement UpdatesTab()
    {
        var p=new StackPanel{Margin=new Thickness(18)};p.Children.Add(updatesEnabled);p.Children.Add(autoInstall);p.Children.Add(Label("Manifest URL on GitHub main"));p.Children.Add(manifestUrl);p.Children.Add(Label("Check every N hours"));p.Children.Add(updateHours);p.Children.Add(new TextBlock{Text="Recommended: keep update-manifest.json on main, but point packageUrl at a GitHub Release asset. HomeFocus verifies the package SHA-256 before installing it.",TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.DimGray,Margin=new Thickness(0,12,0,0)});return p;
    }

    void LoadConfig()
    {
        enabled.IsChecked=config.Enabled;allowLan.IsChecked=config.AllowLocalNetwork;whitelist.Text=string.Join(Environment.NewLine,config.AlwaysAllowedDomains);
        rewardList.ItemsSource=config.RewardGroups;rewardList.DisplayMemberPath="Name";taskList.ItemsSource=config.Tasks;taskList.DisplayMemberPath="Name";collectionList.ItemsSource=config.PassageCollections;collectionList.DisplayMemberPath="Name";
        updatesEnabled.IsChecked=config.Updates.Enabled;autoInstall.IsChecked=config.Updates.AutoInstall;manifestUrl.Text=config.Updates.ManifestUrl;updateHours.Text=config.Updates.CheckEveryHours.ToString();
        RefreshCombos(); if(config.RewardGroups.Count>0)rewardList.SelectedIndex=0;if(config.Tasks.Count>0)taskList.SelectedIndex=0;if(config.PassageCollections.Count>0)collectionList.SelectedIndex=0;
    }

    void RefreshCombos(){taskReward.ItemsSource=null;taskReward.ItemsSource=config.RewardGroups;taskReward.DisplayMemberPath="Name";taskCollection.ItemsSource=null;taskCollection.ItemsSource=config.PassageCollections;taskCollection.DisplayMemberPath="Name";}
    void PersistReward(){var x=SelectedReward;if(x is null)return;x.Name=rewardName.Text.Trim();x.Domains=Lines(rewardDomains.Text);x.AllowAllInternet=rewardAll.IsChecked==true;x.ProgramPaths=Lines(rewardPrograms.Text);rewardList.Items.Refresh();RefreshCombos();}
    void LoadReward(){rewardName.Text=SelectedReward?.Name??"";rewardDomains.Text=string.Join(Environment.NewLine,SelectedReward?.Domains??new());rewardAll.IsChecked=SelectedReward?.AllowAllInternet??false;rewardPrograms.Text=string.Join(Environment.NewLine,SelectedReward?.ProgramPaths??new());}
    void AddReward(){PersistReward();var x=new RewardGroup{Name="New reward"};config.RewardGroups.Add(x);rewardList.Items.Refresh();RefreshCombos();rewardList.SelectedItem=x;}
    void DeleteReward(){if(SelectedReward is not {} x)return;config.RewardGroups.Remove(x);rewardList.Items.Refresh();RefreshCombos();}

    void PersistTask(){var x=SelectedTask;if(x is null)return;x.Name=taskName.Text.Trim();x.Kind=taskKind.SelectedItem is TaskKind k?k:TaskKind.Ice;x.RewardGroupId=(taskReward.SelectedItem as RewardGroup)?.Id??"";x.RewardMinutes=Num(rewardMinutes.Text,10,1,1440);x.IceClicks=Num(iceClicks.Text,100,1,100000);x.PassageCollectionId=(taskCollection.SelectedItem as PassageCollection)?.Id??"";x.PassagesRequired=Num(passageCount.Text,1,1,100);x.CooldownMinutes=Num(cooldown.Text,0,0,10080);x.DailyRewardCapMinutes=Num(dailyCap.Text,120,0,1440);taskList.Items.Refresh();}
    void LoadTask(){var x=SelectedTask;if(x is null)return;taskName.Text=x.Name;taskKind.SelectedItem=x.Kind;taskReward.SelectedItem=config.RewardGroups.FirstOrDefault(r=>r.Id==x.RewardGroupId);rewardMinutes.Text=x.RewardMinutes.ToString();iceClicks.Text=x.IceClicks.ToString();taskCollection.SelectedItem=config.PassageCollections.FirstOrDefault(c=>c.Id==x.PassageCollectionId);passageCount.Text=x.PassagesRequired.ToString();cooldown.Text=x.CooldownMinutes.ToString();dailyCap.Text=x.DailyRewardCapMinutes.ToString();}
    void AddTask(){PersistTask();var x=new TaskDefinition{Name="New task",RewardGroupId=config.RewardGroups.FirstOrDefault()?.Id??"",PassageCollectionId=config.PassageCollections.FirstOrDefault()?.Id??""};config.Tasks.Add(x);taskList.Items.Refresh();taskList.SelectedItem=x;}
    void DeleteTask(){if(SelectedTask is not {} x)return;config.Tasks.Remove(x);taskList.Items.Refresh();}

    void PersistCollection(){var x=SelectedCollection;if(x is null)return;x.Name=collectionName.Text.Trim();x.Passages=collectionPassages.Text.Replace("\r\n","\n").Split("\n\n",StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToList();collectionList.Items.Refresh();RefreshCombos();}
    void LoadCollection(){collectionName.Text=SelectedCollection?.Name??"";collectionPassages.Text=string.Join(Environment.NewLine+Environment.NewLine,SelectedCollection?.Passages??new());}
    void AddCollection(){PersistCollection();var x=new PassageCollection{Name="New collection"};config.PassageCollections.Add(x);collectionList.Items.Refresh();RefreshCombos();collectionList.SelectedItem=x;}
    void DeleteCollection(){if(SelectedCollection is not {} x)return;config.PassageCollections.Remove(x);collectionList.Items.Refresh();RefreshCombos();}

    async Task SaveAsync()
    {
        PersistReward();PersistTask();PersistCollection();config.Enabled=enabled.IsChecked==true;config.AllowLocalNetwork=allowLan.IsChecked==true;config.AlwaysAllowedDomains=Lines(whitelist.Text);config.Updates.Enabled=updatesEnabled.IsChecked==true;config.Updates.AutoInstall=autoInstall.IsChecked==true;config.Updates.ManifestUrl=manifestUrl.Text.Trim();config.Updates.CheckEveryHours=Num(updateHours.Text,6,1,168);
        try{var r=await agent.SendAsync(new AgentRequest{Type="saveConfig",Pin=pin,Config=config});if(!r.Ok)throw new InvalidOperationException(r.Error);DialogResult=true;Close();}catch(Exception ex){MessageBox.Show(this,ex.Message,"HomeFocus",MessageBoxButton.OK,MessageBoxImage.Error);}
    }

    async Task ChangePinAsync(){var n=new PromptWindow("Change PIN","Enter a new 4–12 digit parent PIN."){Owner=this};if(n.ShowDialog()!=true)return;var r=await agent.SendAsync(new AgentRequest{Type="changePin",Pin=pin,TaskId=n.Value});MessageBox.Show(this,r.Ok?"PIN changed. Reopen Parent Settings with the new PIN.":r.Error??"Could not change PIN.");if(r.Ok)Close();}
    async Task CheckUpdateAsync(){var r=await agent.SendAsync(new AgentRequest{Type="checkUpdate",Pin=pin});MessageBox.Show(this,r.Ok?(r.Status?.AvailableVersion is {} v?$"Update {v} is available/downloaded according to your update policy.":"No newer update reported."):r.Error??"Update check failed.");}
    static List<string> Lines(string t)=>t.Replace("\r\n","\n").Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    static int Num(string s,int fallback,int min,int max)=>int.TryParse(s,out var n)?Math.Clamp(n,min,max):fallback;
}
