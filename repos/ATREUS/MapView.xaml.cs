using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui.Tiling.Layers;

namespace ATREUS;

public partial class MapView : UserControl
{
    public IReadOnlyList<Vehicle> Vehicles { get; private set; } = [];
    public int Count => Vehicles.Count;
    public event EventHandler<Vehicle>? VehicleSelected;

    private static readonly Point[] Positions =
    [
        new(180, 325), new(275, 325), new(370, 325), new(465, 325), new(820, 255), new(760, 410)
    ];

    public MapView()
    {
        InitializeComponent();
        InitializeSatelliteMap();
        SetMapMode(false);
    }

    private void InitializeSatelliteMap()
    {
        SatelliteMap.Map.BackColor = Mapsui.Styles.Color.FromString("#0B252D");

        var tileSource = new HttpTileSource(
            new GlobalSphericalMercator(),
            "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
            name: "Esri World Imagery",
            attribution: new BruTile.Attribution("© Esri, Maxar, Earthstar Geographics", "https://www.esri.com/legal/copyright-trademarks"));

        SatelliteMap.Map.Layers.Add(new TileLayer(tileSource));
        SatelliteMap.Map.Navigator.OverrideResolutions = new[]
        {
            19531.25d,
            9765.625d, 4882.8125d, 2441.40625d, 1220.703125d,
            610.3515625d, 305.17578125d, 152.587890625d, 76.2939453125d,
            38.14697265625d, 19.073486328125d
        };
        SatelliteMap.Map.Navigator.ZoomToLevel(0);
    }

    private void ConceptualModeClick(object sender, RoutedEventArgs e)
    {
        SetMapMode(false);
    }

    private void SatelliteModeClick(object sender, RoutedEventArgs e)
    {
        SetMapMode(true);
    }

    private void SetMapMode(bool satellite)
    {
        MapCanvas.Visibility = satellite ? Visibility.Collapsed : Visibility.Visible;
        SatelliteMap.Visibility = satellite ? Visibility.Visible : Visibility.Collapsed;
        ConceptualModeButton.Background = !satellite ? new SolidColorBrush(Color.FromRgb(200, 162, 74)) : Brushes.Transparent;
        SatelliteModeButton.Background = satellite ? new SolidColorBrush(Color.FromRgb(200, 162, 74)) : Brushes.Transparent;
        ConceptualModeButton.Foreground = !satellite ? new SolidColorBrush(Color.FromRgb(6, 21, 29)) : Brushes.White;
        SatelliteModeButton.Foreground = satellite ? new SolidColorBrush(Color.FromRgb(6, 21, 29)) : Brushes.White;
    }

    public void ShowVehicles(IReadOnlyList<Vehicle> vehicles)
    {
        Vehicles = vehicles;
        DataContext = this;
        VehicleLayer.Children.Clear();
        foreach (var (vehicle, index) in Vehicles.Select((vehicle, index) => (vehicle, index)))
        {
            var position = Positions[index % Positions.Length];
            var marker = new Ellipse
            {
                Width = 18, Height = 18,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(vehicle.StateColor)),
                Stroke = Brushes.White, StrokeThickness = 2,
                ToolTip = $"{vehicle.Identifier} · {vehicle.State}"
            };
            marker.MouseLeftButtonDown += (_, _) => VehicleSelected?.Invoke(this, vehicle);
            var label = new TextBlock { Text = vehicle.Identifier, Foreground = Brushes.White, FontSize = 11, FontWeight = FontWeights.Bold, Background = new SolidColorBrush(Color.FromArgb(220, 6, 21, 29)), Padding = new Thickness(4, 2, 4, 2) };
            label.MouseLeftButtonDown += (_, _) => VehicleSelected?.Invoke(this, vehicle);
            Canvas.SetLeft(marker, position.X); Canvas.SetTop(marker, position.Y);
            Canvas.SetLeft(label, position.X + 22); Canvas.SetTop(label, position.Y - 4);
            VehicleLayer.Children.Add(marker); VehicleLayer.Children.Add(label);
        }
    }

    private void VehicleListItemClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Vehicle vehicle)
        {
            VehicleSelected?.Invoke(this, vehicle);
        }
    }
}
