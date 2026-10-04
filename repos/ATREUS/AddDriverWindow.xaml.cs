using System.Windows;

namespace ATREUS;

public partial class AddDriverWindow : Window
{
    public Driver? CreatedDriver { get; private set; }

    public AddDriverWindow()
    {
        InitializeComponent();
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var name = NameInput.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show("Le nom du chauffeur est obligatoire.", "Informations manquantes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CreatedDriver = new Driver
        {
            Name = name,
            Phone = PhoneInput.Text.Trim(),
            Notes = NotesInput.Text.Trim()
        };
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
