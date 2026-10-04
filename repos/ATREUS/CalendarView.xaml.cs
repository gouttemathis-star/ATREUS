using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ATREUS;

public partial class CalendarView : UserControl
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly string[] WeekdayNames = ["Lun", "Mar", "Mer", "Jeu", "Ven", "Sam", "Dim"];
    private ApplicationData? data;
    private Driver? planningDriver;
    private bool canManageTasks = true;
    private bool canManageTrips = true;
    private bool canViewTrips = true;
    private DateTime currentDate = DateTime.Today;
    private CalendarPeriod period = CalendarPeriod.Month;
    private Point? taskDragStart;

    public event Action<Vehicle>? VehicleRequested;
    public event Action<Driver>? DriverRequested;

    public CalendarView()
    {
        InitializeComponent();
        Refresh();
    }

    public void ShowData(
        ApplicationData source,
        bool allowTaskChanges = true,
        bool allowTripChanges = true,
        bool allowTripViewing = true)
    {
        data = source;
        planningDriver = null;
        canManageTasks = allowTaskChanges;
        canManageTrips = allowTripChanges;
        canViewTrips = allowTripViewing || allowTripChanges;
        CalendarTitle.Text = "Calendrier";
        CalendarSubtitle.Text = "Vos tâches et trajets, organisés dans le temps.";
        AddTripButton.Visibility = allowTripChanges ? Visibility.Visible : Visibility.Collapsed;
        AddTaskButton.Visibility = allowTaskChanges ? Visibility.Visible : Visibility.Collapsed;
        Refresh();
    }

    public void ShowDriverPlan(ApplicationData source, Driver driver)
    {
        data = source;
        planningDriver = driver;
        canManageTasks = false;
        canManageTrips = false;
        canViewTrips = true;
        CalendarTitle.Text = "Mon planning";
        CalendarSubtitle.Text = $"Planning personnel de {driver.Name} · tâches et trajets qui vous sont attribués.";
        AddTripButton.Visibility = Visibility.Collapsed;
        AddTaskButton.Visibility = Visibility.Collapsed;
        Refresh();
    }

    public void Refresh()
    {
        if (CalendarGrid is null || data is null)
        {
            return;
        }

        RangeLabel.Text = GetRangeLabel();
        UpdateViewButtons();
        var entries = GetCalendarEntries();
        var incomplete = entries.Where(IsOpen).ToList();
        OpenCount.Text = incomplete.Count.ToString();
        OverdueCount.Text = incomplete.Count(IsOverdue).ToString();
        BuildCalendar();
    }

    private string GetRangeLabel()
    {
        var day = currentDate.ToString("d MMMM yyyy", FrenchCulture);
        return period switch
        {
            CalendarPeriod.Day => char.ToUpper(day[0], FrenchCulture) + day[1..],
            CalendarPeriod.Week => GetWeekStart(currentDate).ToString("d MMMM", FrenchCulture) +
                                  " – " + GetWeekStart(currentDate).AddDays(6).ToString("d MMMM yyyy", FrenchCulture),
            _ => char.ToUpper(currentDate.ToString("MMMM yyyy", FrenchCulture)[0], FrenchCulture) +
                 currentDate.ToString("MMMM yyyy", FrenchCulture)[1..]
        };
    }

    private void BuildCalendar()
    {
        CalendarGrid.Children.Clear();
        CalendarGrid.RowDefinitions.Clear();
        CalendarGrid.ColumnDefinitions.Clear();
        WeekdayHeader.Children.Clear();
        WeekdayHeader.ColumnDefinitions.Clear();

        var columnCount = period == CalendarPeriod.Day ? 1 : 7;
        for (var column = 0; column < columnCount; column++)
        {
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 88 });
            if (period != CalendarPeriod.Day)
            {
                WeekdayHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 88 });
                var weekdayHeader = new TextBlock
                {
                    Text = WeekdayNames[column],
                    Foreground = new SolidColorBrush(Color.FromRgb(145, 165, 173)),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(12, 3, 0, 6)
                };
                WeekdayHeader.Children.Add(weekdayHeader);
                Grid.SetColumn(weekdayHeader, column);
            }
        }

        var dates = GetVisibleDates();
        var rowCount = period == CalendarPeriod.Month ? 6 : period == CalendarPeriod.Week ? 1 : 1;
        for (var row = 0; row < rowCount; row++)
        {
            CalendarGrid.RowDefinitions.Add(new RowDefinition
            {
                Height = period == CalendarPeriod.Month
                    ? new GridLength(112)
                    : new GridLength(period == CalendarPeriod.Week ? 510 : Math.Max(360, GetCalendarEntriesForDate(currentDate).Count * 88 + 78))
            });
        }
        CalendarGrid.Height = rowCount * CalendarGrid.RowDefinitions[0].Height.Value;

        for (var index = 0; index < dates.Count; index++)
        {
            var date = dates[index];
            var row = period == CalendarPeriod.Month ? index / 7 : 0;
            var column = period == CalendarPeriod.Day ? 0 : index % 7;
            var cell = CreateDayCell(date);
            CalendarGrid.Children.Add(cell);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
        }
    }

    private Border CreateDayCell(DateTime date)
    {
        var inCurrentMonth = date.Month == currentDate.Month && date.Year == currentDate.Year;
        var isToday = date.Date == DateTime.Today;
        var isDayView = period == CalendarPeriod.Day;
        var background = isToday ? "#132D38" : inCurrentMonth || period != CalendarPeriod.Month ? "#0A1C26" : "#081720";
        var outer = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(background)),
            BorderBrush = isToday
                ? new SolidColorBrush(Color.FromRgb(214, 178, 94))
                : new SolidColorBrush(Color.FromRgb(32, 56, 70)),
            BorderThickness = new Thickness(0.7),
            Padding = new Thickness(isDayView ? 17 : 8),
            Tag = date,
            Cursor = Cursors.Hand,
            AllowDrop = planningDriver is null && (canManageTasks || canManageTrips)
        };
        outer.MouseLeftButtonDown += DayCellClick;
        if (planningDriver is null && (canManageTasks || canManageTrips))
        {
            outer.DragOver += DayCellDragOver;
            outer.Drop += DayCellDrop;
        }

        var layout = new DockPanel();
        var dateHeader = new Grid { Margin = new Thickness(0, 0, 0, isDayView ? 12 : 5) };
        var dateLabel = new TextBlock
        {
            Text = isDayView
                ? date.ToString("dddd d MMMM yyyy", FrenchCulture)
                : date.Day.ToString(),
            Foreground = inCurrentMonth || period != CalendarPeriod.Month
                ? new SolidColorBrush(isToday ? Color.FromRgb(214, 178, 94) : Color.FromRgb(232, 238, 240))
                : new SolidColorBrush(Color.FromRgb(87, 108, 118)),
            FontSize = isDayView ? 21 : 13,
            FontWeight = isToday || isDayView ? FontWeights.Bold : FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center
        };
        dateHeader.Children.Add(dateLabel);
        var addButton = new Button
        {
            Content = "+",
            Tag = date,
            Width = 25,
            Height = 24,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(214, 178, 94)),
            BorderBrush = Brushes.Transparent,
            FontSize = 16,
            ToolTip = "Ajouter une tâche à cette date"
        };
        addButton.Visibility = planningDriver is null && canManageTasks ? Visibility.Visible : Visibility.Collapsed;
        if (planningDriver is null && canManageTasks)
        {
            addButton.Click += AddForDateClick;
        }
        dateHeader.Children.Add(addButton);
        DockPanel.SetDock(dateHeader, Dock.Top);
        layout.Children.Add(dateHeader);

        var tasksPanel = new StackPanel();
        var entries = GetCalendarEntriesForDate(date);
        if (isDayView)
        {
            foreach (var entry in entries)
            {
                tasksPanel.Children.Add(CreateCalendarCard(entry, detailed: true));
            }
            if (entries.Count == 0)
            {
                tasksPanel.Children.Add(new TextBlock
                {
                    Text = planningDriver is null
                        ? "Aucune tâche ni aucun trajet prévu. Cliquez sur + pour planifier votre journée."
                        : "Aucune tâche ni aucune opération prévue dans votre planning.",
                    Foreground = new SolidColorBrush(Color.FromRgb(145, 165, 173)),
                    FontSize = 13,
                    Margin = new Thickness(2, 6, 0, 0)
                });
            }
        }
        else
        {
            var visibleLimit = period == CalendarPeriod.Month ? 3 : 7;
            foreach (var entry in entries.Take(visibleLimit))
            {
                tasksPanel.Children.Add(CreateCalendarCard(entry, detailed: period == CalendarPeriod.Week));
            }
            if (entries.Count > visibleLimit)
            {
                var moreButton = new Button
                {
                    Content = $"+{entries.Count - visibleLimit} événement(s)",
                    Tag = date,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(3, 2, 3, 2),
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(214, 178, 94)),
                    BorderThickness = new Thickness(0),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                moreButton.Click += ShowDayClick;
                tasksPanel.Children.Add(moreButton);
            }
        }

        layout.Children.Add(tasksPanel);
        outer.Child = layout;
        return outer;
    }

    private Border CreateCalendarCard(CalendarEntry entry, bool detailed)
    {
        var task = entry.Task;
        var trip = entry.Trip;
        var isCompleted = entry.IsCompleted;
        var accent = GetCalendarColor(entry);
        var content = new StackPanel();
        var headlineRow = new Grid();
        var headline = new TextBlock
        {
            Text = $"{GetTimeLabel(entry)} · {entry.Title}",
            Foreground = isCompleted
                ? new SolidColorBrush(Color.FromRgb(170, 190, 183))
                : new SolidColorBrush(Color.FromRgb(239, 244, 245)),
            FontSize = detailed ? 12 : 10,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        headlineRow.Children.Add(headline);
        if (detailed && CanManageEntry(entry) && IsOpen(entry))
        {
            var completeButton = new Button
            {
                Content = "✓",
                Tag = entry,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(5, 0, 0, 0),
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(124, 203, 155)),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                ToolTip = "Marquer comme terminé"
            };
            completeButton.Click += CompleteClick;
            headlineRow.Children.Add(completeButton);
        }
        content.Children.Add(headlineRow);
        if (detailed)
        {
            var detailText = trip is null
                ? new List<string> { task!.Status, $"Priorité {task.Priority}" }
                : trip.Kind == OperationKinds.Trip
                    ? new List<string> { $"Trajet · {trip.Status}", $"Durée {FormatDuration(trip.DurationMinutes)}" }
                    : new List<string> { $"{trip.Kind} · {trip.Status}", trip.Location };
            var description = trip is null ? task!.Description : trip.Details;
            if (!string.IsNullOrWhiteSpace(description))
            {
                detailText.Add(description);
            }
            if (trip?.Kind == OperationKinds.Trip)
            {
                var route = FormatRoute(trip);
                if (!string.IsNullOrWhiteSpace(route))
                {
                    detailText.Add(route);
                }
            }
            content.Children.Add(new TextBlock
            {
                Text = string.Join(" · ", detailText),
                Foreground = new SolidColorBrush(Color.FromRgb(178, 196, 201)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            });
            if (CanManageEntry(entry))
            {
                var links = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
                var vehicleId = trip?.VehicleId ?? task?.VehicleId;
                var driverId = trip?.DriverId ?? task?.DriverId;
                if (data?.FindVehicle(vehicleId) is Vehicle vehicle)
                {
                    links.Children.Add(CreateLinkButton(vehicle.Identifier, vehicle, VehicleLinkClick, "#D6B25E"));
                }
                if (trip is not null)
                {
                    links.Children.Add(CreateLinkButton(trip.Number, trip, OperationLinkClick, "#83BDD3"));
                }
                else if (data?.FindOperation(task!.OperationId) is OperationRecord operation)
                {
                    links.Children.Add(CreateLinkButton($"{operation.Kind} {operation.Number}", operation, OperationLinkClick, "#83BDD3"));
                }
                if (data?.FindDriver(driverId) is Driver driver)
                {
                    links.Children.Add(CreateLinkButton(driver.Name, driver, DriverLinkClick, "#A9BBC0"));
                }
                if (links.Children.Count > 0)
                {
                    content.Children.Add(links);
                }
            }
        }
        else if (period == CalendarPeriod.Month)
        {
            content.Children.Add(new TextBlock
            {
                Text = trip is null ? task!.Status : $"Trajet · {trip.Status}",
                Foreground = new SolidColorBrush(Color.FromRgb(178, 196, 201)),
                FontSize = 9,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        var card = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, detailed ? 5 : 3, 5, detailed ? 5 : 3),
            Margin = new Thickness(0, 2, 0, 2),
            Child = content,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(GetCalendarBorderColor(entry))),
            BorderThickness = isCompleted ? new Thickness(0, 0, 0, 0) : new Thickness(2, 0, 0, 0),
            Opacity = isCompleted || entry.IsCancelled ? 0.72 : 1,
            Tag = entry,
            Cursor = Cursors.Hand
        };
        card.MouseLeftButtonDown += CalendarCardClick;
        if (planningDriver is null)
        {
            card.PreviewMouseLeftButtonDown += (_, e) => taskDragStart = e.GetPosition(this);
            card.PreviewMouseMove += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed ||
                    taskDragStart is not Point dragStart)
                {
                    return;
                }
                var position = e.GetPosition(this);
                if (Math.Abs(position.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(position.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                {
                    return;
                }
                taskDragStart = null;
                DragDrop.DoDragDrop(card, entry.DragId, DragDropEffects.Move);
            };
        }
        var canManageEntry = CanManageEntry(entry);
        card.Cursor = canManageEntry ? Cursors.Hand : Cursors.Arrow;
        card.ToolTip = canManageEntry
            ? $"{GetTimeLabel(entry)} · {entry.Title} · {entry.Status} · Cliquez pour modifier, glissez vers un autre jour pour déplacer"
            : $"{GetTimeLabel(entry)} · {entry.Title} · {entry.Status} · Cliquez pour consulter les détails";
        return card;
    }

    private static Button CreateLinkButton(string label, object target, RoutedEventHandler click, string color)
    {
        var button = new Button
        {
            Content = label,
            Tag = target,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 1, 10, 1),
            FontSize = 10,
            Cursor = Cursors.Hand
        };
        button.Click += click;
        return button;
    }

    private List<DateTime> GetVisibleDates()
    {
        if (period == CalendarPeriod.Day)
        {
            return [currentDate.Date];
        }
        if (period == CalendarPeriod.Week)
        {
            var start = GetWeekStart(currentDate);
            return Enumerable.Range(0, 7).Select(offset => start.AddDays(offset)).ToList();
        }

        var firstOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);
        var gridStart = GetWeekStart(firstOfMonth);
        return Enumerable.Range(0, 42).Select(offset => gridStart.AddDays(offset)).ToList();
    }

    private List<CalendarEntry> GetCalendarEntriesForDate(DateTime date) =>
        GetCalendarEntries().Where(entry => entry.DateTime.Date == date.Date)
            .OrderBy(entry => entry.HasTime ? entry.DateTime.TimeOfDay : TimeSpan.MinValue)
            .ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private List<CalendarEntry> GetCalendarEntries()
    {
        if (data is null)
        {
            return [];
        }

        var filter = (FilterInput?.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var entries = data.Tasks.Select(CalendarEntry.ForTask)
            .Concat(data.Operations
                .Where(operation => (canViewTrips && operation.Kind == OperationKinds.Trip) ||
                    (planningDriver is not null && operation.Kind is OperationKinds.Incident or OperationKinds.Maintenance))
                .Select(CalendarEntry.ForTrip));
        if (filter == "En retard")
        {
            entries = entries.Where(IsOverdue);
        }
        else if (filter == "À venir")
        {
            entries = entries.Where(entry => IsOpen(entry) && !IsOverdue(entry));
        }
        else if (filter == "Terminées")
        {
            entries = entries.Where(entry => entry.IsCompleted);
        }

        if (planningDriver is not null)
        {
            var driverId = planningDriver.Id;
            entries = entries.Where(entry =>
                entry.Task is TaskItem task &&
                    (task.DriverId == driverId ||
                     (task.DriverId is null && data.FindVehicle(task.VehicleId)?.DriverId == driverId)) ||
                entry.Trip is OperationRecord trip &&
                    (trip.DriverId == driverId || data.FindVehicle(trip.VehicleId)?.DriverId == driverId));
        }

        return entries.ToList();
    }

    private static DateTime GetWeekStart(DateTime date) =>
        date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static bool IsOpen(CalendarEntry entry) => !entry.IsCompleted && !entry.IsCancelled;

    private bool CanManageEntry(CalendarEntry entry) =>
        planningDriver is null && (entry.Task is not null ? canManageTasks : canManageTrips);

    private static bool IsOverdue(CalendarEntry entry)
    {
        if (!IsOpen(entry))
        {
            return false;
        }
        return entry.DateTime.Date < DateTime.Today ||
               (entry.HasTime && entry.DateTime < DateTime.Now);
    }

    private static string GetTimeLabel(CalendarEntry entry)
    {
        if (entry.Trip is OperationRecord trip)
        {
            if (!TimeSpan.TryParseExact(trip.Time, "hh\\:mm", CultureInfo.InvariantCulture, out var tripStart))
            {
                return trip.Kind == OperationKinds.Trip
                    ? $"Toute journée · durée {FormatDuration(trip.DurationMinutes)}"
                    : "Toute journée";
            }
            if (trip.Kind != OperationKinds.Trip)
            {
                return trip.Time;
            }
            var tripEnd = tripStart.Add(TimeSpan.FromMinutes(Math.Max(0, trip.DurationMinutes)));
            var dayOffset = (int)tripEnd.TotalDays;
            var endLabel = tripEnd.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            return $"{tripStart:hh\\:mm}–{endLabel}" + (dayOffset > 0 ? $" (+{dayOffset} j)" : string.Empty);
        }

        var task = entry.Task!;
        if (!task.HasTime)
        {
            return "Toute journée";
        }
        var start = task.DueAt.ToString("HH:mm");
        return task.EndAt is DateTime end ? $"{start}–{end:HH:mm}" : start;
    }

    private static string FormatDuration(int durationMinutes)
    {
        var hours = Math.Max(0, durationMinutes) / 60;
        var minutes = Math.Max(0, durationMinutes) % 60;
        return hours > 0 && minutes > 0
            ? $"{hours} h {minutes:D2}"
            : hours > 0 ? $"{hours} h" : $"{minutes} min";
    }

    private static string FormatRoute(OperationRecord trip)
    {
        var start = trip.Location.Trim();
        var destination = trip.Destination.Trim();
        return (start, destination) switch
        {
            (not "", not "") => $"{start} → {destination}",
            (not "", "") => start,
            ("", not "") => destination,
            _ => string.Empty
        };
    }

    private static string GetCalendarColor(CalendarEntry entry)
    {
        if (IsOverdue(entry))
        {
            return "#4A3032";
        }
        if (entry.Trip is not null)
        {
            return entry.Status switch
            {
                "Terminé" => "#254638",
                "Annulé" => "#3B3438",
                "En cours" => "#224354",
                _ => "#204453"
            };
        }
        var task = entry.Task!;
        return task.Status switch
        {
            "Terminée" => "#254638",
            "Annulée" => "#3B3438",
            "En cours" => "#224354",
            _ when task.Priority == "Haute" => "#4A3032",
            _ when task.Priority == "Basse" => "#244239",
            _ => "#273B50"
        };
    }

    private static string GetCalendarBorderColor(CalendarEntry entry)
    {
        if (IsOverdue(entry))
        {
            return "#EF9388";
        }
        if (entry.Trip is not null)
        {
            return entry.Status switch
            {
                "Terminé" => "#78B492",
                "Annulé" => "#9B888B",
                "En cours" => "#78B9D3",
                _ => "#83BDD3"
            };
        }
        var task = entry.Task!;
        return task.Status switch
        {
            "Terminée" => "#78B492",
            "Annulée" => "#9B888B",
            "En cours" => "#78B9D3",
            _ when task.Priority == "Haute" => "#EF9388",
            _ when task.Priority == "Basse" => "#7CCB9B",
            _ => "#D6B25E"
        };
    }

    private void AddClick(object sender, RoutedEventArgs e) => OpenTaskEditor(null, currentDate);

    private void AddTripClick(object sender, RoutedEventArgs e) => OpenTripEditor(null, currentDate);

    private void AddForDateClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DateTime date)
        {
            OpenTaskEditor(null, date);
            e.Handled = true;
        }
    }

    private void DayCellClick(object sender, MouseButtonEventArgs e)
    {
        if (period == CalendarPeriod.Day || (sender as FrameworkElement)?.Tag is not DateTime date)
        {
            return;
        }
        currentDate = date.Date;
        period = CalendarPeriod.Day;
        Refresh();
        e.Handled = true;
    }

    private void ShowDayClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is DateTime date)
        {
            currentDate = date.Date;
            period = CalendarPeriod.Day;
            Refresh();
        }
        e.Handled = true;
    }

    private void CalendarCardClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CalendarEntry entry)
        {
            if (!CanManageEntry(entry))
            {
                var description = entry.Task?.Description ?? entry.Trip?.Details;
                var vehicle = data?.FindVehicle(entry.Task?.VehicleId ?? entry.Trip?.VehicleId);
                MessageBox.Show(
                    $"{entry.Title}\n{entry.DateTime:dddd d MMMM yyyy}{(entry.HasTime ? $" · {GetTimeLabel(entry)}" : string.Empty)}\nStatut : {entry.Status}" +
                    (vehicle is null ? string.Empty : $"\nVéhicule : {vehicle.Identifier}") +
                    (string.IsNullOrWhiteSpace(description) ? string.Empty : $"\n\n{description}"),
                    "Détail de mon planning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                e.Handled = true;
                return;
            }

            if (entry.Task is TaskItem task)
            {
                OpenTaskEditor(task, task.DueAt);
            }
            else if (entry.Trip is OperationRecord trip)
            {
                OpenTripEditor(trip, trip.Date);
            }
        }
        e.Handled = true;
    }

    private void CompleteClick(object sender, RoutedEventArgs e)
    {
        if (data is not null &&
            (sender as FrameworkElement)?.Tag is CalendarEntry entry &&
            CanManageEntry(entry))
        {
            if (entry.Task is TaskItem task)
            {
                task.Status = "Terminée";
            }
            else if (entry.Trip is OperationRecord trip)
            {
                trip.Status = "Terminé";
            }
            ApplicationDataStore.Save(data);
            Refresh();
        }
        e.Handled = true;
    }

    private void VehicleLinkClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Vehicle vehicle)
        {
            VehicleRequested?.Invoke(vehicle);
        }
        e.Handled = true;
    }

    private void DriverLinkClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Driver driver)
        {
            DriverRequested?.Invoke(driver);
        }
        e.Handled = true;
    }

    private void OperationLinkClick(object sender, RoutedEventArgs e)
    {
        if (canManageTrips && data is not null && (sender as FrameworkElement)?.Tag is OperationRecord operation)
        {
            new OperationEditWindow(operation, operation.Kind, data) { Owner = Window.GetWindow(this) }.ShowDialog();
            Refresh();
        }
        e.Handled = true;
    }

    private void OpenTaskEditor(TaskItem? task, DateTime? preferredDate = null)
    {
        if (data is null || !canManageTasks)
        {
            return;
        }
        var dialog = new TaskEditWindow(task, data, preferredDate) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            ApplicationDataStore.Save(data);
        }
        Refresh();
    }

    private void OpenTripEditor(OperationRecord? trip, DateTime? preferredDate = null)
    {
        if (data is null || !canManageTrips)
        {
            return;
        }
        var dialog = new OperationEditWindow(trip, OperationKinds.Trip, data, preferredDate)
        {
            Owner = Window.GetWindow(this)
        };
        if (dialog.ShowDialog() == true)
        {
            ApplicationDataStore.Save(data);
        }
        Refresh();
    }

    private void DayCellDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(string)) && (sender as FrameworkElement)?.Tag is DateTime
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DayCellDrop(object sender, DragEventArgs e)
    {
        if (data is null || (!canManageTasks && !canManageTrips) ||
            (sender as FrameworkElement)?.Tag is not DateTime date ||
            e.Data.GetData(typeof(string)) is not string draggedId)
        {
            return;
        }

        var separator = draggedId.IndexOf(':');
        if (separator < 0)
        {
            return;
        }
        var itemType = draggedId[..separator];
        var itemId = draggedId[(separator + 1)..];
        if (itemType == "task" && data.Tasks.FirstOrDefault(item => item.Id == itemId) is TaskItem task)
        {
            if (!canManageTasks) return;
            var timeOfDay = task.DueAt.TimeOfDay;
            task.DueAt = date.Date + timeOfDay;
            if (task.EndAt is DateTime endAt)
            {
                task.EndAt = date.Date + endAt.TimeOfDay;
            }
        }
        else if (itemType == "trip" && data.Operations.FirstOrDefault(item => item.Id == itemId && item.Kind == OperationKinds.Trip) is OperationRecord trip)
        {
            if (!canManageTrips) return;
            trip.Date = date.Date;
        }
        else
        {
            return;
        }
        ApplicationDataStore.Save(data);
        Refresh();
        e.Handled = true;
    }

    private void MonthViewClick(object sender, RoutedEventArgs e) { period = CalendarPeriod.Month; Refresh(); }
    private void WeekViewClick(object sender, RoutedEventArgs e) { period = CalendarPeriod.Week; Refresh(); }
    private void DayViewClick(object sender, RoutedEventArgs e) { period = CalendarPeriod.Day; Refresh(); }

    private void PreviousClick(object sender, RoutedEventArgs e)
    {
        currentDate = period switch
        {
            CalendarPeriod.Day => currentDate.AddDays(-1),
            CalendarPeriod.Week => currentDate.AddDays(-7),
            _ => currentDate.AddMonths(-1)
        };
        Refresh();
    }

    private void NextClick(object sender, RoutedEventArgs e)
    {
        currentDate = period switch
        {
            CalendarPeriod.Day => currentDate.AddDays(1),
            CalendarPeriod.Week => currentDate.AddDays(7),
            _ => currentDate.AddMonths(1)
        };
        Refresh();
    }

    private void TodayClick(object sender, RoutedEventArgs e)
    {
        currentDate = DateTime.Today;
        Refresh();
    }

    private void FilterChanged(object sender, SelectionChangedEventArgs e) => Refresh();

    private void UpdateViewButtons()
    {
        SetViewButton(MonthViewButton, period == CalendarPeriod.Month);
        SetViewButton(WeekViewButton, period == CalendarPeriod.Week);
        SetViewButton(DayViewButton, period == CalendarPeriod.Day);
    }

    private static void SetViewButton(Button button, bool active)
    {
        button.Background = active ? new SolidColorBrush(Color.FromRgb(214, 178, 94)) : Brushes.Transparent;
        button.Foreground = active ? new SolidColorBrush(Color.FromRgb(16, 33, 42)) : new SolidColorBrush(Color.FromRgb(206, 217, 221));
        button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private enum CalendarPeriod
    {
        Month,
        Week,
        Day
    }

    private sealed class CalendarEntry
    {
        private CalendarEntry(TaskItem task) => Task = task;
        private CalendarEntry(OperationRecord trip) => Trip = trip;

        public TaskItem? Task { get; }
        public OperationRecord? Trip { get; }
        public DateTime DateTime => Task?.DueAt ?? TripDateTime(Trip!);
        public bool HasTime => Task?.HasTime ?? !string.IsNullOrWhiteSpace(Trip?.Time);
        public string Title => Task?.Title ?? (string.IsNullOrWhiteSpace(Trip?.Nature) ? Trip!.Number : Trip.Nature);
        public string Status => Task?.Status ?? Trip!.Status;
        public bool IsCompleted => Task?.Status == "Terminée" || Trip?.Status == "Terminé";
        public bool IsCancelled => Task?.Status == "Annulée" || Trip?.Status == "Annulé";
        public string DragId => Task is not null ? $"task:{Task.Id}" : $"trip:{Trip!.Id}";

        public static CalendarEntry ForTask(TaskItem task) => new(task);
        public static CalendarEntry ForTrip(OperationRecord trip) => new(trip);

        private static DateTime TripDateTime(OperationRecord trip) =>
            trip.Date.Date + (TimeSpan.TryParseExact(trip.Time, "hh\\:mm", CultureInfo.InvariantCulture, out var time)
                ? time
                : TimeSpan.Zero);
    }
}
