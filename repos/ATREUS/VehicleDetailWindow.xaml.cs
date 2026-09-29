using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class VehicleDetailWindow : Window
{
    public Vehicle? UpdatedVehicle { get; private set; }
    private bool isEditing;

    public VehicleDetailWindow(Vehicle vehicle)
    {
        InitializeComponent();
        DataContext = vehicle;
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = UpdatedVehicle is not null;
    }

    private void EditClick(object sender, RoutedEventArgs e)
    {
        if (!isEditing)
        {
            if (!AccessKeyWindow.Authorize(this))
            {
                return;
            }

            SetFieldsReadOnly(false);
            isEditing = true;
            EditButton.Content = "Enregistrer";
            return;
        }

        if (IdentifierInput.Text.Trim().Length == 0 || DescriptionTextBlock.Text.Trim().Length == 0)
        {
            MessageBox.Show("L’identifiant et la description sont obligatoires.", "Informations manquantes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var state = StateInput.Text.Trim();
        var stateColor = state switch
        {
            "Maintenance" => "#E2A05A",
            "Indisponible" => "#E28076",
            _ => "#7CCB9B"
        };

        UpdatedVehicle = new Vehicle(IdentifierInput.Text.Trim(), TypeInput.Text.Trim(), state, DescriptionTextBlock.Text.Trim(), stateColor)
        {
            Plate = PlateInput.Text.Trim(),
            Mileage = MileageInput.Text.Trim(),
            Brand = BrandInput.Text.Trim(),
            Model = ModelInput.Text.Trim(),
            Registration = RegistrationInput.Text.Trim(),
            Vin = VinInput.Text.Trim(),
            AcquisitionDate = AcquisitionDateInput.Text.Trim(),
            PurchasePrice = PurchasePriceInput.Text.Trim(),
            Supplier = SupplierInput.Text.Trim(),
            ServiceDate = ServiceDateInput.Text.Trim(),
            Energy = EnergyInput.Text.Trim(),
            Power = PowerInput.Text.Trim(),
            LastInspection = LastInspectionInput.Text.Trim(),
            NextInspection = NextInspectionInput.Text.Trim(),
            Location = LocationInput.Text.Trim()
        };

        DataContext = UpdatedVehicle;
        SetFieldsReadOnly(true);
        isEditing = false;
        EditButton.Content = "Modifier";
    }

    private void SetFieldsReadOnly(bool isReadOnly)
    {
        foreach (var field in new[] { IdentifierInput, DescriptionTextBlock, StateInput, PlateInput, MileageInput, BrandInput, ModelInput, TypeInput, RegistrationInput, AcquisitionDateInput, SupplierInput, EnergyInput, VinInput, PurchasePriceInput, ServiceDateInput, PowerInput, LastInspectionInput, NextInspectionInput, LocationInput })
        {
            field.IsReadOnly = isReadOnly;
        }
    }
}
