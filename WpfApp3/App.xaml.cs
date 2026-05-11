using System.Windows;

namespace WpfApp3;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow window = new();
        MainWindow = window;
        window.Show();
    }
}
