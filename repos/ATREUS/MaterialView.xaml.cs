using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class MaterialView : UserControl
{
    private ApplicationData? data;
    public event Action<Vehicle>? VehicleRequested;

    public MaterialView()
    {
        InitializeComponent();
    }

    public void ShowVehicles(ApplicationData source)
    {
        data = source;
        Refresh();
    }

    public void Refresh()
    {
        if (data is null || VehiclesList is null)
        {
            return;
        }
        var rows = data.Vehicles.Select(vehicle =>
        {
            vehicle.DriverName = data.DriverName(vehicle.DriverId);
            vehicle.TypeDisplay = VehicleTypes.Display(vehicle.Type, data.VehicleCategories);
            var makeModel = string.Join(" ", new[] { vehicle.Brand, vehicle.Model }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return new VehicleRow(vehicle, string.IsNullOrWhiteSpace(makeModel) ? "Non renseigné" : makeModel, vehicle.DriverName);
        }).ToList();
        VehiclesList.ItemsSource = rows;
        CountLabel.Text = $"{rows.Count} véhicule(s)";
    }

    private void VehicleClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Vehicle vehicle)
        {
            VehicleRequested?.Invoke(vehicle);
        }
    }

    private sealed record VehicleRow(Vehicle Vehicle, string MakeModel, string DriverName);
}
