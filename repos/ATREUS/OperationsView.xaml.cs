using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class OperationsView : UserControl
{
    private ApplicationData? data;
    private string kind = OperationKinds.Incident;
    private bool canManage;
    public event Action<Vehicle>? VehicleRequested;
    public event Action<Driver>? DriverRequested;

    public OperationsView()
    {
        InitializeComponent();
    }

    public void ShowData(ApplicationData source, string operationKind, bool canManage)
    {
        data = source;
        kind = operationKind;
        this.canManage = canManage;
        AddOperationButton.Visibility = canManage ? Visibility.Visible : Visibility.Collapsed;
        TitleLabel.Text = operationKind switch
        {
            OperationKinds.Maintenance => "Maintenance",
            OperationKinds.Trip => "Trajets",
            _ => "Incidents"
        };
        SectionLabel.Text = operationKind == OperationKinds.Maintenance ? "ATREUS · SUIVI TECHNIQUE" : "ATREUS · SUIVI OPÉRATIONNEL";
        Refresh();
    }

    public void Refresh()
    {
        if (data is null)
        {
            return;
        }

        var rows = data.Operations.Where(operation => operation.Kind == kind)
            .OrderByDescending(operation => operation.Date)
            .Select(operation =>
            {
                var vehicle = data.FindVehicle(operation.VehicleId);
                return new OperationRow(
                    operation,
                    vehicle,
                    data.FindDriver(operation.DriverId),
                    vehicle?.Identifier ?? "Véhicule supprimé",
                    data.DriverName(operation.DriverId),
                    operation.Date.ToString("dd/MM/yyyy") + (string.IsNullOrWhiteSpace(operation.Time) ? string.Empty : $" · {operation.Time}"),
                    canManage);
            }).ToList();
        EntriesList.ItemsSource = rows;
        CountLabel.Text = $"{rows.Count} {TitleLabel.Text.ToLowerInvariant()}";
    }

    private void AddClick(object sender, RoutedEventArgs e)
    {
        if (data is null || !canManage)
        {
            return;
        }
        var dialog = new OperationEditWindow(null, kind, data) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            Refresh();
        }
    }

    private void EntryClick(object sender, RoutedEventArgs e)
    {
        if (canManage && (sender as FrameworkElement)?.Tag is OperationRecord operation && data is not null)
        {
            var dialog = new OperationEditWindow(operation, operation.Kind, data) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                Refresh();
            }
        }
    }

    private void VehicleClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Vehicle vehicle)
        {
            VehicleRequested?.Invoke(vehicle);
            Refresh();
        }
    }

    private void DriverClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Driver driver)
        {
            DriverRequested?.Invoke(driver);
        }
    }

    private void DeleteClick(object sender, RoutedEventArgs e)
    {
        if (!canManage || (sender as FrameworkElement)?.Tag is not OperationRecord operation || data is null)
        {
            return;
        }
        if (MessageBox.Show($"Supprimer {operation.Number} ?", "Confirmer la suppression", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        data.Operations.Remove(operation);
        foreach (var incident in data.Operations.Where(item => item.TripId == operation.Id))
        {
            incident.TripId = null;
        }
        foreach (var task in data.Tasks.Where(task => task.OperationId == operation.Id))
        {
            task.OperationId = null;
        }
        ApplicationDataStore.Save(data);
        Refresh();
    }

    private sealed record OperationRow(OperationRecord Operation, Vehicle? Vehicle, Driver? Driver, string VehicleIdentifier, string DriverName, string DateTimeLabel, bool CanEdit);
}
