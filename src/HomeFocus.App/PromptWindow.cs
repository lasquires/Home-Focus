using System.Windows;
using System.Windows.Controls;

namespace HomeFocus.App;

internal sealed class PromptWindow : Window
{
    readonly PasswordBox box = new();
    public string Value => box.Password;
    public PromptWindow(string title, string message)
    {
        Title = title; Width = 380; Height = 190; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) });
        box.Margin = new Thickness(0,0,0,14); panel.Children.Add(box);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", MinWidth = 80, Margin = new Thickness(0,0,8,0) };
        cancel.Click += (_,__) => { DialogResult = false; Close(); };
        var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
        ok.Click += (_,__) => { DialogResult = true; Close(); };
        buttons.Children.Add(cancel); buttons.Children.Add(ok); panel.Children.Add(buttons); Content = panel;
        Loaded += (_,__) => box.Focus();
    }
}
