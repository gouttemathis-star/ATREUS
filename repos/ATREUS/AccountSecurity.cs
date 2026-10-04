using System.Security.Cryptography;

namespace ATREUS;

public static class AccountSecurity
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 210_000;

    public static UserAccount CreateAccount(
        ApplicationData data,
        string userName,
        string displayName,
        string role,
        string accessLevel,
        string? driverId,
        string password)
    {
        userName = userName.Trim();
        displayName = displayName.Trim();

        if (userName.Length < 3)
        {
            throw new ArgumentException("L’identifiant doit contenir au moins 3 caractères.");
        }
        if (displayName.Length == 0)
        {
            throw new ArgumentException("Le nom affiché est obligatoire.");
        }
        if (!UserRoles.All.Contains(role, StringComparer.Ordinal))
        {
            throw new ArgumentException("Le rôle sélectionné n’est pas valide.");
        }
        if (data.Users.Any(user => string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Cet identifiant existe déjà.");
        }
        if (role == UserRoles.Driver && data.FindDriver(driverId) is null)
        {
            throw new ArgumentException("Un compte conducteur doit être associé à une fiche chauffeur.");
        }
        if (role == UserRoles.Driver && data.Users.Any(user =>
            user.IsEnabled && user.Role == UserRoles.Driver && user.DriverId == driverId))
        {
            throw new ArgumentException("Un compte actif est déjà associé à cette fiche chauffeur.");
        }
        if (password.Length < 10)
        {
            throw new ArgumentException("Le mot de passe doit contenir au moins 10 caractères.");
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        var account = new UserAccount
        {
            UserName = userName,
            DisplayName = displayName,
            Role = role,
            DriverId = driverId,
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(hash)
        };
        AccessControl.ApplyPreset(account, accessLevel);
        return account;
    }

    public static bool CanManageAccount(UserAccount? actor, UserAccount target) =>
        actor is not null &&
        AccessControl.Resolve(actor).ManageAccounts &&
        !AccessControl.IsProtectedAdministrator(target) &&
        (actor.AccessLevel != AccessLevels.Special || target.AccessLevel != AccessLevels.Level3);

    public static bool CanAssignAccessLevel(UserAccount? actor, string accessLevel) =>
        actor is null ||
        AccessControl.Resolve(actor).ManageAccounts &&
        (actor.AccessLevel != AccessLevels.Special || accessLevel != AccessLevels.Level3);

    public static bool CanDeleteVehicle(UserAccount? actor) =>
        actor is not null && AccessControl.Resolve(actor).DeleteVehicles;

    public static void SetPassword(UserAccount user, string password)
    {
        if (password.Length < 10)
        {
            throw new ArgumentException("Le mot de passe doit contenir au moins 10 caractères.");
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        user.PasswordSalt = Convert.ToBase64String(salt);
        user.PasswordHash = Convert.ToBase64String(hash);
    }

    public static string CreateTemporaryPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static UserAccount? Authenticate(ApplicationData data, string userName, string password)
    {
        var user = data.Users.FirstOrDefault(candidate =>
            candidate.IsEnabled &&
            string.Equals(candidate.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user is null ||
            !Convert.TryFromBase64String(user.PasswordSalt, new byte[SaltSize], out var saltLength) ||
            saltLength != SaltSize)
        {
            return null;
        }

        var salt = Convert.FromBase64String(user.PasswordSalt);
        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromBase64String(user.PasswordHash);
        }
        catch (FormatException)
        {
            return null;
        }

        var suppliedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return expectedHash.Length == suppliedHash.Length &&
               CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash)
            ? user
            : null;
    }
}
