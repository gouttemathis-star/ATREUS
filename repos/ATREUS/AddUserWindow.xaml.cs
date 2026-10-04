using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class AddUserWindow : Window
{
    private readonly ApplicationData data;

    public UserAccount? CreatedUser { get; private set; }

    public AddUserWindow(ApplicationData data)
    {
        this.data = data;
        InitializeComponent();
        RoleInput.ItemsSource = UserRoles.Definitions;
        RoleInput.SelectedItem = UserRoles.Get(UserRoles.Operations);
        DriverInput.ItemsSource = data.Drivers.OrderBy(driver => driver.Name).ToList();
        if (DriverInput.Items.Count > 0)
        {
            DriverInput.SelectedIndex = 0;
        }
    }

    private void RoleChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedRole = RoleInput?.SelectedItem as UserRoleDefinition;
        var isDriver = selectedRole?.Name == UserRoles.Driver;
        if (RoleDescriptionLabel is not null)
        {
            RoleDescriptionLabel.Text = selectedRole?.Description ?? string.Empty;
        }
        if (DriverLabel is not null)
        {
            DriverLabel.Visibility = isDriver ? Visibility.Visible : Visibility.Collapsed;
            DriverInput.Visibility = isDriver ? Visibility.Visible : Visibility.Collapsed;
        }
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
            CreatedUser = AccountSecurity.CreateAccount(
                data,
                UserNameInput.Text,
                DisplayNameInput.Text,
                (RoleInput.SelectedItem as UserRoleDefinition)?.Name ?? string.Empty,
                DriverInput.SelectedValue as string,
                PasswordInput.Password);
            DialogResult = true;
        }
        catch (ArgumentException exception)
        {
            ErrorLabel.Text = exception.Message;
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
