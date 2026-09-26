using System.Security.Cryptography;
using System.Text;
using ITShop.Models;
using Microsoft.AspNetCore.Identity;

namespace ITShop.Helpers;

public static class UserPasswordService
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static string Hash(User user, string password) => Hasher.HashPassword(user, password);

    public static bool Verify(User user, string password)
    {
        if (IsHashed(user.PasswordHash))
        {
            return Hasher.VerifyHashedPassword(user, user.PasswordHash, password)
                != PasswordVerificationResult.Failed;
        }

        var stored = Encoding.UTF8.GetBytes(user.PasswordHash);
        var supplied = Encoding.UTF8.GetBytes(password);
        return CryptographicOperations.FixedTimeEquals(stored, supplied);
    }

    public static bool IsHashed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length >= 45 && (bytes[0] == 0 || bytes[0] == 1);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static int MigrateLegacyPasswords(Csi402dbContext db)
    {
        var users = db.Users.ToList();
        var migrated = 0;
        foreach (var user in users.Where(user => !IsHashed(user.PasswordHash)))
        {
            user.PasswordHash = Hash(user, user.PasswordHash);
            migrated++;
        }

        db.SaveChanges();
        return migrated;
    }
}
