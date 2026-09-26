using ITShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ITShop.Helpers;

public static class TestAccountSeeder
{
    private static readonly (string Email, string FirstName, string RoleName)[] Accounts =
    {
        ("user@test.com", "Test User", "user"),
        ("admin@test.com", "Test Admin", "admin"),
        ("superadmin@test.com", "Test SuperAdmin", "superadmin")
    };

    public static IReadOnlyList<string> Seed(Csi402dbContext db, string password)
    {
        var roleIds = db.Roles
            .Where(role => Accounts.Select(account => account.RoleName).Contains(role.RoleName))
            .ToDictionary(role => role.RoleName, role => role.RoleId, StringComparer.OrdinalIgnoreCase);

        var missingRoles = Accounts
            .Select(account => account.RoleName)
            .Where(roleName => !roleIds.ContainsKey(roleName))
            .ToList();

        if (missingRoles.Count > 0)
        {
            throw new InvalidOperationException($"Missing required roles: {string.Join(", ", missingRoles)}");
        }

        var messages = new List<string>();

        foreach (var account in Accounts)
        {
            var user = db.Users
                .Include(existingUser => existingUser.UserRoles)
                .FirstOrDefault(existingUser => existingUser.Email == account.Email);

            var wasCreated = user == null;
            if (user == null)
            {
                user = new User
                {
                    FirstName = account.FirstName,
                    LastName = "Account",
                    Email = account.Email,
                    CreatedAt = DateTime.Now
                };
                db.Users.Add(user);
            }

            user.PasswordHash = UserPasswordService.Hash(user, password);
            user.Status = "active";
            db.SaveChanges();

            db.UserRoles.RemoveRange(user.UserRoles);
            db.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = roleIds[account.RoleName]
            });
            db.SaveChanges();

            messages.Add($"{(wasCreated ? "Created" : "Reset")} {account.Email} ({account.RoleName})");
        }

        return messages;
    }
}
