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

    private void ForgotPasswordClick(object sender, RoutedEventArgs e)
    {
        var userName = UserNameInput.Text.Trim();
        if (userName.Length == 0)
        {
            ErrorLabel.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(226, 128, 118));
            ErrorLabel.Text = "Saisissez votre identifiant pour envoyer une demande aux responsables habilités.";
            UserNameInput.Focus();
            return;
        }

        if (!data.PasswordResetRequests.Any(request =>
                request.Status == "En attente" &&
                string.Equals(request.UserName, userName, StringComparison.OrdinalIgnoreCase)))
        {
            data.PasswordResetRequests.Add(new PasswordResetRequest
            {
                UserName = userName,
                RequestedAt = DateTime.Now
            });
            ApplicationDataStore.Save(data);
        }
        ErrorLabel.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(124, 203, 155));
        ErrorLabel.Text = "Si un compte correspondant existe, une demande a été transmise aux responsables habilités.";
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
