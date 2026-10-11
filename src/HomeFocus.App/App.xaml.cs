using System.Windows;

namespace HomeFocus.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ProxySettings.Enable();
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
