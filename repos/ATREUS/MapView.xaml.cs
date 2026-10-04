using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace ATREUS;

public partial class MapView : UserControl
{
    private ApplicationData? data;
    private bool globeReady;

    public event EventHandler<Vehicle>? VehicleSelected;

    public MapView()
    {
        InitializeComponent();
        Loaded += MapView_Loaded;
        GlobeBrowser.SizeChanged += (_, _) => SendMapMessage(new { type = "resize" });
    }

    public void ShowVehicles(ApplicationData source)
    {
        data = source;
        var rows = source.Vehicles.Select(vehicle =>
        {
            vehicle.DriverName = source.DriverName(vehicle.DriverId);
            vehicle.TypeDisplay = VehicleTypes.Display(vehicle.Type, source.VehicleCategories);
            var vehicleDescription = string.Join(" · ", new[] { $"{vehicle.Brand} {vehicle.Model}".Trim(), vehicle.Plate, vehicle.Registration }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase));
            var mileage = vehicle.Mileage.Trim();
            var mileageLabel = string.IsNullOrWhiteSpace(mileage)
                ? null
                : mileage.EndsWith("km", StringComparison.OrdinalIgnoreCase) ||
                  mileage.EndsWith("h", StringComparison.OrdinalIgnoreCase) ||
                  mileage.Contains("heure", StringComparison.OrdinalIgnoreCase)
                    ? mileage
                    : $"{mileage} km";
            var operationalInfo = string.Join(" · ", new[]
            {
                mileageLabel,
                string.IsNullOrWhiteSpace(vehicle.NextInspection) ? null : $"Prochain contrôle : {vehicle.NextInspection}"
            }.Where(value => value is not null));
            return new VehicleRow(
                vehicle,
                string.IsNullOrWhiteSpace(vehicle.Location) ? "Lieu non renseigné" : vehicle.Location,
                vehicle.DriverName,
                string.IsNullOrWhiteSpace(vehicleDescription) ? "Modèle / immatriculation non renseignés" : vehicleDescription,
                operationalInfo);
        }).ToList();

        VehicleList.ItemsSource = rows;
        CountLabel.Text = $"{rows.Count} véhicule(s) suivi(s)";
        var openOperations = source.Operations.Count(operation => operation.Status is not "Terminée" and not "Annulée" and not "Clôturé");
        var pendingTasks = source.Tasks.Count(task => task.Status is not "Terminée" and not "Annulée");
        OverviewLabel.Text = $"{rows.Count} véhicule(s) · {source.Drivers.Count} chauffeur(s) · {source.Sites.Count} site(s)\n" +
            $"{openOperations} opération(s) active(s) · {pendingTasks} tâche(s) à suivre";
        var sites = source.Sites.Select(site => new SiteRow(
            site,
            $"{site.Icon}  {site.Name} · {site.Kind}",
            $"{site.Latitude:0.####}, {site.Longitude:0.####}",
            site.Details)).ToList();
        SiteList.ItemsSource = sites;
        SiteCountLabel.Text = $"{sites.Count} site(s) personnalisé(s)";
        if (globeReady)
        {
            SendMapData();
        }
    }

    private async void MapView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MapView_Loaded;
        try
        {
            await GlobeBrowser.EnsureCoreWebView2Async();
            GlobeBrowser.CoreWebView2.WebMessageReceived += GlobeMessageReceived;
            GlobeBrowser.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    GlobeStatus.Text = $"ERREUR DE CHARGEMENT · {args.WebErrorStatus}";
                }
            };
            var mapPath = Path.Combine(AppContext.BaseDirectory, "GlobeMap.html");
            if (!File.Exists(mapPath))
            {
                throw new FileNotFoundException("Le fichier de la carte du globe est introuvable.", mapPath);
            }
            GlobeBrowser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets.atreus",
                AppContext.BaseDirectory,
                CoreWebView2HostResourceAccessKind.Allow);
            GlobeBrowser.CoreWebView2.Navigate("https://appassets.atreus/GlobeMap.html");
        }
        catch (Exception exception)
        {
            GlobeStatus.Text = "GLOBE INDISPONIBLE";
            MessageBox.Show(
                $"Impossible d'initialiser le globe interactif : {exception.Message}",
                "Carte ATREUS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void GlobeMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var message = JsonDocument.Parse(e.WebMessageAsJson);
        var root = message.RootElement;
        if (!root.TryGetProperty("type", out var typeValue))
        {
            return;
        }

        switch (typeValue.GetString())
        {
            case "ready":
                globeReady = true;
                GlobeStatus.Text = "GLOBE · EUROPE · IMAGERIE SATELLITE";
                SendMapData();
                SendMapMessage(new { type = "resize" });
                break;
            case "error":
                GlobeStatus.Text = "FOND CARTOGRAPHIQUE INDISPONIBLE";
                break;
            case "render-error":
                GlobeStatus.Text = "ERREUR DU GLOBE · " + root.GetProperty("message").GetString();
                break;
            case "vehicle":
                var vehicleId = root.GetProperty("id").GetString();
                var vehicle = data?.Vehicles.FirstOrDefault(item => item.Id == vehicleId);
                if (vehicle is not null)
                {
                    VehicleSelected?.Invoke(this, vehicle);
                }
                break;
            case "site":
                var siteId = root.GetProperty("id").GetString();
                var site = data?.Sites.FirstOrDefault(item => item.Id == siteId);
                if (site is not null)
                {
                    SendMapMessage(new { type = "focus", longitude = site.Longitude, latitude = site.Latitude });
                }
                break;
        }
    }

    private void SendMapData()
    {
        if (!globeReady || data is null)
        {
            return;
        }

        var payload = new
        {
            type = "data",
            vehicles = data.Vehicles.Select(vehicle => new
            {
                id = vehicle.Id,
                identifier = vehicle.Identifier,
                driver = data.DriverName(vehicle.DriverId),
                type = VehicleTypes.Display(vehicle.Type, data.VehicleCategories),
                state = vehicle.State,
                brand = vehicle.Brand,
                model = vehicle.Model,
                plate = string.IsNullOrWhiteSpace(vehicle.Plate) ? vehicle.Registration : vehicle.Plate,
                mileage = vehicle.Mileage,
                latitude = vehicle.Latitude,
                longitude = vehicle.Longitude,
                location = vehicle.Location,
                energy = vehicle.Energy,
                nextInspection = vehicle.NextInspection,
                description = vehicle.Description,
                color = vehicle.StateColor,
                route = GetRouteProgress(vehicle.Id),
                operations = data.Operations.Where(operation => operation.VehicleId == vehicle.Id)
                    .OrderByDescending(operation => operation.Date)
                    .Select(operation => new
                    {
                        kind = operation.Kind,
                        number = operation.Number,
                        date = operation.Date.ToString("dd/MM/yyyy"),
                        time = operation.Time,
                        driver = data.DriverName(operation.DriverId),
                        location = operation.Location,
                        destination = operation.Destination,
                        nature = operation.Nature,
                        status = operation.Status,
                        details = operation.Details
                    }),
                tasks = data.Tasks.Where(task => task.VehicleId == vehicle.Id && task.Status is not "Terminée" and not "Annulée")
                    .OrderBy(task => task.DueAt)
                    .Select(task => new
                    {
                        title = task.Title,
                        dueAt = task.DueAt.ToString("dd/MM/yyyy HH:mm"),
                        status = task.Status,
                        priority = task.Priority,
                        driver = data.DriverName(task.DriverId),
                        description = task.Description
                    })
            }),
            sites = data.Sites.Select(site => new
            {
                id = site.Id,
                name = site.Name,
                kind = site.Kind,
                icon = site.Icon,
                latitude = site.Latitude,
                longitude = site.Longitude,
                details = site.Details
            }),
            headquarters = data.Sites
                .Where(site => site.Kind == SiteKinds.Headquarters)
                .Select(site => new { latitude = site.Latitude, longitude = site.Longitude })
                .FirstOrDefault()
        };
        SendMapMessage(payload);
    }

    private object? GetRouteProgress(string vehicleId)
    {
        if (data is null)
        {
            return null;
        }

        var now = DateTime.Now;
        var trips = data.Operations
            .Where(operation =>
                operation.VehicleId == vehicleId &&
                operation.Kind == OperationKinds.Trip &&
                !string.Equals(operation.Status, "Annulé", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(operation.Status, "Annulée", StringComparison.OrdinalIgnoreCase))
            .Select(operation =>
            {
                var hasTime = TimeSpan.TryParseExact(operation.Time, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var time);
                var start = operation.Date.Date.Add(hasTime ? time : TimeSpan.Zero);
                var duration = Math.Max(operation.DurationMinutes, 0);
                var isCompleted = string.Equals(operation.Status, "Terminé", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(operation.Status, "Terminée", StringComparison.OrdinalIgnoreCase);
                var isInProgress = string.Equals(operation.Status, "En cours", StringComparison.OrdinalIgnoreCase);
                return new
                {
                    Operation = operation,
                    HasTime = hasTime,
                    Start = start,
                    Duration = duration,
                    IsCompleted = isCompleted,
                    IsInProgress = isInProgress
                };
            })
            .ToList();

        var trip = trips
            .Where(candidate => candidate.IsInProgress || candidate.IsCompleted || candidate.Start.AddMinutes(candidate.Duration) >= now)
            .OrderByDescending(candidate => candidate.IsInProgress)
            .ThenBy(candidate => candidate.IsCompleted ? 1 : 0)
            .ThenBy(candidate => candidate.IsCompleted ? -candidate.Start.Ticks : candidate.Start.Ticks)
            .FirstOrDefault()
            ?? trips.OrderByDescending(candidate => candidate.Start).FirstOrDefault();

        if (trip is null)
        {
            return null;
        }

        return new
        {
            origin = string.IsNullOrWhiteSpace(trip.Operation.Location) ? "Non renseigné" : trip.Operation.Location,
            destination = string.IsNullOrWhiteSpace(trip.Operation.Destination) ? "Non renseignée" : trip.Operation.Destination,
            startAt = trip.HasTime ? trip.Start.ToString("yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) : null,
            durationMinutes = trip.Duration,
            status = trip.Operation.Status,
            number = trip.Operation.Number,
            departureLabel = trip.HasTime ? trip.Start.ToString("dd/MM/yyyy à HH:mm") : trip.Start.ToString("dd/MM/yyyy") + " · heure non renseignée",
            isCompleted = trip.IsCompleted
        };
    }

    private void SendMapMessage(object payload)
    {
        if (globeReady)
        {
            GlobeBrowser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload));
        }
    }

    private void AddSiteClick(object sender, RoutedEventArgs e)
    {
        if (data is null)
        {
            return;
        }

        var dialog = new SiteLocationWindow() { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.CreatedSite is SiteLocation site)
        {
            data.Sites.Add(site);
            PersistAndRefreshMap();
        }
    }

    private void EditSiteClick(object sender, RoutedEventArgs e)
    {
        if (data is null || (sender as FrameworkElement)?.Tag is not SiteLocation site)
        {
            return;
        }

        var dialog = new SiteLocationWindow(site) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.UpdatedSite is SiteLocation updatedSite)
        {
            var index = data.Sites.FindIndex(item => item.Id == updatedSite.Id);
            if (index >= 0)
            {
                data.Sites[index] = updatedSite;
                PersistAndRefreshMap();
            }
        }
    }

    private void DeleteSiteClick(object sender, RoutedEventArgs e)
    {
        if (data is null || (sender as FrameworkElement)?.Tag is not SiteLocation site)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            $"Supprimer le site « {site.Name} » ?",
            "Supprimer le site",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation == MessageBoxResult.Yes)
        {
            data.Sites.RemoveAll(item => item.Id == site.Id);
            PersistAndRefreshMap();
        }
    }

    private void PersistAndRefreshMap()
    {
        if (data is null)
        {
            return;
        }
        ApplicationDataStore.Save(data);
        ShowVehicles(data);
    }

    private void VehicleClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Vehicle vehicle)
        {
            VehicleSelected?.Invoke(this, vehicle);
        }
    }

    private void SiteClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SiteLocation site)
        {
            SendMapMessage(new { type = "focus", longitude = site.Longitude, latitude = site.Latitude });
        }
    }

    private sealed record VehicleRow(Vehicle Vehicle, string City, string Driver, string VehicleDescription, string OperationalInfo);
    private sealed record SiteRow(SiteLocation Site, string Title, string Coordinates, string Details);
}
