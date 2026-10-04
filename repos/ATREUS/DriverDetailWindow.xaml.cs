using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class DriverDetailWindow : Window
{
    private readonly ApplicationData data;
    private readonly Driver driver;

    public DriverDetailWindow(Driver driver, ApplicationData data)
    {
        InitializeComponent();
        this.data = data;
        this.driver = driver;
        RefreshDriver();
    }

    private void RefreshDriver()
    {
        NameLabel.Text = driver.Name;
        PhoneLabel.Text = string.IsNullOrWhiteSpace(driver.Phone) ? "Téléphone non renseigné" : driver.Phone;
        NotesLabel.Text = string.IsNullOrWhiteSpace(driver.Notes) ? "Aucune note." : driver.Notes;
        var assignedVehicles = data.Vehicles.Where(vehicle => vehicle.DriverId == driver.Id)
            .Select(vehicle => new VehicleRow($"{vehicle.Identifier} · {vehicle.Type} · {vehicle.Location}", vehicle))
            .ToList();
        VehiclesList.ItemsSource = assignedVehicles;
        EmptyLabel.Visibility = assignedVehicles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var currentUser = (Application.Current as App)?.AuthenticatedUser;
        var permissions = currentUser is null ? null : AccessControl.Resolve(currentUser);
        var assignedOperations = data.Operations.Where(operation =>
                (operation.DriverId == driver.Id || data.FindVehicle(operation.VehicleId)?.DriverId == driver.Id) &&
                (operation.Kind == OperationKinds.Incident && permissions?.Incidents == true ||
                 operation.Kind == OperationKinds.Maintenance && permissions?.Maintenance == true ||
                 operation.Kind == OperationKinds.Trip &&
                    (permissions?.GroupCalendar == true || permissions?.PersonalCalendar == true && currentUser?.DriverId == driver.Id)))
            .OrderByDescending(operation => operation.Date)
            .Select(operation => new OperationRow($"{operation.Date:dd/MM/yyyy} · {operation.Kind} · {operation.Nature} · {operation.Status}", operation))
            .ToList();
        OperationsList.ItemsSource = assignedOperations;
        EmptyOperationsLabel.Visibility = assignedOperations.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void VehicleClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Vehicle vehicle)
        {
            var detail = new VehicleDetailWindow(vehicle, data) { Owner = this };
            if (detail.ShowDialog() == true)
            {
                if (detail.WasDeleted)
                {
                    data.Vehicles.Remove(vehicle);
                }
                ApplicationDataStore.Save(data);
                RefreshDriver();
            }
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void OperationClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is OperationRecord operation &&
            (Application.Current as App)?.AuthenticatedUser is UserAccount actor &&
            operation.Kind switch
            {
                var kind when kind == OperationKinds.Incident => AccessControl.Resolve(actor).ManageIncidents,
                var kind when kind == OperationKinds.Maintenance => AccessControl.Resolve(actor).ManageMaintenance,
                _ => AccessControl.Resolve(actor).ManageTrips
            })
        {
            new OperationEditWindow(operation, operation.Kind, data) { Owner = this }.ShowDialog();
        }
    }

    private sealed record VehicleRow(string Display, Vehicle Vehicle);
    private sealed record OperationRow(string Display, OperationRecord Operation);
}
