using System.Configuration;
using System.Data;
using System.Windows;

namespace ATREUS;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public UserAccount? AuthenticatedUser { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var data = ApplicationDataStore.Load();
        if (data.Users.Count == 0)
        {
            if (new AccountSetupWindow(data).ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        var login = new LoginWindow(data);
        if (login.ShowDialog() != true || login.AuthenticatedUser is null)
        {
            Shutdown();
            return;
        }

        AuthenticatedUser = login.AuthenticatedUser;
        var mainWindow = new MainWindow(data, login.AuthenticatedUser);
        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => Shutdown();
        mainWindow.Show();
    }
}
