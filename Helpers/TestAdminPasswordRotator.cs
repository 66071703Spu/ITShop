using ITShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ITShop.Helpers;

public static class TestAdminPasswordRotator
{
    private static readonly (string Email, string Role, string ConfigKey)[] Accounts =
    {
        ("admin@test.com", "admin", "TestAccounts:AdminPassword"),
        ("superadmin@test.com", "superadmin", "TestAccounts:SuperAdminPassword")
    };

    public static IReadOnlyList<string> Rotate(Csi402dbContext db, IConfiguration configuration)
    {
        var changes = Accounts.Select(account =>
        {
            var password = configuration[account.ConfigKey];
            if (string.IsNullOrWhiteSpace(password) || password.Length < 16)
            {
                throw new InvalidOperationException($"Set {account.ConfigKey.Replace(":", "__")} to a password of at least 16 characters.");
            }

            var user = db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                .SingleOrDefault(u => u.Email == account.Email);

            if (user == null || !user.UserRoles.Any(userRole =>
                    string.Equals(userRole.Role.RoleName, account.Role, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Expected test account with {account.Role} role: {account.Email}");
            }

            return (User: user, Password: password);
        }).ToList();

        if (string.Equals(changes[0].Password, changes[1].Password, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Admin and SuperAdmin must have different passwords.");
        }

        using var transaction = db.Database.BeginTransaction();
        foreach (var change in changes)
        {
            change.User.PasswordHash = UserPasswordService.Hash(change.User, change.Password);
            change.User.Status = "active";
        }

        db.SaveChanges();
        transaction.Commit();

        return changes.Select(change => $"Rotated {change.User.Email}").ToList();
    }
}
