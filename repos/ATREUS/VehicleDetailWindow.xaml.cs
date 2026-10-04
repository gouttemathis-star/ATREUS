using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class VehicleDetailWindow : Window
{
    private readonly Vehicle vehicle;
    private readonly ApplicationData data;
    private readonly bool canEdit;
    private bool isEditing;
    private bool updatingTypes;
    private string? selectedTypeName;
    public Vehicle? UpdatedVehicle { get; private set; }

    public VehicleDetailWindow(Vehicle vehicle, ApplicationData data, bool? allowEdit = null)
    {
        this.vehicle = vehicle;
        this.data = data;
        canEdit = allowEdit ?? UserRoles.Get((Application.Current as App)?.AuthenticatedUser?.Role).ManageFleet;
        InitializeComponent();
        EditButton.Visibility = canEdit ? Visibility.Visible : Visibility.Collapsed;
        RefreshTypes(vehicle.Type);
        DriverInput.ItemsSource = data.Drivers;
        LoadVehicle();
        RefreshHistory();
        RefreshTasks();
    }

    private void LoadVehicle()
    {
        vehicle.TypeDisplay = VehicleTypes.Display(vehicle.Type, data.VehicleCategories);
        TitleLabel.Text = $"{vehicle.Identifier} · {vehicle.Description}";
        IdentifierInput.Text = vehicle.Identifier;
        DescriptionInput.Text = vehicle.Description;
        TypeInput.SelectedItem = TypeInput.Items.Cast<VehicleCategoryChoice>()
            .FirstOrDefault(choice => !choice.IsCreateAction && choice.Name == vehicle.Type);
        StateInput.SelectedItem = StateInput.Items.Cast<ComboBoxItem>().First(item => item.Content?.ToString() == vehicle.State);
        BrandInput.Text = vehicle.Brand;
        ModelInput.Text = vehicle.Model;
        PlateInput.Text = vehicle.Plate;
        MileageInput.Text = vehicle.Mileage;
        DriverInput.SelectedValue = vehicle.DriverId;
        DriverLabel.Text = $"Chauffeur : {data.DriverName(vehicle.DriverId)} · Statut : {vehicle.State}";
        LocationInput.Text = vehicle.Location;
        LatitudeInput.Text = vehicle.Latitude.ToString(CultureInfo.InvariantCulture);
        LongitudeInput.Text = vehicle.Longitude.ToString(CultureInfo.InvariantCulture);
        EnergyInput.Text = vehicle.Energy;
        PowerInput.Text = vehicle.Power;
        NextInspectionInput.Text = vehicle.NextInspection;
        RegistrationInput.Text = vehicle.Registration;
        VinInput.Text = vehicle.Vin;
        PurchasePriceInput.Text = vehicle.PurchasePrice;
        AcquisitionDateInput.Text = vehicle.AcquisitionDate;
        ServiceDateInput.Text = vehicle.ServiceDate;
        LastInspectionInput.Text = vehicle.LastInspection;
        DriverButton.IsEnabled = data.FindDriver(vehicle.DriverId) is not null;
    }

    private void RefreshHistory()
    {
        var permissions = UserRoles.Get((Application.Current as App)?.AuthenticatedUser?.Role);
        var visibleKinds = new HashSet<string>(StringComparer.Ordinal);
        if (permissions.Incidents) visibleKinds.Add(OperationKinds.Incident);
        if (permissions.Maintenance) visibleKinds.Add(OperationKinds.Maintenance);
        if (permissions.ViewTrips) visibleKinds.Add(OperationKinds.Trip);
        var operations = data.Operations.Where(operation => operation.VehicleId == vehicle.Id)
            .Where(operation => visibleKinds.Contains(operation.Kind))
            .OrderByDescending(operation => operation.Date)
            .Select(operation => new HistoryRow(operation, operation.Date.ToString("dd/MM/yyyy"), data.DriverName(operation.DriverId)))
            .ToList();
        HistoryList.ItemsSource = operations;
        HistoryCountLabel.Text = $"{operations.Count} entrée(s)";
        EmptyHistoryLabel.Visibility = operations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshTasks()
    {
        var tasks = data.Tasks.Where(task => task.VehicleId == vehicle.Id)
            .OrderBy(task => task.DueAt)
            .Select(task => new VehicleTaskRow($"{task.DueAt:dd/MM/yyyy}{(task.HasTime ? $" · {task.DueAt:HH:mm}{(task.EndAt is DateTime endAt ? $"–{endAt:HH:mm}" : string.Empty)}" : string.Empty)} · {task.Title} · {task.Status}", task))
            .ToList();
        VehicleTasksList.ItemsSource = tasks;
        TaskCountLabel.Text = $"{tasks.Count} tâche(s)";
        EmptyTasksLabel.Visibility = tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EditClick(object sender, RoutedEventArgs e)
    {
        if (!isEditing)
        {
            if (!canEdit)
            {
                MessageBox.Show("Votre rôle ne permet pas de modifier cette fiche véhicule.", "Accès refusé",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            isEditing = true;
            SetFieldsEnabled(true);
            EditButton.Visibility = Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Visible;
            return;
        }

        SaveVehicle();
    }

    private void SaveClick(object sender, RoutedEventArgs e) => SaveVehicle();

    private void SaveVehicle()
    {
        var identifier = IdentifierInput.Text.Trim();
        var description = DescriptionInput.Text.Trim();
        if (identifier.Length == 0 || description.Length == 0)
        {
            MessageBox.Show("L’identifiant et la description du véhicule sont obligatoires.", "Informations manquantes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (data.Vehicles.Any(other => other.Id != vehicle.Id && string.Equals(other.Identifier, identifier, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Cet identifiant de véhicule existe déjà.", "Identifiant déjà utilisé", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!double.TryParse(LatitudeInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
            !double.TryParse(LongitudeInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            MessageBox.Show("Les coordonnées doivent être des nombres décimaux valides.", "Coordonnées incorrectes", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        vehicle.Identifier = identifier;
        vehicle.Description = description;
        vehicle.Type = (TypeInput.SelectedItem as VehicleCategoryChoice)?.Name ?? VehicleTypes.Defaults[0].Name;
        vehicle.State = (StateInput.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Disponible";
        vehicle.Brand = BrandInput.Text.Trim();
        vehicle.Model = ModelInput.Text.Trim();
        vehicle.Plate = PlateInput.Text.Trim();
        vehicle.Mileage = MileageInput.Text.Trim();
        vehicle.DriverId = DriverInput.SelectedValue as string;
        vehicle.Location = LocationInput.Text.Trim();
        vehicle.Latitude = latitude;
        vehicle.Longitude = longitude;
        vehicle.Energy = EnergyInput.Text.Trim();
        vehicle.Power = PowerInput.Text.Trim();
        vehicle.NextInspection = NextInspectionInput.Text.Trim();
        vehicle.Registration = RegistrationInput.Text.Trim();
        vehicle.Vin = VinInput.Text.Trim();
        vehicle.PurchasePrice = PurchasePriceInput.Text.Trim();
        vehicle.AcquisitionDate = AcquisitionDateInput.Text.Trim();
        vehicle.ServiceDate = ServiceDateInput.Text.Trim();
        vehicle.LastInspection = LastInspectionInput.Text.Trim();
        vehicle.DriverName = data.DriverName(vehicle.DriverId);
        vehicle.TypeDisplay = VehicleTypes.Display(vehicle.Type, data.VehicleCategories);
        ApplicationDataStore.Save(data);
        UpdatedVehicle = vehicle;
        LoadVehicle();
        SetFieldsEnabled(false);
        isEditing = false;
        EditButton.Visibility = Visibility.Visible;
        SaveButton.Visibility = Visibility.Collapsed;
    }

    private void SetFieldsEnabled(bool enabled)
    {
        foreach (var field in new[] { IdentifierInput, DescriptionInput, BrandInput, ModelInput, PlateInput, MileageInput, LocationInput, LatitudeInput, LongitudeInput, EnergyInput, PowerInput, NextInspectionInput, RegistrationInput, VinInput, PurchasePriceInput, AcquisitionDateInput, ServiceDateInput, LastInspectionInput })
        {
            field.IsReadOnly = !enabled;
        }
        TypeInput.IsEnabled = enabled;
        StateInput.IsEnabled = enabled;
        DriverInput.IsEnabled = enabled;
    }

    private void RefreshTypes(string? selectedName)
    {
        updatingTypes = true;
        TypeInput.ItemsSource = VehicleTypes.Choices(data.VehicleCategories);
        TypeInput.SelectedItem = TypeInput.Items.Cast<VehicleCategoryChoice>()
            .FirstOrDefault(choice => !choice.IsCreateAction && choice.Name == selectedName)
            ?? TypeInput.Items.Cast<VehicleCategoryChoice>().First(choice => !choice.IsCreateAction);
        selectedTypeName = (TypeInput.SelectedItem as VehicleCategoryChoice)?.Name;
        updatingTypes = false;
    }

    private void TypeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingTypes || !isEditing || TypeInput.SelectedItem is not VehicleCategoryChoice choice || !choice.IsCreateAction)
        {
            return;
        }

        var dialog = new AddVehicleCategoryWindow(data) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.CreatedCategory is VehicleCategory category)
        {
            RefreshTypes(category.Name);
        }
        else
        {
            RefreshTypes(selectedTypeName);
        }
    }

    private void DriverClick(object sender, RoutedEventArgs e)
    {
        if (data.FindDriver(vehicle.DriverId) is Driver driver)
        {
            var dialog = new DriverDetailWindow(driver, data) { Owner = this };
            dialog.ShowDialog();
        }
    }

    private void HistoryItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is OperationRecord operation)
        {
            var dialog = new OperationEditWindow(operation, operation.Kind, data) { Owner = this };
            dialog.ShowDialog();
            RefreshHistory();
            RefreshTasks();
        }
    }

    private void TaskItemClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TaskItem task)
        {
            new TaskEditWindow(task, data) { Owner = this }.ShowDialog();
            RefreshTasks();
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        if (isEditing)
        {
            var result = MessageBox.Show("Abandonner les modifications non enregistrées ?", "Modifications en cours", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }
        DialogResult = UpdatedVehicle is not null;
    }

    private sealed record HistoryRow(OperationRecord Operation, string Date, string DriverName);
    private sealed record VehicleTaskRow(string Display, TaskItem Task);
}
