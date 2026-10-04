using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class SiteLocationWindow : Window
{
    private readonly SiteLocation? originalSite;
    private bool updatingKind;

    public SiteLocation? CreatedSite { get; private set; }
    public SiteLocation? UpdatedSite { get; private set; }

    public SiteLocationWindow(SiteLocation? site = null)
    {
        InitializeComponent();
        originalSite = site;
        KindInput.ItemsSource = SiteKinds.Defaults;
        IconInput.ItemsSource = new[]
        {
            new SiteIconOption("🏢", "Siège"),
            new SiteIconOption("🚗", "Parc automobile"),
            new SiteIconOption("🏭", "Dépôt"),
            new SiteIconOption("🔧", "Garagiste"),
            new SiteIconOption("📍", "Lieu"),
            new SiteIconOption("⛽", "Station-service"),
            new SiteIconOption("🅿️", "Parking"),
            new SiteIconOption("📦", "Logistique"),
            new SiteIconOption("🏥", "Centre médical"),
            new SiteIconOption("🚧", "Zone de travaux")
        };

        updatingKind = true;
        KindInput.SelectedItem = SiteKinds.Defaults.FirstOrDefault(kind => kind.Name == (site?.Kind ?? SiteKinds.Headquarters))
            ?? SiteKinds.Defaults[0];
        IconInput.SelectedItem = IconInput.Items.Cast<SiteIconOption>().FirstOrDefault(icon => icon.Icon == (site?.Icon ?? "🏢"))
            ?? IconInput.Items.Cast<SiteIconOption>().First();
        updatingKind = false;

        if (site is not null)
        {
            WindowTitle.Text = "Modifier le site";
            NameInput.Text = site.Name;
            LatitudeInput.Text = site.Latitude.ToString(CultureInfo.InvariantCulture);
            LongitudeInput.Text = site.Longitude.ToString(CultureInfo.InvariantCulture);
            DetailsInput.Text = site.Details;
        }
        else
        {
            LatitudeInput.Text = "48.8566";
            LongitudeInput.Text = "2.3522";
        }
    }

    private void KindSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingKind || KindInput.SelectedItem is not SiteKindOption kind)
        {
            return;
        }
        IconInput.SelectedItem = IconInput.Items.Cast<SiteIconOption>().FirstOrDefault(icon => icon.Icon == kind.Icon);
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameInput.Text))
        {
            ShowValidationError("Saisissez le nom du lieu.");
            return;
        }
        if (!TryParseCoordinate(LatitudeInput.Text, out var latitude) || latitude is < -90 or > 90)
        {
            ShowValidationError("La latitude doit être comprise entre −90 et 90.");
            return;
        }
        if (!TryParseCoordinate(LongitudeInput.Text, out var longitude) || longitude is < -180 or > 180)
        {
            ShowValidationError("La longitude doit être comprise entre −180 et 180.");
            return;
        }
        if (KindInput.SelectedItem is not SiteKindOption kind || IconInput.SelectedItem is not SiteIconOption icon)
        {
            ShowValidationError("Sélectionnez un type de lieu et une icône.");
            return;
        }

        var site = new SiteLocation
        {
            Id = originalSite?.Id ?? Guid.NewGuid().ToString("N"),
            Name = NameInput.Text.Trim(),
            Kind = kind.Name,
            Icon = icon.Icon,
            Latitude = latitude,
            Longitude = longitude,
            Details = DetailsInput.Text.Trim()
        };
        if (originalSite is null)
        {
            CreatedSite = site;
        }
        else
        {
            UpdatedSite = site;
        }
        DialogResult = true;
    }

    private static bool TryParseCoordinate(string input, out double value) =>
        double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        || double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

    private void ShowValidationError(string message)
    {
        MessageBox.Show(this, message, "Vérification du site", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private sealed record SiteIconOption(string Icon, string Name)
    {
        public string Display => $"{Icon}  {Name}";
    }
}
