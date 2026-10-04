using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class AddVehicleWindow : Window
{
    private readonly ApplicationData data;
    private bool updatingTypes;
    private string? selectedTypeName;
    public Vehicle? CreatedVehicle { get; private set; }

    public AddVehicleWindow(ApplicationData data)
    {
        this.data = data;
        InitializeComponent();
        RefreshTypes(VehicleTypes.Defaults[0].Name);
        StateInput.SelectedIndex = 0;
        DriverInput.ItemsSource = data.Drivers;
    }

    private void RefreshTypes(string? selectedName = null)
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
        if (updatingTypes || TypeInput.SelectedItem is not VehicleCategoryChoice choice || !choice.IsCreateAction)
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

    private void AddDriverClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AddDriverWindow { Owner = this };
        if (dialog.ShowDialog() == true && dialog.CreatedDriver is Driver driver)
        {
            data.Drivers.Add(driver);
            ApplicationDataStore.Save(data);
            DriverInput.ItemsSource = null;
            DriverInput.ItemsSource = data.Drivers;
            DriverInput.SelectedValue = driver.Id;
        }
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var identifier = IdentifierInput.Text.Trim();
        var description = DescriptionInput.Text.Trim();
        var type = (TypeInput.SelectedItem as VehicleCategoryChoice)?.Name ?? VehicleTypes.Defaults[0].Name;
        var state = (StateInput.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Disponible";
        if (identifier.Length == 0 || description.Length == 0)
        {
            MessageBox.Show("Renseigne l’identifiant et la description du véhicule.", "Informations manquantes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (data.Vehicles.Any(vehicle => string.Equals(vehicle.Identifier, identifier, StringComparison.OrdinalIgnoreCase)))
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

        CreatedVehicle = new Vehicle
        {
            Identifier = identifier,
            Description = description,
            Type = type,
            State = state,
            Brand = BrandInput.Text.Trim(),
            Model = ModelInput.Text.Trim(),
            Plate = PlateInput.Text.Trim(),
            Mileage = MileageInput.Text.Trim(),
            DriverId = DriverInput.SelectedValue as string,
            Location = LocationInput.Text.Trim(),
            Latitude = latitude,
            Longitude = longitude
        };
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
