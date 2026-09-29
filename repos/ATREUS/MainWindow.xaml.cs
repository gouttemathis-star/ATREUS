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

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly List<Vehicle> vehicles =
    [
        new("VLT-OR-021", "Terrestre", "Disponible", "Véhicule tactique Orion", "#7CCB9B") { Plate = "OR-021-VT" },
        new("VBL-VM-014", "Terrestre", "Disponible", "Véhicule blindé léger Valmour", "#7CCB9B") { Plate = "VM-014-VB" },
        new("TRP-HC-044", "Terrestre", "Disponible", "Transporteur Hautclair", "#7CCB9B") { Plate = "HC-044-TR" },
        new("VBC-OR-117", "Terrestre", "Indisponible", "Véhicule de commandement Orion", "#E28076") { Plate = "OR-117-VC" },
        new("AER-OR-014", "Aérien", "Disponible", "Moyen aérien Orion", "#7CCB9B") { Plate = "AE-014-OR" },
        new("NAV-OR-002", "Maritime", "Maintenance", "Moyen maritime Orion", "#E2A05A") { Plate = "OR-002-NA" }
    ];

    public ObservableCollection<Vehicle> VisibleVehicles { get; } = [];
    public ObservableCollection<Vehicle> AlertVehicles { get; } = [];
    public ObservableCollection<Unit> Units { get; } =
    [
        new("12e Régiment Orion", "Armée de Terre · Parc Orion", 842, 6),
        new("Groupe Aérien Atlas", "Moyens aériens · Base Nord", 126, 0),
        new("Escadre Maritime Nérée", "Moyens maritimes · Port Sud", 214, 0)
    ];
    public int ReferenceCount => vehicles.Count;
    public int AvailableCount => vehicles.Count(vehicle => vehicle.State == "Disponible");
    public int MaintenanceCount => vehicles.Count(vehicle => vehicle.State == "Maintenance");
    public int UnavailableCount => vehicles.Count(vehicle => vehicle.State == "Indisponible");
    public string AvailabilityPercentage => ReferenceCount == 0 ? "0 %" : $"{AvailableCount * 100 / ReferenceCount} %";
    public int AlertCount => MaintenanceCount + UnavailableCount;

    public event PropertyChangedEventHandler? PropertyChanged;
    private string searchQuery = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        RefreshVehicles();
        SetActiveNavigation(DashboardNavButton);
        EmbeddedMapView.VehicleSelected += MapVehicleSelected;
    }

    private void FilterChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshVehicles();
    }

    private void MapClick(object sender, RoutedEventArgs e)
    {
        HideAllViews();
        DashboardView.Visibility = Visibility.Collapsed;
        EmbeddedMapView.Visibility = Visibility.Visible;
        EmbeddedMapView.ShowVehicles(vehicles);
        SetActiveNavigation(MapNavButton);
    }

    private void DashboardClick(object sender, RoutedEventArgs e)
    {
        HideAllViews();
        EmbeddedMapView.Visibility = Visibility.Collapsed;
        DashboardView.Visibility = Visibility.Visible;
        SetActiveNavigation(DashboardNavButton);
    }

    private void UnitsClick(object sender, RoutedEventArgs e)
    {
        HideAllViews();
        UnitsView.Visibility = Visibility.Visible;
        SetActiveNavigation(UnitsNavButton);
    }

    private void MaterialClick(object sender, RoutedEventArgs e)
    {
        HideAllViews();
        MaterialView.ShowVehicles(vehicles);
        MaterialView.Visibility = Visibility.Visible;
        SetActiveNavigation(MaterialNavButton);
    }

    private void IncidentsClick(object sender, RoutedEventArgs e) => ShowOperations(false, IncidentsNavButton);

    private void MaintenanceClick(object sender, RoutedEventArgs e) => ShowOperations(true, MaintenanceNavButton);

    private void ShowOperations(bool maintenance, Button activeButton)
    {
        HideAllViews();
        OperationsView.ShowData(vehicles, maintenance);
        OperationsView.Visibility = Visibility.Visible;
        SetActiveNavigation(activeButton);
    }

    private void HideAllViews()
    {
        DashboardView.Visibility = Visibility.Collapsed;
        UnitsView.Visibility = Visibility.Collapsed;
        MaterialView.Visibility = Visibility.Collapsed;
        OperationsView.Visibility = Visibility.Collapsed;
        EmbeddedMapView.Visibility = Visibility.Collapsed;
    }

    private void AddUnitClick(object sender, RoutedEventArgs e)
    {
        if (AccessKeyWindow.Authorize(this))
        {
            MessageBox.Show("La création détaillée des unités sera ajoutée avec le module de persistance.", "Régiments", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void MapVehicleSelected(object? sender, Vehicle vehicle)
    {
        var index = vehicles.IndexOf(vehicle);
        var window = new VehicleDetailWindow(vehicle) { Owner = this };
        if (window.ShowDialog() == true && window.UpdatedVehicle is Vehicle updatedVehicle)
        {
            vehicles[index] = updatedVehicle;
            RefreshVehicles();
            EmbeddedMapView.ShowVehicles(vehicles);
        }
    }

    private void SetActiveNavigation(Button activeButton)
    {
        foreach (var button in new[] { DashboardNavButton, MapNavButton, UnitsNavButton, MaterialNavButton, IncidentsNavButton, MaintenanceNavButton })
        {
            button.Background = button == activeButton ? new SolidColorBrush(Color.FromRgb(21, 51, 64)) : Brushes.Transparent;
            button.Foreground = button == activeButton ? new SolidColorBrush(Color.FromRgb(232, 238, 240)) : new SolidColorBrush(Color.FromRgb(181, 197, 201));
            button.BorderBrush = button == activeButton ? new SolidColorBrush(Color.FromRgb(200, 162, 74)) : Brushes.Transparent;
            button.BorderThickness = button == activeButton ? new Thickness(3, 0, 0, 0) : new Thickness(0);
        }
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        searchQuery = (sender as TextBox)?.Text.Trim() ?? string.Empty;
        RefreshVehicles();
    }

    private void VehicleDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Vehicle vehicle)
        {
            var index = vehicles.IndexOf(vehicle);
            var window = new VehicleDetailWindow(vehicle) { Owner = this };
            if (window.ShowDialog() == true && window.UpdatedVehicle is Vehicle updatedVehicle)
            {
                vehicles[index] = updatedVehicle;
                RefreshVehicles();
            }
        }
    }

    private void AddVehicleClick(object sender, RoutedEventArgs e)
    {
        if (!AccessKeyWindow.Authorize(this))
        {
            return;
        }

        var window = new AddVehicleWindow { Owner = this };
        if (window.ShowDialog() == true && window.CreatedVehicle is Vehicle vehicle)
        {
            vehicles.Add(vehicle);
            RefreshVehicles();
        }
    }

    private void ExportCsvClick(object sender, RoutedEventArgs e)
    {
        if (!AccessKeyWindow.Authorize(this))
        {
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
        csv.AppendLine("Identifiant;Description;Type;Etat;Plaque;Kilometrage;Localisation");
        foreach (var vehicle in vehicles)
        {
            csv.AppendLine(string.Join(';', vehicle.Identifier, vehicle.Description, vehicle.Type, vehicle.State, vehicle.Plate, vehicle.Mileage, vehicle.Location));
        }

        File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
        MessageBox.Show("Le rapport CSV a été exporté avec succès.", "Export terminé", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RefreshVehicles()
    {
        string? selectedType = (TypeFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        string? selectedState = (StateFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString();
           var normalizedSearch = NormalizeSearch(searchQuery);

        var filteredVehicles = vehicles.Where(vehicle =>
            (selectedType is null || selectedType.StartsWith("Tous") || vehicle.Type == selectedType) &&
            (selectedState is null || selectedState.StartsWith("Tous") || vehicle.State == selectedState) &&
            (normalizedSearch.Length == 0 ||
               StartsWithSearch(vehicle.Identifier, normalizedSearch) ||
             StartsWithSearch(vehicle.Plate, normalizedSearch)));

        VisibleVehicles.Clear();
        AlertVehicles.Clear();
        foreach (var vehicle in filteredVehicles)
        {
            VisibleVehicles.Add(vehicle);
        }
        foreach (var vehicle in vehicles.Where(vehicle => vehicle.State != "Disponible"))
        {
            AlertVehicles.Add(vehicle);
        }

        if (VehicleList is not null)
        {
            VehicleList.ItemsSource = null;
            VehicleList.ItemsSource = VisibleVehicles;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReferenceCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailableCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaintenanceCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnavailableCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AvailabilityPercentage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AlertCount)));
    }

    private static bool StartsWithSearch(string value, string search)
    {
        return NormalizeSearch(value).StartsWith(search, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearch(string value)
    {
        return value.Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
    }
}

public sealed record Vehicle(string Identifier, string Type, string State, string Description, string StateColor)
{
    public string Plate { get; init; } = string.Empty;
    public string Mileage { get; init; } = "42 680 km";
    public string Brand { get; init; } = "ATREUS Motors";
    public string Model { get; init; } = "Orion T4";
    public string Registration { get; init; } = "CG-OR-2023-021";
    public string Vin { get; init; } = "VF7DEMO0000OR02184";
    public string AcquisitionDate { get; init; } = "14/03/2023";
    public string PurchasePrice { get; init; } = "248 500 €";
    public string Supplier { get; init; } = "M Industrie Fleet";
    public string ServiceDate { get; init; } = "28/03/2023";
    public string Energy { get; init; } = "Diesel";
    public string Power { get; init; } = "190 ch";
    public string LastInspection { get; init; } = "12/02/2026";
    public string NextInspection { get; init; } = "12/02/2027";
    public string Location { get; init; } = "Parc Orion";
    public double Latitude { get; init; } = 46.5;
    public double Longitude { get; init; } = 2.35;
}

public sealed record Unit(string Name, string Description, int PersonnelCount, int VehicleCount);

public sealed record Incident(string Number, string VehicleIdentifier, string Nature, string Status, string Date);

public sealed record MaintenanceOperation(string Number, string VehicleIdentifier, string Nature, string Status, string Date);