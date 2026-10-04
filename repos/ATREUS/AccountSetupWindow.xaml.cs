using System.Windows;

namespace ATREUS;

public partial class AccountSetupWindow : Window
{
    private readonly ApplicationData data;

    public AccountSetupWindow(ApplicationData data)
    {
        this.data = data;
        InitializeComponent();
        Loaded += (_, _) => UserNameInput.Focus();
    }

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        if (PasswordInput.Password != ConfirmPasswordInput.Password)
        {
            ErrorLabel.Text = "Les mots de passe ne correspondent pas.";
            return;
        }

        try
        {
            data.Users.Add(AccountSecurity.CreateAccount(
                data, UserNameInput.Text, DisplayNameInput.Text,
                UserRoles.Administrator, null, PasswordInput.Password));
            ApplicationDataStore.Save(data);
            DialogResult = true;
        }
        catch (ArgumentException exception)
        {
            ErrorLabel.Text = exception.Message;
        }
    }
}
