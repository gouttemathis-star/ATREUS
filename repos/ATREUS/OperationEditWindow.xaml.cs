using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class OperationEditWindow : Window
{
    private readonly ApplicationData data;
    private readonly OperationRecord? existing;
    private readonly string kind;

    public OperationEditWindow(OperationRecord? operation, string kind, ApplicationData data, DateTime? initialDate = null)
    {
        this.data = data;
        existing = operation;
        this.kind = kind;
        InitializeComponent();
        SectionLabel.Text = $"ATREUS · {kind.ToUpperInvariant()}";
        TitleLabel.Text = operation is null ? $"Nouvel élément · {kind}" : $"{kind} · {operation.Number}";
        VehicleInput.ItemsSource = data.Vehicles;
        DriverInput.ItemsSource = data.Drivers;
        StatusInput.ItemsSource = kind switch
        {
            OperationKinds.Maintenance => new[] { "Planifiée", "En cours", "Terminée", "Annulée" },
            OperationKinds.Trip => new[] { "Planifié", "En cours", "Terminé", "Annulé" },
            _ => new[] { "À traiter", "En cours", "Résolu", "Annulé" }
        };
        DateInput.SelectedDate = operation?.Date ?? initialDate?.Date ?? DateTime.Today;
        TimeInput.Text = operation?.Time ?? string.Empty;
        LocationInput.Text = operation?.Location ?? string.Empty;
        DestinationInput.Text = operation?.Destination ?? string.Empty;
        DurationInput.Text = FormatDuration(operation?.DurationMinutes ?? 60);
        NatureInput.Text = operation?.Nature ?? string.Empty;
        DetailsInput.Text = operation?.Details ?? string.Empty;
        StatusInput.SelectedItem = operation?.Status ?? StatusInput.Items[0];
        VehicleInput.SelectedValue = operation?.VehicleId ?? data.Vehicles.FirstOrDefault()?.Id;
        DriverInput.SelectedValue = operation?.DriverId ?? data.FindVehicle(operation?.VehicleId ?? VehicleInput.SelectedValue as string)?.DriverId;
        if (VehicleInput.SelectedItem is Vehicle selectedVehicle)
        {
            TripInput.ItemsSource = data.Operations.Where(item => item.Kind == OperationKinds.Trip && item.VehicleId == selectedVehicle.Id).ToList();
        }
        TripInput.SelectedValue = operation?.TripId;
        TripInput.IsEnabled = kind == OperationKinds.Incident;
        RelatedTripPanel.Visibility = kind == OperationKinds.Incident ? Visibility.Visible : Visibility.Collapsed;
        TripFieldsPanel.Visibility = kind == OperationKinds.Trip ? Visibility.Visible : Visibility.Collapsed;
        DeleteTripButton.Visibility = kind == OperationKinds.Trip && operation is not null ? Visibility.Visible : Visibility.Collapsed;
        TimeLabel.Text = kind == OperationKinds.Trip ? "Heure de départ (HH:mm)" : "Heure (facultative, HH:mm)";
        LocationLabel.Text = kind == OperationKinds.Trip ? "Lieu de départ" : "Lieu";
        NatureLabel.Text = kind == OperationKinds.Trip ? "Intitulé du trajet" : "Nature / objet";
        VehicleInputLinkVisibility();
        RefreshRelatedTasks();
    }

    private void VehicleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VehicleInput.SelectedItem is not Vehicle vehicle)
        {
            return;
        }
        if (DriverInput.SelectedValue is null && vehicle.DriverId is not null)
        {
            DriverInput.SelectedValue = vehicle.DriverId;
        }
        TripInput.ItemsSource = data.Operations.Where(item => item.Kind == OperationKinds.Trip && item.VehicleId == vehicle.Id).ToList();
        VehicleInputLinkVisibility();
    }

    private void VehicleInputLinkVisibility()
    {
        VehicleLinkButton.IsEnabled = VehicleInput.SelectedItem is Vehicle;
        DriverLinkButton.IsEnabled = data.FindDriver(DriverInput.SelectedValue as string) is not null;
    }

    private void DriverSelectionChanged(object sender, SelectionChangedEventArgs e) => VehicleInputLinkVisibility();

    private void OpenVehicleClick(object sender, RoutedEventArgs e)
    {
        if (VehicleInput.SelectedItem is Vehicle vehicle)
        {
            var detail = new VehicleDetailWindow(vehicle, data, allowDelete: false) { Owner = this };
            if (detail.ShowDialog() == true)
            {
                ApplicationDataStore.Save(data);
            }
        }
    }

    private void OpenDriverClick(object sender, RoutedEventArgs e)
    {
        if (data.FindDriver(DriverInput.SelectedValue as string) is Driver driver)
        {
            new DriverDetailWindow(driver, data) { Owner = this }.ShowDialog();
        }
    }

    private void RefreshRelatedTasks()
    {
        List<RelatedTaskRow> tasks = existing is null
            ? []
            : data.Tasks.Where(task => task.OperationId == existing.Id)
                .OrderBy(task => task.DueAt)
                .Select(task => new RelatedTaskRow($"{task.DueAt:dd/MM/yyyy} · {task.Title} · {task.Status}", task))
                .ToList();
        RelatedTasksList.ItemsSource = tasks;
        NoRelatedTasksLabel.Visibility = tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RelatedTaskClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TaskItem task)
        {
            new TaskEditWindow(task, data) { Owner = this }.ShowDialog();
            RefreshRelatedTasks();
        }
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (VehicleInput.SelectedItem is not Vehicle vehicle)
        {
            MessageBox.Show("Associez l’opération à un véhicule.", "Véhicule requis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (DateInput.SelectedDate is not DateTime date)
        {
            MessageBox.Show("Choisissez une date valide.", "Date requise", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var time = TimeInput.Text.Trim();
        if (time.Length > 0 && !DateTime.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            MessageBox.Show("L’heure doit respecter le format HH:mm.", "Heure incorrecte", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var durationMinutes = 0;
        if (kind == OperationKinds.Trip)
        {
            if (time.Length == 0)
            {
                MessageBox.Show("Renseignez l’heure de départ du trajet.", "Heure de départ requise", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!TryParseDuration(DurationInput.Text, out durationMinutes))
            {
                MessageBox.Show("Saisissez une durée valide au format HH:mm, supérieure à 00:00 et inférieure ou égale à 168:00.", "Durée incorrecte", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        var nature = NatureInput.Text.Trim();
        if (nature.Length == 0)
        {
            MessageBox.Show("Renseignez la nature de l’opération.", "Informations manquantes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var operation = existing ?? new OperationRecord
        {
            Number = CreateNumber(),
            Kind = kind
        };
        operation.VehicleId = vehicle.Id;
        operation.DriverId = DriverInput.SelectedValue as string;
        operation.TripId = kind == OperationKinds.Incident ? TripInput.SelectedValue as string : null;
        operation.Date = date.Date;
        operation.Time = time;
        operation.DurationMinutes = kind == OperationKinds.Trip ? durationMinutes : operation.DurationMinutes;
        operation.Location = LocationInput.Text.Trim();
        operation.Destination = kind == OperationKinds.Trip ? DestinationInput.Text.Trim() : string.Empty;
        operation.Nature = nature;
        operation.Status = StatusInput.SelectedItem?.ToString() ?? "À traiter";
        operation.Details = DetailsInput.Text.Trim();
        if (existing is null)
        {
            data.Operations.Add(operation);
        }
        ApplicationDataStore.Save(data);
        DialogResult = true;
    }

    private string CreateNumber()
    {
        var prefix = kind switch
        {
            OperationKinds.Maintenance => "MAI",
            OperationKinds.Trip => "TRA",
            _ => "INC"
        };
        var next = data.Operations.Count(operation => operation.Kind == kind) + 1;
        string number;
        do
        {
            number = $"{prefix}-{next++:000}";
        } while (data.Operations.Any(operation => operation.Number == number));
        return number;
    }

    private static string FormatDuration(int durationMinutes) =>
        $"{durationMinutes / 60:00}:{durationMinutes % 60:00}";

    private static bool TryParseDuration(string text, out int durationMinutes)
    {
        durationMinutes = 0;
        var parts = text.Trim().Split(':');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            hours is < 0 or > 168 || minutes is < 0 or > 59)
        {
            return false;
        }

        durationMinutes = hours * 60 + minutes;
        return durationMinutes is > 0 and <= 10080;
    }

    private void CloseClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void DeleteTripClick(object sender, RoutedEventArgs e)
    {
        if (existing is null || kind != OperationKinds.Trip)
        {
            return;
        }
        if (MessageBox.Show(
                $"Supprimer le trajet {existing.Number} ?",
                "Confirmer la suppression",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        data.Operations.Remove(existing);
        foreach (var incident in data.Operations.Where(item => item.TripId == existing.Id))
        {
            incident.TripId = null;
        }
        foreach (var task in data.Tasks.Where(item => item.OperationId == existing.Id))
        {
            task.OperationId = null;
        }
        ApplicationDataStore.Save(data);
        DialogResult = true;
    }

    private sealed record RelatedTaskRow(string Display, TaskItem Task);
}
