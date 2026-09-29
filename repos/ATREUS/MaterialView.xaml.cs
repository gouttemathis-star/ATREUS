using System.Windows.Controls;

namespace ATREUS;

public partial class MaterialView : UserControl
{
    public IReadOnlyList<Vehicle> Vehicles { get; private set; } = [];
    public int Count => Vehicles.Count;

    public MaterialView()
    {
        InitializeComponent();
    }

    public void ShowVehicles(IReadOnlyList<Vehicle> vehicles)
    {
        Vehicles = vehicles;
        DataContext = this;
    }
}
