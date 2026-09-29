using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class AddVehicleWindow : Window
{
    public Vehicle? CreatedVehicle { get; private set; }

    public AddVehicleWindow()
    {
        InitializeComponent();
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var identifier = IdentifierInput.Text.Trim();
        var description = DescriptionInput.Text.Trim();
        var type = (TypeInput.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Terrestre";
        var state = (StateInput.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Disponible";

        if (identifier.Length == 0 || description.Length == 0)
        {
            MessageBox.Show("Renseigne l’identifiant et la description du véhicule.", "Informations manquantes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var stateColor = state switch
        {
            "Maintenance" => "#E2A05A",
            "Indisponible" => "#E28076",
            _ => "#7CCB9B"
        };

        CreatedVehicle = new Vehicle(identifier, type, state, description, stateColor);
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
