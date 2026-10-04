using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class AddVehicleCategoryWindow : Window
{
    private readonly ApplicationData data;
    public VehicleCategory? CreatedCategory { get; private set; }

    public AddVehicleCategoryWindow(ApplicationData data)
    {
        this.data = data;
        InitializeComponent();
    }

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        var name = NameInput.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show("Saisissez un nom pour la catégorie.", "Nom requis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (name.Equals("Autre véhicule", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Créer une catégorie de véhicule", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Choisissez un nom de catégorie différent de l’ancienne option.", "Nom non disponible", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (data.VehicleCategories.Any(category => string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Cette catégorie existe déjà.", "Catégorie déjà présente", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var emoji = (EmojiInput.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "🚘";
        CreatedCategory = new VehicleCategory { Name = name, Emoji = emoji };
        data.VehicleCategories.Add(CreatedCategory);
        ApplicationDataStore.Save(data);
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
