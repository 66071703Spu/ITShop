using ITShop.Models;

namespace ITShop.Helpers;

public static class BackOfficeAccessHelper
{
    // อ่าน role ปัจจุบันของผู้ใช้จาก session โดยให้ค่าเริ่มต้นเป็น user
    public static string GetCurrentRole(ISession session)
    {
        return session.GetString("UserRole") ?? "user";
    }

    // ตรวจว่า role นี้ถือเป็นผู้ดูแลหลังบ้านหรือไม่
    public static bool IsAdminRole(string? roleName)
    {
        return string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleName, "superadmin", StringComparison.OrdinalIgnoreCase);
    }

    // ตรวจว่า role นี้เป็น superadmin โดยตรงหรือไม่
    public static bool IsSuperAdmin(string? roleName)
    {
        return string.Equals(roleName, "superadmin", StringComparison.OrdinalIgnoreCase);
    }

    // คืนค่าลำดับความสำคัญของ role เพื่อใช้เรียงสิทธิ์และเส้นทางหลังบ้าน
    public static int GetRolePriority(string? roleName)
    {
        return (roleName ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "superadmin" => 3,
            "admin" => 2,
            _ => 1
        };
    }

    // ตรวจว่า role นี้มี permission ที่ร้องขออยู่ในฐานข้อมูลหรือไม่
    public static bool HasPermission(Csi402dbContext db, string roleName, string permissionName)
    {
        if (IsSuperAdmin(roleName))
        {
            return true;
        }

        return db.RolePermissions
            .Where(rp => rp.Role != null && rp.Permission != null)
            .Any(rp => rp.Role!.RoleName == roleName && rp.Permission!.PermissionName == permissionName);
    }
}