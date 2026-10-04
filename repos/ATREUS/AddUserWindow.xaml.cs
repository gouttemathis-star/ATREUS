using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class AddUserWindow : Window
{
    private readonly ApplicationData data;
    private readonly UserAccount? editingUser;
    private readonly UserAccount? actor;

    public UserAccount? CreatedUser { get; private set; }

    public AddUserWindow(ApplicationData data, UserAccount? editingUser = null, UserAccount? actor = null)
    {
        this.data = data;
        this.editingUser = editingUser;
        this.actor = actor;
        InitializeComponent();
        RoleInput.ItemsSource = UserRoles.Definitions;
        AccessLevelInput.ItemsSource = AccessLevels.All
            .Where(level => AccountSecurity.CanAssignAccessLevel(actor, level))
            .ToList();
        RoleInput.SelectedItem = UserRoles.Get(editingUser?.Role ?? UserRoles.Operations);
        AccessLevelInput.SelectedItem = editingUser?.AccessLevel ?? AccessLevels.Level1;
        DriverInput.ItemsSource = data.Drivers.OrderBy(driver => driver.Name).ToList();
        if (editingUser is not null)
        {
            WindowHeading.Text = $"Modifier {editingUser.UserName}";
            SaveButton.Content = "Enregistrer les modifications";
            UserNameInput.Text = editingUser.UserName;
            DisplayNameInput.Text = editingUser.DisplayName;
            DriverInput.SelectedValue = editingUser.DriverId;
            if (editingUser.AccessLevel == AccessLevels.Special)
            {
                LoadCustomPermissions(editingUser);
                SetSpecialOnlyRestrictions(true);
            }
            RoleDescriptionLabel.Text = UserRoles.Get(editingUser.Role).Description;
            AccessDescriptionLabel.Text = DescribeLevel(editingUser.AccessLevel);
            CustomPermissionsPanel.Visibility =
                editingUser.AccessLevel == AccessLevels.Special ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            RoleInput.SelectedItem = UserRoles.Get(UserRoles.Operations);
            AccessLevelInput.SelectedItem = AccessLevels.Level1;
        }
        if (DriverInput.Items.Count > 0)
        {
            DriverInput.SelectedIndex = editingUser is null ? 0 : DriverInput.SelectedIndex;
        }
        RefreshPlanningLinkVisibility();
    }

    private void SetSpecialOnlyRestrictions(bool special)
    {
        CreateVehiclesPermission.IsEnabled = !special;
        DeleteVehiclesPermission.IsEnabled = !special;
        if (special)
        {
            CreateVehiclesPermission.IsChecked = false;
            DeleteVehiclesPermission.IsChecked = false;
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
            RefreshPlanningLinkVisibility();
        }
    }

    private void AccessLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AccessLevelInput?.SelectedItem is not string accessLevel)
        {
            return;
        }

        AccessDescriptionLabel.Text = DescribeLevel(accessLevel);
        CustomPermissionsPanel.Visibility = accessLevel == AccessLevels.Special
            ? Visibility.Visible
            : Visibility.Collapsed;
        SetSpecialOnlyRestrictions(accessLevel == AccessLevels.Special);
        if (accessLevel == AccessLevels.Special)
        {
            if (editingUser is not null)
            {
                LoadCustomPermissions(editingUser);
            }
            else
            {
                var temporary = new UserAccount();
                AccessControl.ApplyPreset(temporary, AccessLevels.Level1);
                LoadCustomPermissions(temporary);
            }
        }
        RefreshPlanningLinkVisibility();
    }

    private void PersonalCalendarChanged(object sender, RoutedEventArgs e) => RefreshPlanningLinkVisibility();

    private void RefreshPlanningLinkVisibility()
    {
        var personalPlanning = AccessLevelInput?.SelectedItem as string != AccessLevels.Special ||
            PersonalCalendarPermission?.IsChecked == true;
        var isDriver = RoleInput?.SelectedItem is UserRoleDefinition role && role.Name == UserRoles.Driver;
        var visible = personalPlanning || isDriver;
        DriverLabel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        DriverInput.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string DescribeLevel(string accessLevel) => accessLevel switch
    {
        AccessLevels.Level1 => "Niveau 1 : tableau de bord, parc, incidents, maintenance, planning personnel, assistance et SAV. Pas de carte, calendrier de groupe ni modification du parc.",
        AccessLevels.Level2 => "Niveau 2 : droits du niveau 1, avec ATREMAPS et calendrier de groupe. Aucune création ni suppression de véhicule.",
        AccessLevels.Level3 => "Niveau 3 : accès complet, gestion des véhicules et des comptes non protégés. Les comptes administrateurs ATREUS restent protégés.",
        _ => "Choisissez précisément les écrans et actions autorisés pour ce compte."
    };

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        if ((editingUser is null || PasswordInput.Password.Length > 0) &&
            PasswordInput.Password != ConfirmPasswordInput.Password)
        {
            ErrorLabel.Text = "Les mots de passe ne correspondent pas.";
            return;
        }

        try
        {
            var role = (RoleInput.SelectedItem as UserRoleDefinition)?.Name ?? string.Empty;
            var accessLevel = AccessLevelInput.SelectedItem as string ?? string.Empty;
            if (!AccountSecurity.CanAssignAccessLevel(actor, accessLevel))
            {
                throw new ArgumentException("Votre niveau ne permet pas d’attribuer ce niveau d’accréditation.");
            }
            ValidateCustomPermissions(accessLevel);
            if (editingUser is null)
            {
                CreatedUser = AccountSecurity.CreateAccount(
                    data, UserNameInput.Text, DisplayNameInput.Text, role,
                    accessLevel, DriverInput.SelectedValue as string, PasswordInput.Password);
                ApplySelectedPermissions(CreatedUser);
            }
            else
            {
                UpdateExistingUser(editingUser, role, accessLevel);
                CreatedUser = editingUser;
            }
            DialogResult = true;
        }
        catch (ArgumentException exception)
        {
            ErrorLabel.Text = exception.Message;
        }
    }

    private void UpdateExistingUser(UserAccount user, string role, string accessLevel)
    {
        if (actor?.Id == user.Id)
        {
            throw new ArgumentException("Vous ne pouvez pas modifier vos propres accès pendant votre session.");
        }
        if (actor is not null && !AccountSecurity.CanManageAccount(actor, user))
        {
            throw new ArgumentException("Vous ne pouvez pas modifier ce compte.");
        }
        if (!AccountSecurity.CanAssignAccessLevel(actor, accessLevel))
        {
            throw new ArgumentException("Votre niveau ne permet pas d’attribuer ce niveau d’accréditation.");
        }
        var newUserName = UserNameInput.Text.Trim();
        if (newUserName.Length < 3)
        {
            throw new ArgumentException("L’identifiant doit contenir au moins 3 caractères.");
        }
        if (string.IsNullOrWhiteSpace(DisplayNameInput.Text))
        {
            throw new ArgumentException("Le nom affiché est obligatoire.");
        }
        if (!UserRoles.All.Contains(role, StringComparer.Ordinal))
        {
            throw new ArgumentException("Le rôle sélectionné n’est pas valide.");
        }
        if (data.Users.Any(item => item.Id != user.Id &&
            string.Equals(item.UserName, newUserName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Cet identifiant existe déjà.");
        }
        var driverId = DriverInput.SelectedValue as string;
        if (role == UserRoles.Driver && data.FindDriver(driverId) is null)
        {
            throw new ArgumentException("Un compte conducteur doit être associé à une fiche chauffeur.");
        }
        if (role == UserRoles.Driver && data.Users.Any(item =>
            item.Id != user.Id &&
            item.IsEnabled &&
            item.Role == UserRoles.Driver &&
            item.DriverId == driverId))
        {
            throw new ArgumentException("Un compte actif est déjà associé à cette fiche chauffeur.");
        }

        user.UserName = newUserName;
        user.DisplayName = DisplayNameInput.Text.Trim();
        user.Role = role;
        user.DriverId = driverId;
        AccessControl.ApplyPreset(user, accessLevel);
        ApplySelectedPermissions(user);
        if (PasswordInput.Password.Length > 0)
        {
            AccountSecurity.SetPassword(user, PasswordInput.Password);
        }
    }

    private void ApplySelectedPermissions(UserAccount user)
    {
        if (AccessLevelInput.SelectedItem as string != AccessLevels.Special)
        {
            return;
        }

        user.CanViewDashboard = DashboardPermission.IsChecked == true;
        user.CanViewFleet = FleetPermission.IsChecked == true;
        user.CanViewIncidents = IncidentsPermission.IsChecked == true;
        user.CanViewMaintenance = MaintenancePermission.IsChecked == true;
        user.CanViewMap = MapPermission.IsChecked == true;
        user.CanViewPersonalCalendar = PersonalCalendarPermission.IsChecked == true;
        user.CanViewGroupCalendar = GroupCalendarPermission.IsChecked == true;
        user.CanUseAssistant = AssistantPermission.IsChecked == true;
        user.CanUseSav = SavPermission.IsChecked == true;
        user.CanCreateVehicles = CreateVehiclesPermission.IsChecked == true;
        user.CanEditVehicles = EditVehiclesPermission.IsChecked == true;
        user.CanDeleteVehicles = DeleteVehiclesPermission.IsChecked == true;
        user.CanManageIncidents = ManageIncidentsPermission.IsChecked == true;
        user.CanManageMaintenance = ManageMaintenancePermission.IsChecked == true;
        user.CanManageTasks = ManageTasksPermission.IsChecked == true;
        user.CanManageTrips = ManageTripsPermission.IsChecked == true;
        user.CanExport = ExportPermission.IsChecked == true;
        user.CanManageAccounts = ManageAccountsPermission.IsChecked == true;
    }

    private void ValidateCustomPermissions(string accessLevel)
    {
        if (accessLevel == AccessLevels.Special &&
            (CreateVehiclesPermission.IsChecked == true ||
             DeleteVehiclesPermission.IsChecked == true))
        {
            throw new ArgumentException("Le niveau spécial ne peut pas ajouter ou supprimer des véhicules.");
        }
    }

    private void LoadCustomPermissions(UserAccount user)
    {
        DashboardPermission.IsChecked = user.CanViewDashboard;
        FleetPermission.IsChecked = user.CanViewFleet;
        IncidentsPermission.IsChecked = user.CanViewIncidents;
        MaintenancePermission.IsChecked = user.CanViewMaintenance;
        MapPermission.IsChecked = user.CanViewMap;
        PersonalCalendarPermission.IsChecked = user.CanViewPersonalCalendar;
        GroupCalendarPermission.IsChecked = user.CanViewGroupCalendar;
        AssistantPermission.IsChecked = user.CanUseAssistant;
        SavPermission.IsChecked = user.CanUseSav;
        CreateVehiclesPermission.IsChecked = user.CanCreateVehicles;
        EditVehiclesPermission.IsChecked = user.CanEditVehicles;
        DeleteVehiclesPermission.IsChecked = user.CanDeleteVehicles;
        ManageIncidentsPermission.IsChecked = user.CanManageIncidents;
        ManageMaintenancePermission.IsChecked = user.CanManageMaintenance;
        ManageTasksPermission.IsChecked = user.CanManageTasks;
        ManageTripsPermission.IsChecked = user.CanManageTrips;
        ExportPermission.IsChecked = user.CanExport;
        ManageAccountsPermission.IsChecked = user.CanManageAccounts;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
