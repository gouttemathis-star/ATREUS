using System.IO;
using System.Text.Json;

namespace ATREUS;

public static class ApplicationDataStore
{
    private static readonly string DataFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ATREUS",
        "data.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static ApplicationData Load()
    {
        if (!File.Exists(DataFilePath))
        {
            var initialData = CreateInitialData();
            Save(initialData);
            return initialData;
        }

        var json = File.ReadAllText(DataFilePath);
        var data = JsonSerializer.Deserialize<ApplicationData>(json, JsonOptions)
            ?? throw new InvalidDataException("Le fichier de données ATREUS est vide ou invalide.");

        data.Vehicles ??= [];
        data.Drivers ??= [];
        data.Operations ??= [];
        data.Tasks ??= [];
        data.Sites ??= [];
        data.Users ??= [];
        data.PasswordResetRequests ??= [];
        var changed = string.IsNullOrWhiteSpace(data.LocalAiModel);
        data.LocalAiModel = string.IsNullOrWhiteSpace(data.LocalAiModel) ? "qwen2.5:3b" : data.LocalAiModel;
        if (data.AccessControlVersion < 1)
        {
            foreach (var user in data.Users)
            {
                var isAdmin = user.Role == UserRoles.Administrator || AccessControl.IsProtectedAdministrator(user);
                AccessControl.ApplyPreset(user, isAdmin ? AccessLevels.Level3 : AccessLevels.Level1);
                if (AccessControl.IsProtectedAdministrator(user))
                {
                    user.Role = UserRoles.Administrator;
                }
            }
            data.AccessControlVersion = 1;
            changed = true;
        }
        changed |= data.EnsureVehicleCategories();
        changed |= EnsureDemonstrationContent(data);
        if (changed)
        {
            Save(data);
        }
        return data;
    }

    public static void Save(ApplicationData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataFilePath)!);
        var temporaryFile = DataFilePath + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(temporaryFile, DataFilePath, overwrite: true);
    }

    private static ApplicationData CreateInitialData()
    {
        var data = new ApplicationData();
        data.AccessControlVersion = 1;
        var drivers = new[]
        {
            new Driver { Name = "Camille Bernard", Phone = "06 10 20 30 40" },
            new Driver { Name = "Alexandre Moreau", Phone = "06 11 21 31 41" },
            new Driver { Name = "Sarah Petit", Phone = "06 12 22 32 42" }
        };
        data.Drivers.AddRange(drivers);
        data.Vehicles.AddRange(
        [
            new Vehicle { Identifier = "AT-021", Type = "Camionnette", State = "Disponible", Description = "Camionnette de liaison", Plate = "OR-021-VT", Brand = "Renault", Model = "Trafic", Mileage = "42 680 km", Location = "Lyon", Latitude = 45.7640, Longitude = 4.8357, DriverId = drivers[0].Id },
            new Vehicle { Identifier = "AT-014", Type = "Voiture", State = "Disponible", Description = "Véhicule de coordination", Plate = "VM-014-VB", Brand = "Peugeot", Model = "508", Mileage = "31 240 km", Location = "Paris", Latitude = 48.8566, Longitude = 2.3522, DriverId = drivers[1].Id },
            new Vehicle { Identifier = "AT-044", Type = "Camion", State = "Disponible", Description = "Camion logistique", Plate = "HC-044-TR", Brand = "Iveco", Model = "Eurocargo", Mileage = "88 410 km", Location = "Marseille", Latitude = 43.2965, Longitude = 5.3698, DriverId = drivers[2].Id },
            new Vehicle { Identifier = "AT-117", Type = "Véhicule utilitaire", State = "Indisponible", Description = "Véhicule d'intervention", Plate = "OR-117-VC", Brand = "Citroën", Model = "Jumpy", Mileage = "56 090 km", Location = "Toulouse", Latitude = 43.6045, Longitude = 1.4440 }
        ]);
        data.Operations.Add(new OperationRecord
        {
            Number = "INC-001",
            Kind = OperationKinds.Incident,
            VehicleId = data.Vehicles[3].Id,
            Date = DateTime.Today.AddDays(-1),
            Location = "Toulouse",
            Nature = "Contrôle moteur",
            Status = "À traiter",
            Details = "Voyant moteur signalé lors du retour."
        });
        data.Operations.Add(new OperationRecord
        {
            Number = "MAI-001",
            Kind = OperationKinds.Maintenance,
            VehicleId = data.Vehicles[2].Id,
            DriverId = drivers[2].Id,
            Date = DateTime.Today.AddDays(2),
            Location = "Marseille",
            Nature = "Révision périodique",
            Status = "Planifiée",
            Details = "Contrôle et révision du véhicule."
        });
        data.Tasks.Add(new TaskItem
        {
            Title = "Préparer la révision AT-044",
            DueAt = DateTime.Today.AddDays(2),
            Priority = "Haute",
            Status = "À faire",
            Description = "Préparer le véhicule avant le rendez-vous.",
            VehicleId = data.Vehicles[2].Id,
            DriverId = drivers[2].Id,
            OperationId = data.Operations[1].Id
        });
        return data;
    }

    private static bool EnsureDemonstrationContent(ApplicationData data)
    {
        if (data.DemoContentVersion >= 2)
        {
            return false;
        }

        const double latitude = 48.3424118;
        const double longitude = -1.4470910;
        const string headquartersAddress = "13 lieu-dit La Basse Haye, 35140 Saint-Christophe-de-Valains";
        var changed = false;

        if (!data.Sites.Any(site => site.Id == "demo-site-headquarters" ||
            (site.Kind == SiteKinds.Headquarters && site.Details.Contains(headquartersAddress, StringComparison.OrdinalIgnoreCase))))
        {
            data.Sites.Add(new SiteLocation
            {
                Id = "demo-site-headquarters",
                Name = "Siège social ATREUS",
                Kind = SiteKinds.Headquarters,
                Icon = "🏢",
                Latitude = latitude,
                Longitude = longitude,
                Details = $"Adresse : {headquartersAddress}. Position cartographique indicative, centrée sur la commune ; le point GPS exact de l'adresse reste à confirmer."
            });
            changed = true;
        }

        AddDemoSite(data, new SiteLocation
        {
            Id = "demo-site-depot",
            Name = "Dépôt de démonstration",
            Kind = "Dépôt de véhicules",
            Icon = "🏭",
            Latitude = latitude + 0.0032,
            Longitude = longitude + 0.0050,
            Details = "Site fictif de démonstration, position indicative près du siège. À confirmer avant toute utilisation opérationnelle."
        }, ref changed);
        AddDemoSite(data, new SiteLocation
        {
            Id = "demo-site-depot-fougeres",
            Name = "Dépôt de démonstration — Fougères",
            Kind = "Dépôt de véhicules",
            Icon = "🏭",
            Latitude = 48.3518,
            Longitude = -1.1995,
            Details = "Site fictif de démonstration, position indicative à Fougères. À confirmer avant toute utilisation opérationnelle."
        }, ref changed);
        AddDemoSite(data, new SiteLocation
        {
            Id = "demo-site-garage",
            Name = "Garage partenaire de démonstration",
            Kind = "Garagiste",
            Icon = "🔧",
            Latitude = latitude - 0.0030,
            Longitude = longitude - 0.0045,
            Details = "Site fictif de démonstration, position indicative près du siège. À remplacer par l'adresse réelle du garage."
        }, ref changed);

        var demoDrivers = Enumerable.Range(1, 6)
            .Select(index =>
            {
                var id = $"demo-driver-{index:00}";
                var driver = data.Drivers.FirstOrDefault(item => item.Id == id);
                if (driver is null)
                {
                    driver = new Driver { Id = id, Name = $"Chauffeur démo {index:00}", Notes = "Profil fictif de démonstration — à remplacer avant utilisation réelle." };
                    data.Drivers.Add(driver);
                    changed = true;
                }
                return driver;
            })
            .ToArray();

        var locations = new (string Name, double Latitude, double Longitude)[]
        {
            ("Saint-Christophe-de-Valains", 48.3424, -1.4471),
            ("Fougères", 48.3518, -1.1995),
            ("Rennes", 48.1173, -1.6778),
            ("Vitré", 48.1242, -1.2135),
            ("Liffré", 48.2131, -1.5087),
            ("Saint-Malo", 48.6493, -2.0257),
            ("Dinan", 48.4550, -2.0500),
            ("Laval", 48.0709, -0.7735),
            ("Avranches", 48.6845, -1.3560),
            ("Mayenne", 48.3039, -0.6139),
            ("Vernon", 49.0929, 1.4633),
            ("Caen", 49.1829, -0.3707),
            ("Saint-Brieuc", 48.5142, -2.7658),
            ("Nantes", 47.2184, -1.5536),
            ("Angers", 47.4784, -0.5632)
        };
        var specifications = new (string Type, string Brand, string Model, string Energy, string Power, string Mileage, string State)[]
        {
            ("Voiture", "Renault", "Clio", "Essence", "90 ch", "12 450", "Disponible"),
            ("Fourgon", "Renault", "Master", "Diesel", "135 ch", "48 200", "Disponible"),
            ("Camionnette", "Peugeot", "Expert", "Diesel", "120 ch", "36 800", "En intervention"),
            ("Camion", "Iveco", "Daily", "Diesel", "160 ch", "72 100", "Disponible"),
            ("Véhicule utilitaire", "Citroën", "Berlingo", "Diesel", "100 ch", "29 600", "Disponible"),
            ("Voiture", "Toyota", "Corolla", "Hybride", "140 ch", "18 900", "Disponible"),
            ("Autocar / bus", "Iveco", "Crossway", "Diesel", "320 ch", "114 000", "Maintenance"),
            ("Camionnette", "Ford", "Transit Custom", "Diesel", "130 ch", "54 300", "Disponible"),
            ("Moto", "Yamaha", "Tracer 7", "Essence", "73 ch", "8 750", "Disponible"),
            ("Scooter", "Honda", "Forza 350", "Essence", "29 ch", "4 200", "Disponible"),
            ("Voiture", "Peugeot", "308", "Hybride", "145 ch", "21 600", "Disponible"),
            ("Fourgon", "Fiat", "Ducato", "Diesel", "140 ch", "63 500", "Indisponible"),
            ("Camion", "Mercedes-Benz", "Atego", "Diesel", "180 ch", "91 200", "Disponible"),
            ("Véhicule utilitaire", "Dacia", "Duster", "Hybride", "130 ch", "15 300", "Disponible"),
            ("Camionnette", "Volkswagen", "Transporter", "Diesel", "150 ch", "42 750", "Disponible")
        };
        var vehicles = new Vehicle[specifications.Length];
        for (var index = 0; index < specifications.Length; index++)
        {
            var specification = specifications[index];
            var location = locations[index];
            var id = $"demo-vehicle-{index + 1:00}";
            var vehicle = data.Vehicles.FirstOrDefault(item => item.Id == id);
            if (vehicle is null)
            {
                vehicle = new Vehicle
                {
                    Id = id,
                    Identifier = $"DEMO-VEH-{index + 1:000}",
                    Type = specification.Type,
                    State = specification.State,
                    Description = "Donnée fictive de démonstration — à vérifier ou remplacer avant toute utilisation opérationnelle.",
                    Brand = specification.Brand,
                    Model = specification.Model,
                    Mileage = specification.Mileage,
                    Energy = specification.Energy,
                    Power = specification.Power,
                    Location = location.Name,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    DriverId = demoDrivers[index % demoDrivers.Length].Id,
                    NextInspection = DateTime.Today.AddDays(30 + index * 4).ToString("dd/MM/yyyy")
                };
                data.Vehicles.Add(vehicle);
                changed = true;
            }
            vehicles[index] = vehicle;
        }

        AddDemoOperation(data, new OperationRecord
        {
            Id = "demo-operation-maintenance-01",
            Number = "DEMO-MAI-001",
            Kind = OperationKinds.Maintenance,
            VehicleId = vehicles[6].Id,
            DriverId = vehicles[6].DriverId,
            Date = DateTime.Today.AddDays(3),
            Time = "09:00",
            Location = "Dépôt de démonstration",
            Nature = "Révision périodique",
            Status = "Planifiée",
            Details = "Exemple fictif de révision, créé pour illustrer le suivi lié au véhicule."
        }, ref changed);
        AddDemoOperation(data, new OperationRecord
        {
            Id = "demo-operation-incident-01",
            Number = "DEMO-INC-001",
            Kind = OperationKinds.Incident,
            VehicleId = vehicles[11].Id,
            DriverId = vehicles[11].DriverId,
            Date = DateTime.Today.AddDays(-1),
            Location = "Laval",
            Nature = "Contrôle de démonstration",
            Status = "À traiter",
            Details = "Incident fictif de démonstration — aucune panne réelle n'est signalée."
        }, ref changed);
        AddDemoOperation(data, new OperationRecord
        {
            Id = "demo-operation-trip-01",
            Number = "DEMO-TRJ-001",
            Kind = OperationKinds.Trip,
            VehicleId = vehicles[2].Id,
            DriverId = vehicles[2].DriverId,
            Date = DateTime.Today.AddDays(1),
            Time = "08:30",
            DurationMinutes = 75,
            Location = "Dépôt de démonstration",
            Destination = "Rennes",
            Nature = "Trajet de démonstration",
            Status = "Planifié",
            Details = "Trajet fictif de démonstration — à remplacer par une opération réelle."
        }, ref changed);

        AddDemoTask(data, new TaskItem
        {
            Id = "demo-task-maintenance-01",
            Title = "DÉMO · Préparer la révision du véhicule",
            DueAt = DateTime.Today.AddDays(2).AddHours(9),
            HasTime = true,
            Priority = "Haute",
            Status = "À faire",
            Description = "Tâche fictive de démonstration, liée à une maintenance et à un véhicule d'exemple.",
            VehicleId = vehicles[6].Id,
            DriverId = vehicles[6].DriverId,
            OperationId = "demo-operation-maintenance-01"
        }, ref changed);
        AddDemoTask(data, new TaskItem
        {
            Id = "demo-task-inspection-01",
            Title = "DÉMO · Vérifier les échéances du parc",
            DueAt = DateTime.Today.AddDays(7).AddHours(10),
            HasTime = true,
            Priority = "Normale",
            Status = "À faire",
            Description = "Exemple fictif de tâche de contrôle administratif du parc.",
            VehicleId = vehicles[0].Id,
            DriverId = vehicles[0].DriverId
        }, ref changed);

        data.DemoContentVersion = 2;
        return true;
    }

    private static void AddDemoSite(ApplicationData data, SiteLocation site, ref bool changed)
    {
        if (data.Sites.Any(item => item.Id == site.Id || string.Equals(item.Name, site.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        data.Sites.Add(site);
        changed = true;
    }

    private static void AddDemoOperation(ApplicationData data, OperationRecord operation, ref bool changed)
    {
        if (data.Operations.Any(item => item.Id == operation.Id || item.Number == operation.Number))
        {
            return;
        }
        data.Operations.Add(operation);
        changed = true;
    }

    private static void AddDemoTask(ApplicationData data, TaskItem task, ref bool changed)
    {
        if (data.Tasks.Any(item => item.Id == task.Id))
        {
            return;
        }
        data.Tasks.Add(task);
        changed = true;
    }
}
