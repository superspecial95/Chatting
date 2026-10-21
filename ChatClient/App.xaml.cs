using System.Windows;

namespace ChatClient;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LoginWindow loginWindow = new LoginWindow();
        loginWindow.Show();
    }
}