using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ATREUS;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ApplicationData data;
    private readonly UserAccount currentUser;
    private readonly AccessPermissions permissions;
    private readonly AssistantWindow assistantContent;
    private readonly UserManagementWindow userManagementContent;
    public ObservableCollection<Vehicle> VisibleVehicles { get; } = [];
    public ObservableCollection<Vehicle> AlertVehicles { get; } = [];
    public int ReferenceCount => data.Vehicles.Count;
    public int AvailableCount => data.Vehicles.Count(vehicle => vehicle.State == "Disponible");
    public int MaintenanceCount => data.Vehicles.Count(vehicle => vehicle.State == "Maintenance");
    public int UnavailableCount => data.Vehicles.Count(vehicle => vehicle.State == "Indisponible");
    public string AvailabilityPercentage => ReferenceCount == 0 ? "0 %" : $"{AvailableCount * 100 / ReferenceCount} %";
    public int AlertCount => MaintenanceCount + UnavailableCount;

    public event PropertyChangedEventHandler? PropertyChanged;
    private string searchQuery = string.Empty;
    private bool refreshingTypeFilter;
    private string? selectedVehicleType;

    public MainWindow(ApplicationData data, UserAccount currentUser)
    {
        this.data = data;
        this.currentUser = currentUser;
        permissions = AccessControl.Resolve(currentUser);
        InitializeComponent();
        DataContext = this;
        assistantContent = new AssistantWindow();
        assistantContent.Configure(data, currentUser);
        AssistantHost.Content = assistantContent;
        userManagementContent = new UserManagementWindow(data, currentUser);
        userManagementContent.ReturnRequested += ReturnFromAccounts;
        userManagementContent.PendingRequestCountChanged += UpdateAccountAlertIndicator;
        UserManagementHost.Content = userManagementContent;
        userManagementContent.RefreshView();
        ConfigureRoleAccess();
        RefreshTypeFilter();
        RefreshVehicles();
        EmbeddedMapView.VehicleSelected += MapVehicleSelected;
        MaterialView.VehicleRequested += OpenVehicle;
        OperationsView.VehicleRequested += OpenVehicle;
        OperationsView.DriverRequested += OpenDriver;
        CalendarView.VehicleRequested += OpenVehicle;
        CalendarView.DriverRequested += OpenDriver;
        if (currentUser.Role == UserRoles.Driver && permissions.PersonalCalendar)
        {
            DriverPlanningClick(this, new RoutedEventArgs());
        }
        else if (permissions.Dashboard)
        {
            DashboardClick(this, new RoutedEventArgs());
        }
        else if (permissions.Fleet)
        {
            MaterialClick(this, new RoutedEventArgs());
        }
        else if (permissions.Maintenance)
        {
            MaintenanceClick(this, new RoutedEventArgs());
        }
        else if (permissions.Incidents)
        {
            IncidentsClick(this, new RoutedEventArgs());
        }
        else if (permissions.GroupCalendar)
        {
            CalendarClick(this, new RoutedEventArgs());
        }
        else if (permissions.PersonalCalendar)
        {
            DriverPlanningClick(this, new RoutedEventArgs());
        }
        else if (permissions.Assistant)
        {
            SupportClick(this, new RoutedEventArgs());
        }
        else if (permissions.Sav)
        {
            SavClick(this, new RoutedEventArgs());
        }
        else if (permissions.ManageAccounts)
        {
            AccountsClick(this, new RoutedEventArgs());
        }
    }

    private void ConfigureRoleAccess()
    {
        DashboardView.Visibility = Visibility.Collapsed;
        SessionNameLabel.Text = currentUser.DisplayName.ToUpperInvariant();
        SessionRoleLabel.Text = AccessControl.AccountDisplay(currentUser);
        DashboardNavButton.Visibility = VisibleIf(permissions.Dashboard);
        MapNavButton.Visibility = VisibleIf(permissions.Map);
        MaterialNavButton.Visibility = VisibleIf(permissions.Fleet);
        IncidentsNavButton.Visibility = VisibleIf(permissions.Incidents);
        MaintenanceNavButton.Visibility = VisibleIf(permissions.Maintenance);
        CalendarNavButton.Visibility = VisibleIf(permissions.GroupCalendar);
        DriverPlanningNavButton.Visibility = VisibleIf(permissions.PersonalCalendar);
        AccountsNavButton.Visibility = VisibleIf(permissions.ManageAccounts);
        AddVehicleButton.Visibility = VisibleIf(permissions.CreateVehicles);
        ExportCsvButton.Visibility = VisibleIf(permissions.Export);
        SupportNavButton.Visibility = VisibleIf(permissions.Assistant);
        SavNavButton.Visibility = VisibleIf(permissions.Sav);
        FleetHeader.Visibility = VisibleIf(permissions.Fleet || permissions.Incidents || permissions.Maintenance || permissions.GroupCalendar || permissions.PersonalCalendar);
        PilotageHeader.Visibility = VisibleIf(permissions.Dashboard || permissions.Map);
        PilotageHeader.Text = AccessControl.IsProtectedAdministrator(currentUser)
            ? "👑 ADMINISTRATEUR ATREUS"
            : $"{UserRoles.Display(currentUser.Role).ToUpperInvariant()} · {currentUser.AccessLevel.ToUpperInvariant()}";
        AssistanceHeader.Visibility = VisibleIf(permissions.Assistant || permissions.Sav);
    }

    private void UpdateAccountAlertIndicator(int pendingCount)
    {
        if (AccountsNavButton is not null)
        {
            AccountsNavButton.Content = pendingCount > 0
                ? $"♙  Comptes et rôles · {pendingCount} alerte(s)"
                : "♙  Comptes et rôles";
        }
    }

    private static Visibility VisibleIf(bool allowed) => allowed ? Visibility.Visible : Visibility.Collapsed;

    private void FilterChanged(object sender, SelectionChangedEventArgs e) => RefreshVehicles();

    private void RefreshTypeFilter(string? selectedType = null)
    {
        if (TypeFilter is null)
        {
            return;
        }

        refreshingTypeFilter = true;
        TypeFilter.Items.Clear();
        TypeFilter.Items.Add(new ComboBoxItem { Content = "Tous les types" });
        foreach (var category in data.VehicleCategories)
        {
            TypeFilter.Items.Add(new ComboBoxItem { Content = category.Display, Tag = category.Name });
        }
        if (permissions.CreateVehicles || permissions.EditVehicles)
        {
            TypeFilter.Items.Add(new ComboBoxItem { Content = "➕  Créer une catégorie de véhicule…", Tag = VehicleTypes.CreateCategoryAction });
        }
        TypeFilter.SelectedItem = selectedType is null
            ? TypeFilter.Items[0]
            : TypeFilter.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Tag as string == selectedType) ?? TypeFilter.Items[0];
        selectedVehicleType = (TypeFilter.SelectedItem as ComboBoxItem)?.Tag as string;
        refreshingTypeFilter = false;
    }

    private void TypeFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshingTypeFilter)
        {
            return;
        }
        if (TypeFilter.SelectedItem is not ComboBoxItem item || item.Tag as string != VehicleTypes.CreateCategoryAction)
        {
            selectedVehicleType = (TypeFilter.SelectedItem as ComboBoxItem)?.Tag as string;
            RefreshVehicles();
            return;
        }

        if (!permissions.CreateVehicles && !permissions.EditVehicles)
        {
            RefreshTypeFilter(selectedVehicleType);
            return;
        }

        var dialog = new AddVehicleCategoryWindow(data) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.CreatedCategory is VehicleCategory category)
        {
            RefreshTypeFilter(category.Name);
        }
        else
        {
            RefreshTypeFilter(selectedVehicleType);
        }
        RefreshVehicles();
    }

    private void MapClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.Map) return;
        HideAllViews();
        EmbeddedMapView.ShowVehicles(data);
        EmbeddedMapView.Visibility = Visibility.Visible;
        SetActiveNavigation(MapNavButton);
    }

    private void DashboardClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.Dashboard) return;
        HideAllViews();
        DashboardView.Visibility = Visibility.Visible;
        SetActiveNavigation(DashboardNavButton);
    }

    private void MaterialClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.Fleet) return;
        HideAllViews();
        MaterialView.ShowVehicles(data);
        MaterialView.Visibility = Visibility.Visible;
        SetActiveNavigation(MaterialNavButton);
    }

    private void IncidentsClick(object sender, RoutedEventArgs e) => ShowOperations(OperationKinds.Incident, IncidentsNavButton);
    private void MaintenanceClick(object sender, RoutedEventArgs e) => ShowOperations(OperationKinds.Maintenance, MaintenanceNavButton);

    private void ShowOperations(string kind, Button activeButton)
    {
        if (kind == OperationKinds.Incident ? !permissions.Incidents : !permissions.Maintenance) return;
        HideAllViews();
        var canManage = kind == OperationKinds.Incident
            ? permissions.ManageIncidents
            : permissions.ManageMaintenance;
        OperationsView.ShowData(data, kind, canManage);
        OperationsView.Visibility = Visibility.Visible;
        SetActiveNavigation(activeButton);
    }

    private void CalendarClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.GroupCalendar) return;
        HideAllViews();
        CalendarView.ShowData(data, permissions.ManageTasks, permissions.ManageTrips, permissions.GroupCalendar);
        CalendarView.Visibility = Visibility.Visible;
        SetActiveNavigation(CalendarNavButton);
    }

    private void DriverPlanningClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.PersonalCalendar)
        {
            return;
        }

        HideAllViews();
        CalendarView.ShowPersonalPlan(data, data.FindDriver(currentUser.DriverId));
        CalendarView.Visibility = Visibility.Visible;
        SetActiveNavigation(DriverPlanningNavButton);
    }

    private void SupportClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.Assistant) return;
        HideAllViews();
        assistantContent.ShowAssistant();
        AssistantHost.Visibility = Visibility.Visible;
        SetActiveNavigation(SupportNavButton);
    }

    private void SavClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.Sav) return;
        HideAllViews();
        SavSupportContent.Visibility = Visibility.Visible;
        SetActiveNavigation(SavNavButton);
    }

    private void AccountsClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.ManageAccounts) return;
        HideAllViews();
        userManagementContent.RefreshView();
        UserManagementHost.Visibility = Visibility.Visible;
        SetActiveNavigation(AccountsNavButton);
    }

    private void ReturnFromAccounts()
    {
        if (permissions.Dashboard)
        {
            DashboardClick(this, new RoutedEventArgs());
        }
        else if (permissions.Fleet)
        {
            MaterialClick(this, new RoutedEventArgs());
        }
        else if (permissions.Incidents)
        {
            IncidentsClick(this, new RoutedEventArgs());
        }
        else if (permissions.Maintenance)
        {
            MaintenanceClick(this, new RoutedEventArgs());
        }
        else if (permissions.GroupCalendar)
        {
            CalendarClick(this, new RoutedEventArgs());
        }
        else if (permissions.PersonalCalendar)
        {
            DriverPlanningClick(this, new RoutedEventArgs());
        }
        else if (permissions.Map)
        {
            MapClick(this, new RoutedEventArgs());
        }
        else if (permissions.Assistant)
        {
            SupportClick(this, new RoutedEventArgs());
        }
        else if (permissions.Sav)
        {
            SavClick(this, new RoutedEventArgs());
        }
        else
        {
            HideAllViews();
        }
    }

    private void HideAllViews()
    {
        DashboardView.Visibility = Visibility.Collapsed;
        MaterialView.Visibility = Visibility.Collapsed;
        OperationsView.Visibility = Visibility.Collapsed;
        EmbeddedMapView.Visibility = Visibility.Collapsed;
        CalendarView.Visibility = Visibility.Collapsed;
        AssistantHost.Visibility = Visibility.Collapsed;
        SavSupportContent.Visibility = Visibility.Collapsed;
        UserManagementHost.Visibility = Visibility.Collapsed;
    }

    private void MapVehicleSelected(object? sender, Vehicle vehicle) => OpenVehicle(vehicle);

    private void OpenVehicle(Vehicle vehicle)
    {
        if (!permissions.Fleet) return;
        var window = new VehicleDetailWindow(vehicle, data, permissions.EditVehicles, permissions.DeleteVehicles) { Owner = this };
        if (window.ShowDialog() == true && window.UpdatedVehicle is not null)
        {
            ReplaceVehicle(window.UpdatedVehicle);
        }
        else if (window.WasDeleted)
        {
            data.Vehicles.Remove(vehicle);
            PersistAndRefresh();
        }
    }

    private void OpenDriver(Driver driver)
    {
        if (!permissions.Fleet) return;
        var window = new DriverDetailWindow(driver, data) { Owner = this };
        window.ShowDialog();
    }

    private void VehicleDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Vehicle vehicle)
        {
            OpenVehicle(vehicle);
        }
    }

    private void AddVehicleClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.CreateVehicles)
        {
            MessageBox.Show("Votre rôle ne permet pas de modifier le parc.", "Accès refusé",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var window = new AddVehicleWindow(data) { Owner = this };
        if (window.ShowDialog() == true && window.CreatedVehicle is Vehicle vehicle)
        {
            data.Vehicles.Add(vehicle);
            RefreshTypeFilter(selectedVehicleType);
            PersistAndRefresh();
        }
        else
        {
            RefreshTypeFilter(selectedVehicleType);
            RefreshVehicles();
        }
    }

    private void ExportCsvClick(object sender, RoutedEventArgs e)
    {
        if (!permissions.Export)
        {
            MessageBox.Show("Votre rôle ne permet pas d’exporter ces données.", "Accès refusé",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Fichier CSV (*.csv)|*.csv",
            FileName = "rapport-parc-atreus.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var csv = new StringBuilder();
        csv.AppendLine("Identifiant;Type;Description;Etat;Plaque;Marque;Modèle;Chauffeur;Localisation");
        foreach (var vehicle in data.Vehicles)
        {
            csv.AppendLine(string.Join(';',
                EscapeCsv(vehicle.Identifier),
                EscapeCsv(vehicle.Type),
                EscapeCsv(vehicle.Description),
                EscapeCsv(vehicle.State),
                EscapeCsv(vehicle.Plate),
                EscapeCsv(vehicle.Brand),
                EscapeCsv(vehicle.Model),
                EscapeCsv(data.DriverName(vehicle.DriverId)),
                EscapeCsv(vehicle.Location)));
        }

        File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
        MessageBox.Show("Le rapport CSV a été exporté avec succès.", "Export terminé", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string EscapeCsv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        searchQuery = (sender as TextBox)?.Text.Trim() ?? string.Empty;
        RefreshVehicles();
    }

    private void RefreshVehicles()
    {
        if (data is null)
        {
            return;
        }

        string? selectedType = (TypeFilter?.SelectedItem as ComboBoxItem)?.Tag as string;
        string? selectedState = (StateFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var normalizedSearch = NormalizeSearch(searchQuery);

        var filteredVehicles = data.Vehicles.Where(vehicle =>
            (selectedType is null || selectedType.StartsWith("Tous", StringComparison.Ordinal) || vehicle.Type == selectedType) &&
            (selectedState is null || selectedState.StartsWith("Tous", StringComparison.Ordinal) || vehicle.State == selectedState) &&
            (normalizedSearch.Length == 0 ||
             StartsWithSearch(vehicle.Identifier, normalizedSearch) ||
             StartsWithSearch(vehicle.Plate, normalizedSearch) ||
             StartsWithSearch(data.DriverName(vehicle.DriverId), normalizedSearch)));

        VisibleVehicles.Clear();
        AlertVehicles.Clear();
        foreach (var vehicle in filteredVehicles)
        {
            vehicle.DriverName = data.DriverName(vehicle.DriverId);
            vehicle.TypeDisplay = VehicleTypes.Display(vehicle.Type, data.VehicleCategories);
            VisibleVehicles.Add(vehicle);
        }
        foreach (var vehicle in data.Vehicles.Where(vehicle => vehicle.State != "Disponible"))
        {
            AlertVehicles.Add(vehicle);
        }

        if (VehicleList is not null)
        {
            VehicleList.ItemsSource = null;
            VehicleList.ItemsSource = VisibleVehicles;
        }

        foreach (var property in new[] { nameof(ReferenceCount), nameof(AvailableCount), nameof(MaintenanceCount), nameof(UnavailableCount), nameof(AvailabilityPercentage), nameof(AlertCount) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    private void ReplaceVehicle(Vehicle updatedVehicle)
    {
        var index = data.Vehicles.FindIndex(vehicle => vehicle.Id == updatedVehicle.Id);
        if (index >= 0)
        {
            data.Vehicles[index] = updatedVehicle;
        }
        PersistAndRefresh();
    }

    private void PersistAndRefresh()
    {
        ApplicationDataStore.Save(data);
        RefreshVehicles();
        EmbeddedMapView.ShowVehicles(data);
        MaterialView.ShowVehicles(data);
        CalendarView.Refresh();
        OperationsView.Refresh();
    }

    private void SetActiveNavigation(Button activeButton)
    {
        foreach (var button in new[] { DashboardNavButton, MapNavButton, MaterialNavButton, IncidentsNavButton, MaintenanceNavButton, CalendarNavButton, DriverPlanningNavButton, SupportNavButton, SavNavButton, AccountsNavButton })
        {
            button.Background = button == activeButton ? new SolidColorBrush(Color.FromRgb(21, 51, 64)) : Brushes.Transparent;
            button.Foreground = button == activeButton ? new SolidColorBrush(Color.FromRgb(232, 238, 240)) : new SolidColorBrush(Color.FromRgb(181, 197, 201));
            button.BorderBrush = button == activeButton ? new SolidColorBrush(Color.FromRgb(200, 162, 74)) : Brushes.Transparent;
            button.BorderThickness = button == activeButton ? new Thickness(3, 0, 0, 0) : new Thickness(0);
        }
    }

    private static bool StartsWithSearch(string value, string search) =>
        NormalizeSearch(value).Contains(search, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSearch(string value) =>
        value.Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
}
