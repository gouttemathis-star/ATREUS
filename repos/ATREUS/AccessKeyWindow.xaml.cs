using System.Windows;
using System.Windows.Input;

namespace ATREUS;

public partial class AccessKeyWindow : Window
{
    public AccessKeyWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public static bool Authorize(Window owner)
    {
        if ((Application.Current as App)?.AuthenticatedUser?.Role == UserRoles.Administrator)
        {
            return true;
        }

        MessageBox.Show(owner, "Cette action nécessite le rôle Administrateur.", "Accès refusé",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private void ValidateClick(object sender, RoutedEventArgs e)
    {
        if ((Application.Current as App)?.AuthenticatedUser?.Role == UserRoles.Administrator)
        {
            DialogResult = true;
            return;
        }

        ErrorText.Text = "Le code confidentiel est faux.";
        MessageBox.Show("Le code confidentiel est faux.", "Accès refusé", MessageBoxButton.OK, MessageBoxImage.Warning);
        PasswordInput.Clear();
        PasswordInput.Focus();
    }

    private void PasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ValidateClick(sender, e);
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
