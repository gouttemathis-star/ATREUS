using System.Windows;
using System.Windows.Input;

namespace ATREUS;

public partial class LoginWindow : Window
{
    private readonly ApplicationData data;

    public UserAccount? AuthenticatedUser { get; private set; }

    public LoginWindow(ApplicationData data)
    {
        this.data = data;
        InitializeComponent();
        Loaded += (_, _) => UserNameInput.Focus();
    }

    private void LoginClick(object sender, RoutedEventArgs e)
    {
        var user = AccountSecurity.Authenticate(data, UserNameInput.Text, PasswordInput.Password);
        if (user is null)
        {
            ErrorLabel.Text = "Identifiant ou mot de passe incorrect, ou compte désactivé.";
            PasswordInput.Clear();
            PasswordInput.Focus();
            return;
        }

        if (user.Role == UserRoles.Driver && data.FindDriver(user.DriverId) is null)
        {
            ErrorLabel.Text = "Ce compte conducteur n’est plus relié à une fiche chauffeur. Contactez l’administrateur.";
            return;
        }

        AuthenticatedUser = user;
        DialogResult = true;
    }

    private void InputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginClick(sender, e);
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
