using System.Windows;

namespace ATREUS;

public partial class UserManagementWindow : Window
{
    private readonly ApplicationData data;

    public UserManagementWindow(ApplicationData data)
    {
        this.data = data;
        InitializeComponent();
        RefreshUsers();
    }

    private void RefreshUsers()
    {
        AccountsList.ItemsSource = data.Users.Select(user => new UserRow(
            user.UserName,
            user.DisplayName,
            $"{UserRoles.Display(user.Role)}{(user.IsEnabled ? string.Empty : " · désactivé")}",
            user.Role == UserRoles.Driver ? data.FindDriver(user.DriverId)?.Name ?? "Fiche introuvable" : "—"));
    }

    private void AddClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AddUserWindow(data) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.CreatedUser is UserAccount user)
        {
            data.Users.Add(user);
            ApplicationDataStore.Save(data);
            RefreshUsers();
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private sealed record UserRow(string UserName, string DisplayName, string Role, string DriverName);
}
