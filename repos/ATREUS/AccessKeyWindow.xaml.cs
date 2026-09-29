using System.Windows;
using System.Windows.Input;

namespace ATREUS;

public partial class AccessKeyWindow : Window
{
    private const string AccessKey = "ATREUS-ADMIN-2026";

    public AccessKeyWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordInput.Focus();
    }

    public static bool Authorize(Window owner)
    {
        var window = new AccessKeyWindow { Owner = owner };
        return window.ShowDialog() == true;
    }

    private void ValidateClick(object sender, RoutedEventArgs e)
    {
        if (PasswordInput.Password == AccessKey)
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
