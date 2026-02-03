using System.Windows;

namespace CIDR.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var mainWindow = new MainWindow();
        
        if (e.Args.Length > 0 && System.IO.File.Exists(e.Args[0]))
        {
            _ = mainWindow.LoadFileAsync(e.Args[0]);
        }

        mainWindow.Show();
    }
}
