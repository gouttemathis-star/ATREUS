using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class OperationsView : UserControl
{
    private bool maintenanceMode;
    private IReadOnlyList<Vehicle> vehicles = [];

    public OperationsView()
    {
        InitializeComponent();
    }

    public void ShowData(IReadOnlyList<Vehicle> sourceVehicles, bool maintenance)
    {
        vehicles = sourceVehicles;
        maintenanceMode = maintenance;
        TitleLabel.Text = maintenance ? "Maintenance" : "Incidents";
        SectionLabel.Text = maintenance ? "ATREUS · SUIVI TECHNIQUE" : "ATREUS · SUIVI OPÉRATIONNEL";
        var entries = maintenance
            ? vehicles.Where(vehicle => vehicle.State == "Maintenance").Select((vehicle, index) => new OperationEntry($"MAI-{index + 1:000}", vehicle.Identifier, "Opération de maintenance", vehicle.State, "23/09/2026"))
            : vehicles.Where(vehicle => vehicle.State != "Disponible").Select((vehicle, index) => new OperationEntry($"INC-{index + 1:000}", vehicle.Identifier, "Anomalie à traiter", vehicle.State, "23/09/2026"));
        EntriesList.ItemsSource = entries.ToList();
        CountLabel.Text = $"{EntriesList.Items.Count} entrée(s)";
    }

    private void AddClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Le formulaire détaillé sera ajouté avec la persistance des données.", TitleLabel.Text, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public sealed record OperationEntry(string Number, string VehicleIdentifier, string Nature, string Status, string Date);
