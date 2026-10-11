using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HomeFocus.App;

internal sealed class PromptWindow : Window
{
    readonly PasswordBox box = new();
    public string Value => box.Password;

    public PromptWindow(string title, string message)
    {
        Title = title;
        Width = 420;
        Height = 230;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var body = new StackPanel();
        body.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });
        body.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("TextSecondary"),
            Margin = new Thickness(0, 7, 0, 14)
        });
        body.Children.Add(box);
        root.Children.Add(body);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var cancel = new Button
        {
            Content = "Cancel",
            MinWidth = 88,
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancel.Click += (_,__) => { DialogResult = false; Close(); };

        var ok = new Button
        {
            Content = "Continue",
            Tag = "Primary",
            MinWidth = 96,
            IsDefault = true
        };
        ok.Click += (_,__) => { DialogResult = true; Close(); };

        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        Grid.SetRow(buttons, 1);
        root.Children.Add(buttons);

        Content = root;
        Loaded += (_,__) => box.Focus();
    }

    static Brush Brush(string key)
        => Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
}
