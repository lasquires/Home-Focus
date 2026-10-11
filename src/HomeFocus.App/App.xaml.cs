using System.Windows;

namespace HomeFocus.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Do not change the browser proxy until the agent reports that filtering is enabled.
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
