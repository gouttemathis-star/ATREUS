namespace ATREUS;

public static class VehicleTypes
{
    public const string CreateCategoryAction = "__create_vehicle_category__";

    public static IReadOnlyList<VehicleCategory> Defaults { get; } =
    [
        new() { Name = "Voiture", Emoji = "🚗" },
        new() { Name = "Camion", Emoji = "🚚" },
        new() { Name = "Camionnette", Emoji = "🚐" },
        new() { Name = "Moto", Emoji = "🏍️" },
        new() { Name = "Scooter", Emoji = "🛵" },
        new() { Name = "Fourgon", Emoji = "🚐" },
        new() { Name = "Véhicule utilitaire", Emoji = "🚙" },
        new() { Name = "Autocar / bus", Emoji = "🚌" }
    ];

    public static List<VehicleCategoryChoice> Choices(IEnumerable<VehicleCategory> categories) =>
        categories.Select(category => new VehicleCategoryChoice(category.Name, category.Emoji))
            .Append(new VehicleCategoryChoice(CreateCategoryAction, "➕"))
            .ToList();

    public static string Display(string? name, IEnumerable<VehicleCategory> categories) =>
        categories.FirstOrDefault(category => category.Name == name)?.Display ?? $"🚘 {name}";
}

public sealed class VehicleCategory
{
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🚘";
    [System.Text.Json.Serialization.JsonIgnore]
    public string Display => $"{Emoji}  {Name}";
}

public sealed class VehicleCategoryChoice
{
    public string Name { get; }
    public string Emoji { get; }
    public bool IsCreateAction => Name == VehicleTypes.CreateCategoryAction;
    public string Display => IsCreateAction ? $"{Emoji}  Créer une catégorie de véhicule…" : $"{Emoji}  {Name}";

    public VehicleCategoryChoice(string name, string emoji)
    {
        Name = name;
        Emoji = emoji;
    }
}

public sealed class Driver
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public static class UserRoles
{
    public const string Administrator = "Administrateur";
    public const string Operations = "Exploitation";
    public const string Driver = "Conducteur";
    public const string FleetManager = "Responsable de parc";
    public const string Mechanic = "Mécanicien";
    public const string Planner = "Planificateur";
    public const string Secretary = "Secrétariat";
    public const string Safety = "Sécurité et prévention";
    public const string Accounting = "Comptabilité";
    public const string Observer = "Observateur";
    public const string Procurement = "Approvisionneur";
    public const string Garage = "Garage partenaire";
    public const string Management = "Direction";

    public static IReadOnlyList<UserRoleDefinition> Definitions { get; } =
    [
        new(Administrator, "🔐", "Accès complet, gestion des comptes et paramétrage.", true, true, true, true, true, true, true, true, true, true, true, true, true),
        new(Operations, "🧭", "Pilotage quotidien du parc et des opérations.", true, true, true, true, true, true, true, true, true, true, true, false, true),
        new(Driver, "🚚", "Planning, trajets et véhicule(s) qui lui sont attribués.", false, false, false, false, false, false, false, false, false, false, false, false, false, true),
        new(FleetManager, "🚗", "Gestion du parc, des affectations, des opérations et du calendrier.", true, true, true, true, true, true, true, true, true, true, true, false, true),
        new(Mechanic, "🔧", "Consultation du parc et gestion des maintenances et tâches atelier.", true, false, true, false, true, true, false, false, true, false, true, false, false),
        new(Planner, "🗓️", "Organisation et gestion des trajets et des tâches.", true, true, true, false, false, true, false, false, false, true, true, false, false),
        new(Secretary, "📋", "Consultation du parc et gestion du calendrier administratif.", true, false, true, false, false, true, false, false, false, false, true, false, false),
        new(Safety, "🦺", "Suivi et traitement des incidents et du calendrier associé.", true, false, false, true, false, true, false, true, false, false, true, false, false),
        new(Accounting, "💶", "Consultation du tableau de bord et du parc, sans modification.", true, false, true, false, false, false, false, false, false, false, false, false, true),
        new(Observer, "👁️", "Consultation en lecture seule des vues opérationnelles.", true, true, true, true, true, true, false, false, false, false, false, false, false),
        new(Procurement, "🛒", "Consultation du parc et suivi des maintenances et tâches.", true, false, true, false, true, true, false, false, true, false, true, false, false),
        new(Garage, "🏭", "Accès au parc et traitement des maintenances et tâches atelier.", false, false, true, false, true, true, false, false, true, false, true, false, false),
        new(Management, "💼", "Vue de pilotage et consultation du parc et des opérations.", true, true, true, true, true, true, false, false, false, false, false, false, true)
    ];

    public static IReadOnlyList<string> All { get; } = Definitions.Select(role => role.Name).ToList();

    public static UserRoleDefinition Get(string? role) =>
        Definitions.FirstOrDefault(definition => definition.Name == role) ??
        Definitions.First(definition => definition.Name == Observer);

    public static string Display(string? role) => Get(role).Display;
}

public sealed record UserRoleDefinition(
    string Name,
    string Emoji,
    string Description,
    bool Dashboard,
    bool Map,
    bool Fleet,
    bool Incidents,
    bool Maintenance,
    bool Calendar,
    bool ManageFleet,
    bool ManageIncidents,
    bool ManageMaintenance,
    bool ManageTrips,
    bool ManageTasks,
    bool ManageAccounts,
    bool Export,
    bool PersonalPlanning = false)
{
    public string Display => $"{Emoji}  {Name}";
    public bool ViewTrips => ManageTrips ||
        Name is UserRoles.Administrator or UserRoles.Operations or UserRoles.FleetManager or UserRoles.Observer or UserRoles.Management;
}

public sealed class UserAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.Operations;
    public string? DriverId { get; set; }
    public string PasswordSalt { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public sealed class Vehicle
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Identifier { get; set; } = string.Empty;
    public string Type { get; set; } = VehicleTypes.Defaults[0].Name;
    public string State { get; set; } = "Disponible";
    public string Description { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public string Mileage { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Registration { get; set; } = string.Empty;
    public string Vin { get; set; } = string.Empty;
    public string AcquisitionDate { get; set; } = string.Empty;
    public string PurchasePrice { get; set; } = string.Empty;
    public string ServiceDate { get; set; } = string.Empty;
    public string Energy { get; set; } = string.Empty;
    public string Power { get; set; } = string.Empty;
    public string LastInspection { get; set; } = string.Empty;
    public string NextInspection { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double Latitude { get; set; } = 46.5;
    public double Longitude { get; set; } = 2.35;
    public string? DriverId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string DriverName { get; set; } = "Non attribué";
    [System.Text.Json.Serialization.JsonIgnore]
    public string TypeDisplay { get; set; } = VehicleTypes.Defaults[0].Display;
    public string StateColor => State switch
    {
        "Maintenance" => "#E2A05A",
        "Indisponible" => "#E28076",
        _ => "#7CCB9B"
    };
}

public sealed class OperationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Number { get; set; } = string.Empty;
    public string Kind { get; set; } = OperationKinds.Incident;
    public string VehicleId { get; set; } = string.Empty;
    public string? DriverId { get; set; }
    public string? TripId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public string Time { get; set; } = string.Empty;
    public int DurationMinutes { get; set; } = 60;
    public string Location { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Nature { get; set; } = string.Empty;
    public string Status { get; set; } = "À traiter";
    public string Details { get; set; } = string.Empty;
}

public static class OperationKinds
{
    public const string Incident = "Incident";
    public const string Maintenance = "Maintenance";
    public const string Trip = "Trajet";
}

public sealed class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public DateTime DueAt { get; set; } = DateTime.Today;
    public DateTime? EndAt { get; set; }
    public bool HasTime { get; set; }
    public string Priority { get; set; } = "Normale";
    public string Status { get; set; } = "À faire";
    public string Description { get; set; } = string.Empty;
    public string? VehicleId { get; set; }
    public string? DriverId { get; set; }
    public string? OperationId { get; set; }
}

public sealed class SiteLocation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = SiteKinds.Headquarters;
    public string Icon { get; set; } = "🏢";
    public double Latitude { get; set; } = 48.8566;
    public double Longitude { get; set; } = 2.3522;
    public string Details { get; set; } = string.Empty;
}

public static class SiteKinds
{
    public const string Headquarters = "Siège social";

    public static IReadOnlyList<SiteKindOption> Defaults { get; } =
    [
        new(Headquarters, "🏢"),
        new("Parc automobile", "🚗"),
        new("Dépôt de véhicules", "🏭"),
        new("Garagiste", "🔧"),
        new("Autre lieu", "📍")
    ];
}

public sealed class SiteKindOption(string name, string icon)
{
    public string Name { get; } = name;
    public string Icon { get; } = icon;
    public string Display => $"{Icon}  {Name}";
}

public sealed class ApplicationData
{
    public int DemoContentVersion { get; set; }
    public string LocalAiModel { get; set; } = "qwen2.5:3b";
    public List<Vehicle> Vehicles { get; set; } = [];
    public List<VehicleCategory> VehicleCategories { get; set; } = VehicleTypes.Defaults.Select(category => new VehicleCategory { Name = category.Name, Emoji = category.Emoji }).ToList();
    public List<Driver> Drivers { get; set; } = [];
    public List<OperationRecord> Operations { get; set; } = [];
    public List<TaskItem> Tasks { get; set; } = [];
    public List<SiteLocation> Sites { get; set; } = [];
    public List<UserAccount> Users { get; set; } = [];

    public Driver? FindDriver(string? id) => Drivers.FirstOrDefault(driver => driver.Id == id);
    public UserAccount? FindUser(string? id) => Users.FirstOrDefault(user => user.Id == id);
    public Vehicle? FindVehicle(string? id) => Vehicles.FirstOrDefault(vehicle => vehicle.Id == id);
    public OperationRecord? FindOperation(string? id) => Operations.FirstOrDefault(operation => operation.Id == id);
    public string DriverName(string? id) => FindDriver(id)?.Name ?? "Non attribué";

    public bool EnsureVehicleCategories()
    {
        VehicleCategories ??= [];
        var changed = false;
        foreach (var defaultCategory in VehicleTypes.Defaults)
        {
            var category = VehicleCategories.FirstOrDefault(item => string.Equals(item.Name, defaultCategory.Name, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                VehicleCategories.Add(new VehicleCategory { Name = defaultCategory.Name, Emoji = defaultCategory.Emoji });
                changed = true;
            }
            else if (string.IsNullOrWhiteSpace(category.Emoji))
            {
                category.Emoji = defaultCategory.Emoji;
                changed = true;
            }
        }

        var legacyOther = VehicleCategories.FirstOrDefault(item => item.Name == "Autre véhicule");
        if (legacyOther is not null)
        {
            VehicleCategories.Remove(legacyOther);
            changed = true;
        }

        foreach (var vehicle in Vehicles)
        {
            if (vehicle.Type == "Autre véhicule")
            {
                vehicle.Type = "Autre véhicule personnalisé";
                changed = true;
            }
            if (!VehicleCategories.Any(category => string.Equals(category.Name, vehicle.Type, StringComparison.OrdinalIgnoreCase)))
            {
                VehicleCategories.Add(new VehicleCategory { Name = vehicle.Type, Emoji = "🚘" });
                changed = true;
            }
        }
        return changed;
    }
}
