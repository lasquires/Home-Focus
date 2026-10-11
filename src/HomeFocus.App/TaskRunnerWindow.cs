using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using HomeFocus.Shared;

namespace HomeFocus.App;

internal sealed class TaskRunnerWindow : Window
{
    readonly AgentClient agent; readonly TaskDefinition task; readonly IReadOnlyList<PassageCollection> collections;
    int clicks; int passageIndex; string typed = ""; List<string> passages = new();
    readonly StackPanel root = new();

    public TaskRunnerWindow(AgentClient agent, TaskDefinition task, IReadOnlyList<PassageCollection> collections)
    {
        this.agent=agent; this.task=task; this.collections=collections;
        Title=task.Name; Width=680; Height=560; WindowStartupLocation=WindowStartupLocation.CenterOwner; ResizeMode=ResizeMode.CanMinimize;
        Content=root; root.Margin=new Thickness(28); Loaded += (_,__) => Build();
    }

    void Build()
    {
        root.Children.Clear();
        root.Children.Add(new TextBlock { Text=task.Name, FontSize=28, FontWeight=FontWeights.Bold, Margin=new Thickness(0,0,0,6) });
        root.Children.Add(new TextBlock { Text=$"Complete this to earn {task.RewardMinutes} minutes.", Foreground=Brushes.DimGray, Margin=new Thickness(0,0,0,22) });
        if (task.Kind==TaskKind.Ice) BuildIce(); else BuildTyping();
    }

    void BuildIce()
    {
        var progress=new ProgressBar { Minimum=0, Maximum=Math.Max(1,task.IceClicks), Value=clicks, Height=16, Margin=new Thickness(0,0,0,16) };
        var count=new TextBlock { Text=$"{clicks} / {task.IceClicks}", HorizontalAlignment=HorizontalAlignment.Center, FontSize=18, FontWeight=FontWeights.SemiBold, Margin=new Thickness(0,0,0,15) };
        var ice=new Button { Height=300, FontSize=34, Background=new SolidColorBrush(Color.FromRgb(210,239,248)), BorderBrush=new SolidColorBrush(Color.FromRgb(115,190,215)) };
        void Paint() { var pct=Math.Min(1.0, clicks/(double)Math.Max(1,task.IceClicks)); ice.Content=$"❄  📱  ❄\n\n{(int)(pct*100)}% chipped"; progress.Value=clicks; count.Text=$"{clicks} / {task.IceClicks}"; ice.Opacity=Math.Max(.48,1-pct*.45); }
        ice.Click += async (_,__) => { clicks++; Paint(); if (clicks>=task.IceClicks) await CompleteAsync(); };
        Paint(); root.Children.Add(progress); root.Children.Add(count); root.Children.Add(ice);
        root.Children.Add(new TextBlock { Text="Each click counts once. There is no hold-to-complete shortcut.", Foreground=Brushes.Gray, HorizontalAlignment=HorizontalAlignment.Center, Margin=new Thickness(0,12,0,0) });
    }

    void BuildTyping()
    {
        var collection=collections.FirstOrDefault(x=>x.Id==task.PassageCollectionId);
        var source=collection?.Passages.Where(x=>!string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
        if (source.Count==0) { root.Children.Add(new TextBlock { Text="No passages are assigned to this task. A parent can choose a collection in Parent Settings > Tasks, or add passages in Parent Settings > Passages.", Foreground=Brushes.DarkRed, TextWrapping=TextWrapping.Wrap }); return; }
        passages=source.OrderBy(_=>Random.Shared.Next()).Take(Math.Min(Math.Max(1,task.PassagesRequired),source.Count)).ToList();
        passageIndex=0; ShowPassage();
    }

    void ShowPassage()
    {
        while(root.Children.Count>2) root.Children.RemoveAt(2);
        var target=passages[passageIndex]; typed="";
        var counter=new TextBlock { Text=$"Passage {passageIndex+1} of {passages.Count}", Foreground=Brushes.DimGray, Margin=new Thickness(0,0,0,8) };
        var display=new TextBlock { TextWrapping=TextWrapping.Wrap, FontSize=19, LineHeight=30, Padding=new Thickness(16), Background=Brushes.White, MinHeight=170 };
        var input=new TextBox { FontSize=17, Margin=new Thickness(0,14,0,0) };
        DataObject.AddPastingHandler(input, (_,e)=>e.CancelCommand());
        input.PreviewKeyDown += (_,e) => { if ((Keyboard.Modifiers & ModifierKeys.Control)!=0 && e.Key==Key.V) e.Handled=true; };
        void Render()
        {
            display.Inlines.Clear();
            for(int i=0;i<target.Length;i++)
            {
                var run=new Run(target[i].ToString());
                if(i<typed.Length) run.Foreground = typed[i]==target[i] ? Brushes.Black : Brushes.Red;
                else run.Foreground=Brushes.LightGray;
                display.Inlines.Add(run);
            }
        }
        input.TextChanged += async (_,__) =>
        {
            var proposed=input.Text;
            if (proposed.Length>typed.Length+1) { input.Text=typed; input.CaretIndex=typed.Length; return; }
            if (typed.Length>0 && typed.Length<=target.Length && typed[^1]!=target[typed.Length-1] && proposed.Length>=typed.Length) { input.Text=typed; input.CaretIndex=typed.Length; return; }
            if(proposed.Length>target.Length) { input.Text=typed; input.CaretIndex=typed.Length; return; }
            typed=proposed; Render();
            if(typed==target)
            {
                input.IsEnabled=false; await Task.Delay(450);
                if(++passageIndex>=passages.Count) await CompleteAsync(); else ShowPassage();
            }
        };
        Render(); root.Children.Add(counter); root.Children.Add(display); root.Children.Add(input);
        root.Children.Add(new TextBlock { Text="Correct letters turn black. A mistake turns red and must be backspaced before you can continue. Paste is disabled.", Foreground=Brushes.Gray, Margin=new Thickness(0,10,0,0), TextWrapping=TextWrapping.Wrap });
        input.Focus();
    }

    async Task CompleteAsync()
    {
        try
        {
            var r=await agent.SendAsync(new AgentRequest { Type="completeTask", TaskId=task.Id });
            if(!r.Ok) throw new InvalidOperationException(r.Error);
            MessageBox.Show(this,$"Access earned for {task.RewardMinutes} minutes.","HomeFocus",MessageBoxButton.OK,MessageBoxImage.Information);
            DialogResult=true; Close();
        }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"HomeFocus",MessageBoxButton.OK,MessageBoxImage.Error); }
    }
}
