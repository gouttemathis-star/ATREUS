using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class EditVehicleWindow : Window
{
    public Vehicle? UpdatedVehicle { get; private set; }

    public EditVehicleWindow(Vehicle vehicle)
    {
        InitializeComponent();
        IdentifierInput.Text = vehicle.Identifier;
        DescriptionInput.Text = vehicle.Description;
        TypeInput.SelectedIndex = TypeInput.Items.Cast<ComboBoxItem>().ToList().FindIndex(item => item.Content?.ToString() == vehicle.Type);
        StateInput.SelectedIndex = StateInput.Items.Cast<ComboBoxItem>().ToList().FindIndex(item => item.Content?.ToString() == vehicle.State);
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

        UpdatedVehicle = new Vehicle(identifier, type, state, description, stateColor);
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
