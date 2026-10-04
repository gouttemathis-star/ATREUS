using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace ATREUS;

public partial class TaskEditWindow : Window
{
    private readonly ApplicationData data;
    private readonly TaskItem? existing;

    public TaskEditWindow(TaskItem? task, ApplicationData data, DateTime? initialDate = null)
    {
        this.data = data;
        existing = task;
        InitializeComponent();
        TitleLabel.Text = task is null ? "Nouvelle tâche" : "Modifier la tâche";
        VehicleInput.ItemsSource = data.Vehicles;
        DriverInput.ItemsSource = data.Drivers;
        OperationInput.ItemsSource = data.Operations
            .Select(operation => new OperationOption($"{operation.Kind} · {operation.Number} · {operation.Nature}", operation))
            .ToList();

        TitleInput.Text = task?.Title ?? string.Empty;
        DateInput.SelectedDate = task?.DueAt.Date ?? initialDate?.Date ?? DateTime.Today;
        TimeInput.Text = task?.HasTime == true ? task.DueAt.ToString("HH:mm") : string.Empty;
        EndTimeInput.Text = task?.EndAt?.ToString("HH:mm") ?? string.Empty;
        SelectComboValue(PriorityInput, task?.Priority ?? "Normale");
        SelectComboValue(StatusInput, task?.Status ?? "À faire");
        DescriptionInput.Text = task?.Description ?? string.Empty;
        VehicleInput.SelectedValue = task?.VehicleId;
        DriverInput.SelectedValue = task?.DriverId ?? data.FindVehicle(task?.VehicleId)?.DriverId;
        OperationInput.SelectedValue = task?.OperationId;
        UpdateOperationLink();
    }

    private void VehicleSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DriverInput.SelectedValue is null && VehicleInput.SelectedItem is Vehicle vehicle)
        {
            DriverInput.SelectedValue = vehicle.DriverId;
        }
    }

    private void OpenOperationClick(object sender, RoutedEventArgs e)
    {
        if (data.FindOperation(OperationInput.SelectedValue as string) is OperationRecord operation)
        {
            new OperationEditWindow(operation, operation.Kind, data) { Owner = this }.ShowDialog();
        }
    }

    private void OperationSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateOperationLink();

    private void UpdateOperationLink() => OperationLinkButton.IsEnabled = data.FindOperation(OperationInput.SelectedValue as string) is not null;

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var title = TitleInput.Text.Trim();
        if (title.Length == 0)
        {
            MessageBox.Show("Renseignez l’intitulé de la tâche.", "Informations manquantes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (DateInput.SelectedDate is not DateTime date)
        {
            MessageBox.Show("Choisissez une date d’échéance valide.", "Date requise", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var timeText = TimeInput.Text.Trim();
        var hasTime = timeText.Length > 0;
        var dueAt = date.Date;
        if (hasTime)
        {
            if (!DateTime.TryParseExact(timeText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                MessageBox.Show("L’heure doit respecter le format HH:mm.", "Heure incorrecte", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            dueAt = dueAt.Add(time.TimeOfDay);
        }
        var endTimeText = EndTimeInput.Text.Trim();
        DateTime? endAt = null;
        if (endTimeText.Length > 0)
        {
            if (!hasTime || !DateTime.TryParseExact(endTimeText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime))
            {
                MessageBox.Show("Renseignez une heure de début valide avant l’heure de fin. Le format attendu est HH:mm.", "Heure de fin incorrecte", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            endAt = date.Date.Add(endTime.TimeOfDay);
            if (endAt <= dueAt)
            {
                MessageBox.Show("L’heure de fin doit être postérieure à l’heure de début.", "Horaire incorrect", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var task = existing ?? new TaskItem();
        task.Title = title;
        task.DueAt = dueAt;
        task.EndAt = endAt;
        task.HasTime = hasTime;
        task.Priority = SelectedText(PriorityInput) ?? "Normale";
        task.Status = SelectedText(StatusInput) ?? "À faire";
        task.Description = DescriptionInput.Text.Trim();
        task.VehicleId = VehicleInput.SelectedValue as string;
        task.DriverId = DriverInput.SelectedValue as string;
        task.OperationId = OperationInput.SelectedValue as string;
        if (task.OperationId is not null && data.FindOperation(task.OperationId) is OperationRecord operation)
        {
            task.VehicleId = operation.VehicleId;
            task.DriverId = operation.DriverId ?? task.DriverId;
        }
        if (existing is null)
        {
            data.Tasks.Add(task);
        }
        ApplicationDataStore.Save(data);
        DialogResult = true;
    }

    private static void SelectComboValue(ComboBox combo, string value)
    {
        combo.SelectedItem = combo.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Content?.ToString() == value);
    }

    private static string? SelectedText(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString();

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed record OperationOption(string Display, OperationRecord Operation);
}
