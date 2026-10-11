using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using HomeFocus.Shared;

namespace HomeFocus.App;

internal sealed class TaskRunnerWindow : Window
{
    readonly AgentClient agent;
    readonly TaskDefinition task;
    readonly IReadOnlyList<PassageCollection> collections;

    int clicks;
    int passageIndex;
    string typed = "";
    List<string> passages = new();
    readonly StackPanel root = new();

    public TaskRunnerWindow(
        AgentClient agent,
        TaskDefinition task,
        IReadOnlyList<PassageCollection> collections)
    {
        this.agent = agent;
        this.task = task;
        this.collections = collections;

        Title = task.Name;
        Width = 720;
        Height = 610;
        MinWidth = 660;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanMinimize;

        Content = root;
        root.Margin = new Thickness(30);
        Loaded += (_,__) => Build();
    }

    static Brush Brush(string key)
        => Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;

    static Border Card(UIElement child, Thickness? padding = null)
        => new()
        {
            Background = Brush("CardBackground"),
            BorderBrush = Brush("ControlBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = padding ?? new Thickness(20),
            Child = child
        };

    void Build()
    {
        root.Children.Clear();
        root.Children.Add(new TextBlock
        {
            Text = task.Name,
            FontSize = 28,
            FontWeight = FontWeights.Bold
        });
        root.Children.Add(new TextBlock
        {
            Text = $"Complete this to earn {task.RewardMinutes} minutes.",
            Foreground = Brush("TextSecondary"),
            Margin = new Thickness(0, 5, 0, 22)
        });

        if (task.Kind == TaskKind.Ice)
            BuildIce();
        else
            BuildTyping();
    }

    void BuildIce()
    {
        var outer = new StackPanel();

        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = Math.Max(1, task.IceClicks),
            Value = clicks,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var count = new TextBlock
        {
            Text = $"{clicks} of {task.IceClicks} chips",
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14)
        };

        var stage = new Grid
        {
            Width = 430,
            Height = 300,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var iceBlock = new Border
        {
            Margin = new Thickness(18),
            CornerRadius = new CornerRadius(28),
            BorderBrush = new SolidColorBrush(Color.FromArgb(165, 255, 255, 255)),
            BorderThickness = new Thickness(2),
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Color.FromArgb(145, 210, 243, 252), 0),
                    new(Color.FromArgb(100, 165, 220, 244), .55),
                    new(Color.FromArgb(105, 246, 251, 255), 1)
                },
                new Point(0, 0),
                new Point(1, 1))
        };
        stage.Children.Add(iceBlock);

        var phone = new Border
        {
            Width = 118,
            Height = 218,
            Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(18, 18, 20)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8)
        };
        var screen = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(244, 244, 246)),
            CornerRadius = new CornerRadius(17)
        };
        var screenText = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        screenText.Children.Add(new TextBlock
        {
            Text = "WAIT",
            FontWeight = FontWeights.Bold,
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        screenText.Children.Add(new TextBlock
        {
            Text = "finish the task",
            Foreground = Brush("TextSecondary"),
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        screen.Child = screenText;
        phone.Child = screen;
        stage.Children.Add(phone);

        var shards = new List<(Border View, double Start, double End, double X, double Y)>();
        void AddShard(
            double width,
            double height,
            HorizontalAlignment h,
            VerticalAlignment v,
            Thickness margin,
            double start,
            double end,
            double dx,
            double dy)
        {
            var shard = new Border
            {
                Width = width,
                Height = height,
                HorizontalAlignment = h,
                VerticalAlignment = v,
                Margin = margin,
                CornerRadius = new CornerRadius(14),
                BorderBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new(Color.FromArgb(205, 244, 253, 255), 0),
                        new(Color.FromArgb(150, 150, 220, 244), 1)
                    },
                    new Point(0, 0),
                    new Point(1, 1)),
                RenderTransform = new TranslateTransform()
            };
            stage.Children.Add(shard);
            shards.Add((shard, start, end, dx, dy));
        }

        AddShard(118, 78, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(30, 26, 0, 0), .02, .32, -38, -28);
        AddShard(122, 74, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 30, 26, 0), .08, .40, 40, -30);
        AddShard(118, 82, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(28, 0, 0, 24), .16, .50, -40, 30);
        AddShard(124, 80, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 24, 28), .24, .58, 42, 32);
        AddShard(178, 48, HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 20, 0, 0), .34, .68, 0, -38);
        AddShard(180, 48, HorizontalAlignment.Center, VerticalAlignment.Bottom, new Thickness(0, 0, 0, 20), .42, .76, 0, 40);
        AddShard(58, 158, HorizontalAlignment.Left, VerticalAlignment.Center, new Thickness(24, 0, 0, 0), .50, .88, -46, 0);
        AddShard(58, 158, HorizontalAlignment.Right, VerticalAlignment.Center, new Thickness(0, 0, 24, 0), .58, .96, 48, 0);

        var tap = new Button
        {
            Content = stage,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        void Paint()
        {
            var pct = Math.Min(1.0, clicks / (double)Math.Max(1, task.IceClicks));
            progress.Value = clicks;
            count.Text = $"{clicks} of {task.IceClicks} chips";

            iceBlock.Opacity = Math.Max(.14, .88 - pct * .65);
            phone.RenderTransform = new ScaleTransform(.98 + pct * .04, .98 + pct * .04);

            foreach (var item in shards)
            {
                var local = pct <= item.Start
                    ? 0
                    : pct >= item.End
                        ? 1
                        : (pct - item.Start) / (item.End - item.Start);

                item.View.Opacity = .96 - local * .88;
                if (item.View.RenderTransform is TranslateTransform move)
                {
                    move.X = item.X * local;
                    move.Y = item.Y * local;
                }
            }
        }

        tap.Click += async (_,__) =>
        {
            clicks++;
            Paint();
            if (clicks >= task.IceClicks)
                await CompleteAsync();
        };

        Paint();

        outer.Children.Add(progress);
        outer.Children.Add(count);
        outer.Children.Add(tap);
        outer.Children.Add(new TextBlock
        {
            Text = "Click the ice. Each click counts once; there is no hold-to-complete shortcut.",
            Foreground = Brush("TextSecondary"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0)
        });

        root.Children.Add(Card(outer));
    }

    void BuildTyping()
    {
        var collection = collections.FirstOrDefault(x => x.Id == task.PassageCollectionId);
        var source = collection?.Passages
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList() ?? new List<string>();

        if (source.Count == 0)
        {
            root.Children.Add(Card(new TextBlock
            {
                Text = "No passages are assigned to this task. A parent can choose a collection in Parent Settings > Tasks or add passages in Parent Settings > Passages.",
                Foreground = Brush("DangerBrush"),
                TextWrapping = TextWrapping.Wrap
            }));
            return;
        }

        passages = source
            .OrderBy(_ => Random.Shared.Next())
            .Take(Math.Min(Math.Max(1, task.PassagesRequired), source.Count))
            .ToList();

        passageIndex = 0;
        ShowPassage();
    }

    void ShowPassage()
    {
        while (root.Children.Count > 2)
            root.Children.RemoveAt(2);

        var target = passages[passageIndex];
        typed = "";

        var panel = new StackPanel();
        var top = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var counter = new TextBlock
        {
            Text = $"Passage {passageIndex + 1} of {passages.Count}",
            FontWeight = FontWeights.SemiBold
        };
        top.Children.Add(counter);

        var progressText = new TextBlock
        {
            Text = $"{passageIndex + 1}/{passages.Count}",
            Foreground = Brush("TextSecondary")
        };
        Grid.SetColumn(progressText, 1);
        top.Children.Add(progressText);
        panel.Children.Add(top);

        var display = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 18,
            LineHeight = 29,
            Padding = new Thickness(16),
            MinHeight = 185
        };

        var displayCard = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(249, 249, 251)),
            BorderBrush = Brush("ControlBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = display
        };
        panel.Children.Add(displayCard);

        var input = new TextBox
        {
            FontSize = 16,
            Margin = new Thickness(0, 14, 0, 0)
        };

        DataObject.AddPastingHandler(input, (_, e) => e.CancelCommand());
        input.PreviewKeyDown += (_, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.V)
                e.Handled = true;
        };

        void Render()
        {
            display.Inlines.Clear();
            for (var i = 0; i < target.Length; i++)
            {
                var run = new Run(target[i].ToString());
                if (i < typed.Length)
                    run.Foreground = typed[i] == target[i] ? Brush("TextPrimary") : Brush("DangerBrush");
                else
                    run.Foreground = new SolidColorBrush(Color.FromRgb(183, 183, 189));

                display.Inlines.Add(run);
            }
        }

        input.TextChanged += async (_,__) =>
        {
            var proposed = input.Text;

            if (proposed.Length > typed.Length + 1)
            {
                input.Text = typed;
                input.CaretIndex = typed.Length;
                return;
            }

            if (typed.Length > 0 &&
                typed.Length <= target.Length &&
                typed[^1] != target[typed.Length - 1] &&
                proposed.Length >= typed.Length)
            {
                input.Text = typed;
                input.CaretIndex = typed.Length;
                return;
            }

            if (proposed.Length > target.Length)
            {
                input.Text = typed;
                input.CaretIndex = typed.Length;
                return;
            }

            typed = proposed;
            Render();

            if (typed == target)
            {
                input.IsEnabled = false;
                await Task.Delay(450);

                if (++passageIndex >= passages.Count)
                    await CompleteAsync();
                else
                    ShowPassage();
            }
        };

        Render();
        panel.Children.Add(input);
        panel.Children.Add(new TextBlock
        {
            Text = "Correct letters turn dark. A mistake turns red and must be backspaced before you can continue. Paste is disabled.",
            Foreground = Brush("TextSecondary"),
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        root.Children.Add(Card(panel));
        input.Focus();
    }

    async Task CompleteAsync()
    {
        try
        {
            var r = await agent.SendAsync(new AgentRequest
            {
                Type = "completeTask",
                TaskId = task.Id
            });

            if (!r.Ok)
                throw new InvalidOperationException(r.Error);

            MessageBox.Show(
                this,
                $"Access earned for {task.RewardMinutes} minutes.",
                "HomeFocus",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "HomeFocus", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
