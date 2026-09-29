using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ATREUS;

public partial class MapWindow : Window
{
    public IReadOnlyList<Vehicle> Vehicles { get; }
    public int Count => Vehicles.Count;

    private static readonly Point[] VehiclePositions =
    [
        new(180, 330),
        new(275, 330),
        new(370, 330),
        new(465, 330),
        new(850, 285),
        new(790, 420)
    ];

    public MapWindow(IReadOnlyList<Vehicle> vehicles)
    {
        InitializeComponent();
        Vehicles = vehicles ?? Array.Empty<Vehicle>();
        DataContext = this;
        Loaded += (_, _) => DrawVehiclePoints();
    }

    private static bool TryCreateVehicleBrush(string? stateColor, out Brush brush)
    {
        brush = Brushes.Gray;

        if (string.IsNullOrWhiteSpace(stateColor))
        {
            return true;
        }

        try
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(stateColor));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void DrawVehiclePoints()
    {
        MapCanvas.Children.Clear();

        for (var index = 0; index < Vehicles.Count; index++)
        {
            var vehicle = Vehicles[index];
            if (vehicle is null)
            {
                continue;
            }

            var position = VehiclePositions[index % VehiclePositions.Length];
            var fill = TryCreateVehicleBrush(vehicle.StateColor, out var brush)
                ? brush
                : Brushes.Gray;

            var marker = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = fill,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                ToolTip = $"{vehicle.Identifier ?? "Unknown"} · {vehicle.State ?? "Unknown"}",
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var label = new TextBlock
            {
                Text = vehicle.Identifier ?? "Unknown",
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(210, 6, 21, 29)),
                Padding = new Thickness(4, 2, 4, 2),
                ToolTip = $"{vehicle.Description ?? "No description"} · {vehicle.State ?? "Unknown"}"
            };

            Canvas.SetLeft(marker, position.X);
            Canvas.SetTop(marker, position.Y);
            Canvas.SetLeft(label, position.X + 22);
            Canvas.SetTop(label, position.Y - 4);
            MapCanvas.Children.Add(marker);
            MapCanvas.Children.Add(label);
        }
    }
}
