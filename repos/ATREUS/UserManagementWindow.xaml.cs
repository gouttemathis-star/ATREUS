using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class UserManagementWindow : UserControl
{
    private readonly ApplicationData data;
    private readonly UserAccount actor;
    public event Action? ReturnRequested;
    public event Action<int>? PendingRequestCountChanged;

    public UserManagementWindow(ApplicationData data, UserAccount actor)
    {
        this.data = data;
        this.actor = actor;
        InitializeComponent();
        RefreshUsers();
    }

    private void RefreshUsers()
    {
        AccountsList.ItemsSource = data.Users.Select(user => new UserRow(
            user,
            user.UserName,
            user.DisplayName,
            AccessControl.IsProtectedAdministrator(user)
                ? "👑 ADMINISTRATEUR ATREUS · COMPTE PROTÉGÉ"
                : $"{UserRoles.Display(user.Role)} · {user.AccessLevel}{(user.IsEnabled ? string.Empty : " · désactivé")}",
            user.Role == UserRoles.Driver ? data.FindDriver(user.DriverId)?.Name ?? "Fiche introuvable" : "—"));
        var pendingCount = data.PasswordResetRequests.Count(request => request.Status == "En attente");
        PendingRequestCountChanged?.Invoke(pendingCount);
        ResetRequestsTab.Header = pendingCount == 0
            ? "Demandes de réinitialisation"
            : $"Demandes de réinitialisation ({pendingCount})";
        ResetRequestsList.ItemsSource = data.PasswordResetRequests
            .OrderBy(request => request.Status == "En attente" ? 0 : 1)
            .ThenByDescending(request => request.RequestedAt)
            .Select(request => new ResetRequestRow(
                request,
                request.UserName,
                request.RequestedAt.ToString("dd/MM/yyyy HH:mm"),
                request.Status))
            .ToList();
        UpdateActionButtons();
    }

    private void AddClick(object sender, RoutedEventArgs e)
    {
        if (!AccessControl.Resolve(actor).ManageAccounts) return;
        var dialog = new AddUserWindow(data, actor: actor) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.CreatedUser is UserAccount user)
        {
            data.Users.Add(user);
            ApplicationDataStore.Save(data);
            RefreshUsers();
        }
    }

    private void EditClick(object sender, RoutedEventArgs e)
    {
        if ((AccountsList.SelectedItem as UserRow)?.Account is not UserAccount target ||
            !AccountSecurity.CanManageAccount(actor, target) ||
            target.Id == actor.Id)
        {
            MessageBox.Show("Ce compte est protégé, ou vous ne pouvez pas modifier vos propres accès pendant votre session.",
                "Accès refusé", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new AddUserWindow(data, target, actor) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            ApplicationDataStore.Save(data);
            RefreshUsers();
        }
    }

    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        if ((AccountsList.SelectedItem as UserRow)?.Account is not UserAccount target ||
            !AccountSecurity.CanManageAccount(actor, target))
        {
            MessageBox.Show("Ce compte est protégé ou votre niveau ne permet pas de le supprimer.",
                "Accès refusé", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show($"Supprimer le compte « {target.UserName} » ?",
                "Confirmer la suppression", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        data.Users.Remove(target);
        ApplicationDataStore.Save(data);
        RefreshUsers();
    }

    private void ResetPasswordClick(object sender, RoutedEventArgs e)
    {
        if ((AccountsList.SelectedItem as UserRow)?.Account is not UserAccount target ||
            !AccountSecurity.CanManageAccount(actor, target))
        {
            MessageBox.Show("Ce compte est protégé ou votre niveau ne permet pas de réinitialiser son mot de passe.",
                "Accès refusé", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(
                $"Réinitialiser le mot de passe de « {target.UserName} » ? Son mot de passe actuel ne fonctionnera plus.",
                "Confirmer la réinitialisation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var temporaryPassword = AccountSecurity.CreateTemporaryPassword();
        AccountSecurity.SetPassword(target, temporaryPassword);
        foreach (var request in data.PasswordResetRequests.Where(request =>
                     request.Status == "En attente" &&
                     string.Equals(request.UserName, target.UserName, StringComparison.OrdinalIgnoreCase)))
        {
            request.Status = "Traitée";
            request.ResolvedAt = DateTime.Now;
        }
        ApplicationDataStore.Save(data);
        RefreshUsers();
        MessageBox.Show(
            $"Le mot de passe de « {target.UserName} » a été réinitialisé.\n\nMot de passe temporaire :\n{temporaryPassword}\n\nCommuniquez-le à l’utilisateur de manière sécurisée.",
            "Réinitialisation terminée",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void ProcessResetRequestClick(object sender, RoutedEventArgs e)
    {
        if (!AccessControl.Resolve(actor).ManageAccounts ||
            (ResetRequestsList.SelectedItem as ResetRequestRow)?.Request is not PasswordResetRequest request ||
            request.Status != "En attente")
        {
            MessageBox.Show("Sélectionnez une demande en attente avec un compte que votre niveau est autorisé à gérer.",
                "Demande non traitable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var target = data.Users.FirstOrDefault(user =>
            string.Equals(user.UserName, request.UserName, StringComparison.OrdinalIgnoreCase));
        if (target is null || !AccountSecurity.CanManageAccount(actor, target))
        {
            request.Status = "Ignorée · compte non gérable";
            request.ResolvedAt = DateTime.Now;
            ApplicationDataStore.Save(data);
            RefreshUsers();
            MessageBox.Show("La demande ne correspond pas à un compte que vous êtes autorisé à gérer. Elle a été clôturée sans modification de compte.",
                "Demande clôturée", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(
                $"Réinitialiser le mot de passe de « {target.UserName} » à la suite de cette demande ?",
                "Confirmer la réinitialisation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var temporaryPassword = AccountSecurity.CreateTemporaryPassword();
        AccountSecurity.SetPassword(target, temporaryPassword);
        request.Status = "Traitée";
        request.ResolvedAt = DateTime.Now;
        ApplicationDataStore.Save(data);
        RefreshUsers();
        MessageBox.Show(
            $"La demande de « {target.UserName} » a été traitée.\n\nMot de passe temporaire :\n{temporaryPassword}\n\nCommuniquez-le à l’utilisateur de manière sécurisée.",
            "Réinitialisation terminée",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void DismissResetRequestClick(object sender, RoutedEventArgs e)
    {
        if (!AccessControl.Resolve(actor).ManageAccounts ||
            (ResetRequestsList.SelectedItem as ResetRequestRow)?.Request is not PasswordResetRequest request ||
            request.Status != "En attente")
        {
            MessageBox.Show("Sélectionnez une demande en attente à ignorer.",
                "Aucune demande sélectionnée", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        request.Status = "Ignorée";
        request.ResolvedAt = DateTime.Now;
        ApplicationDataStore.Save(data);
        RefreshUsers();
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionButtons();

    private void UpdateActionButtons()
    {
        var isAccountTab = ManagementTabs?.SelectedItem == AccountsTab;
        var selected = (AccountsList?.SelectedItem as UserRow)?.Account;
        var canManage = isAccountTab && selected is not null && AccountSecurity.CanManageAccount(actor, selected);
        var canEdit = canManage && selected?.Id != actor.Id;
        var selectedRequest = (ResetRequestsList?.SelectedItem as ResetRequestRow)?.Request;
        var canProcessRequest = !isAccountTab &&
            AccessControl.Resolve(actor).ManageAccounts &&
            selectedRequest?.Status == "En attente";
        if (EditAccountButton is not null)
        {
            EditAccountButton.Visibility = isAccountTab ? Visibility.Visible : Visibility.Collapsed;
            EditAccountButton.IsEnabled = canEdit;
        }
        if (DeleteAccountButton is not null)
        {
            DeleteAccountButton.Visibility = isAccountTab ? Visibility.Visible : Visibility.Collapsed;
            DeleteAccountButton.IsEnabled = canManage;
        }
        if (ResetPasswordButton is not null)
        {
            ResetPasswordButton.Visibility = isAccountTab ? Visibility.Visible : Visibility.Collapsed;
            ResetPasswordButton.IsEnabled = canManage;
        }
        if (ProcessResetRequestButton is not null)
        {
            ProcessResetRequestButton.Visibility = isAccountTab ? Visibility.Collapsed : Visibility.Visible;
            ProcessResetRequestButton.IsEnabled = canProcessRequest;
        }
        if (DismissResetRequestButton is not null)
        {
            DismissResetRequestButton.Visibility = isAccountTab ? Visibility.Collapsed : Visibility.Visible;
            DismissResetRequestButton.IsEnabled = canProcessRequest;
        }
    }

    public void RefreshView() => RefreshUsers();

    private void CloseClick(object sender, RoutedEventArgs e) => ReturnRequested?.Invoke();

    private sealed record UserRow(UserAccount Account, string UserName, string DisplayName, string Role, string DriverName);
    private sealed record ResetRequestRow(PasswordResetRequest Request, string UserName, string RequestedAt, string Status);
}
