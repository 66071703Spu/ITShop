using ITShop.Helpers;
using ITShop.Models;
using ITShop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ITShop.Controllers;

public class SuperAdminController : Controller
{
    private static readonly string[] AdminVisiblePermissions =
    {
        "dashboard.view",
        "products.manage",
        "stock.manage",
        "banners.manage",
        "promotions.manage",
        "brands.manage",
        "orders.view",
        "orders.manage",
        "customers.manage"
    };

    private static readonly string[] UserVisiblePermissions = Array.Empty<string>();

    private readonly Csi402dbContext _db;

    public SuperAdminController(Csi402dbContext db)
    {
        _db = db;
    }

    // จำกัดทุก action ใน controller นี้ให้เฉพาะผู้ใช้ superadmin ที่ยืนยันตัวตนแล้ว
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        var currentRole = BackOfficeAccessHelper.GetCurrentRole(HttpContext.Session);

        if (!userId.HasValue || !BackOfficeAccessHelper.IsSuperAdmin(currentRole))
        {
            TempData["AdminError"] = "หน้านี้สำหรับ superadmin เท่านั้น";
            context.Result = RedirectToAction("Login", "Account");
            return;
        }

        ViewBag.CurrentAdminRole = currentRole;
        base.OnActionExecuting(context);
    }

    // แสดงหน้า dashboard ระดับสูงของ superadmin พร้อมสถิติผู้ใช้ role และ permission
    public IActionResult Dashboard()
    {
        var users = _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.CreatedAt)
            .ToList();

        var vm = new SuperAdminDashboardViewModel
        {
            TotalCustomers = users.Count(u => u.UserRoles.Any(ur => ur.Role.RoleName == "user")),
            TotalAdmins = users.Count(u => u.UserRoles.Any(ur => ur.Role.RoleName == "admin")),
            TotalSuperAdmins = users.Count(u => u.UserRoles.Any(ur => ur.Role.RoleName == "superadmin")),
            TotalPermissions = _db.Permissions.Count(),
            RecentAdmins = users
                .Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == "admin" || ur.Role.RoleName == "superadmin"))
                .Take(8)
                .Select(MapAdminUser)
                .ToList()
        };

        return View(vm);
    }

    // แสดงโปรไฟล์หลังบ้านของ superadmin ปัจจุบัน
    public IActionResult Profile()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var user = _db.Users
            .Include(u => u.Addresses)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefault(u => u.UserId == userId.Value);

        if (user == null)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }

        return View(BuildBackOfficeProfile(user));
    }

    // แสดงรายชื่อบัญชีพนักงานฝั่ง admin และ superadmin ทั้งหมด
    public IActionResult AdminList()
    {
        var users = _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == "admin" || ur.Role.RoleName == "superadmin"))
            .OrderBy(u => u.CreatedAt)
            .ToList()
            .Select(MapAdminUser)
            .ToList();

        return View(users);
    }

    // แสดงโปรไฟล์ของบัญชีพนักงานที่เลือก
    public IActionResult AdminProfile(int id)
    {
        var user = _db.Users
            .Include(u => u.Addresses)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefault(u => u.UserId == id);

        if (user == null || !user.UserRoles.Any(ur => ur.Role.RoleName == "admin" || ur.Role.RoleName == "superadmin"))
        {
            TempData["UserError"] = "ไม่พบบัญชี staff ที่ต้องการ";
            return RedirectToAction("AdminList");
        }

        return View(BuildBackOfficeProfile(user));
    }

    // เปิดฟอร์มสำหรับสร้างบัญชีพนักงานใหม่
    public IActionResult AddAdmin()
    {
        return View(new AdminUserFormViewModel
        {
            Status = "active",
            SelectedRoleId = GetRoleIdByName("admin"),
            RoleOptions = GetStaffRoleOptions()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // สร้างบัญชี admin หรือ superadmin ใหม่หลังตรวจอีเมลและกฎของ role
    public IActionResult AddAdmin(AdminUserFormViewModel data)
    {
        if (string.IsNullOrWhiteSpace(data.Email) || string.IsNullOrWhiteSpace(data.Password))
        {
            ViewBag.Error = "Email และ Password ห้ามว่าง";
            data.RoleOptions = GetStaffRoleOptions();
            return View(data);
        }

        if (!_db.Roles.Any(r => r.RoleId == data.SelectedRoleId && (r.RoleName == "admin" || r.RoleName == "superadmin")))
        {
            ViewBag.Error = "Role ที่เลือกไม่ถูกต้อง";
            data.RoleOptions = GetStaffRoleOptions();
            return View(data);
        }

        if (_db.Users.Any(u => u.Email == data.Email))
        {
            ViewBag.Error = "Email นี้มีในระบบแล้ว";
            data.RoleOptions = GetStaffRoleOptions();
            return View(data);
        }

        var user = new User
        {
            FirstName = string.IsNullOrWhiteSpace(data.FirstName) ? "Admin" : data.FirstName,
            LastName = string.IsNullOrWhiteSpace(data.LastName) ? string.Empty : data.LastName,
            Email = data.Email.Trim(),
            PasswordHash = string.Empty,
            PhoneNumber = data.PhoneNumber,
            Status = string.IsNullOrWhiteSpace(data.Status) ? "active" : data.Status,
            CreatedAt = DateTime.Now
        };

        user.PasswordHash = UserPasswordService.Hash(user, data.Password);
        _db.Users.Add(user);
        _db.SaveChanges();

        _db.UserRoles.Add(new UserRole
        {
            UserId = user.UserId,
            RoleId = data.SelectedRoleId
        });
        _db.SaveChanges();

        TempData["UserSuccess"] = $"เพิ่มบัญชี staff {user.Email} เรียบร้อยแล้ว";
        return RedirectToAction("AdminList");
    }

    // เปิดฟอร์มแก้ไขบัญชีพนักงานที่มีอยู่แล้ว
    public IActionResult EditAdmin(int id)
    {
        var user = _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefault(u => u.UserId == id);

        if (user == null || !user.UserRoles.Any())
        {
            return RedirectToAction("AdminList");
        }

        var vm = new AdminUserFormViewModel
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status ?? "active",
            SelectedRoleId = user.UserRoles.OrderByDescending(ur => ur.RoleId).Select(ur => ur.RoleId).FirstOrDefault(),
            RoleOptions = GetStaffRoleOptions()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // บันทึกการแก้ไขบัญชีพนักงานและป้องกันการลดระดับ role ที่ไม่ถูกต้อง
    public IActionResult EditAdmin(AdminUserFormViewModel data)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var user = _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefault(u => u.UserId == data.UserId);

        if (user == null)
        {
            return RedirectToAction("AdminList");
        }

        if (!_db.Roles.Any(r => r.RoleId == data.SelectedRoleId && (r.RoleName == "admin" || r.RoleName == "superadmin")))
        {
            ViewBag.Error = "Role ที่เลือกไม่ถูกต้อง";
            data.RoleOptions = GetStaffRoleOptions();
            return View(data);
        }

        if (_db.Users.Any(u => u.UserId != user.UserId && u.Email == data.Email))
        {
            ViewBag.Error = "Email นี้มีในระบบแล้ว";
            data.RoleOptions = GetStaffRoleOptions();
            return View(data);
        }

        if (IsLastSuperAdmin(user.UserId) && !IsSuperAdminRoleId(data.SelectedRoleId))
        {
            ViewBag.Error = "ไม่สามารถลดสิทธิ์ superadmin คนสุดท้ายได้";
            data.RoleOptions = GetStaffRoleOptions();
            return View(data);
        }

        user.FirstName = string.IsNullOrWhiteSpace(data.FirstName) ? "Admin" : data.FirstName;
        user.LastName = string.IsNullOrWhiteSpace(data.LastName) ? string.Empty : data.LastName;
        user.Email = data.Email.Trim();
        user.PhoneNumber = data.PhoneNumber;
        user.Status = string.IsNullOrWhiteSpace(data.Status) ? "active" : data.Status;

        _db.UserRoles.RemoveRange(user.UserRoles);
        _db.UserRoles.Add(new UserRole
        {
            UserId = user.UserId,
            RoleId = data.SelectedRoleId
        });
        _db.SaveChanges();

        if (currentUserId == user.UserId)
        {
            var updatedRole = _db.Roles.Where(r => r.RoleId == data.SelectedRoleId).Select(r => r.RoleName).FirstOrDefault() ?? "user";
            HttpContext.Session.SetString("UserRole", updatedRole);
        }

        TempData["UserSuccess"] = $"อัปเดตบัญชี staff {user.Email} เรียบร้อยแล้ว";
        return RedirectToAction("AdminList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบบัญชีพนักงานโดยป้องกันการลบตัวเองและการลบ superadmin คนสุดท้าย
    public IActionResult DeleteAdmin(int id)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        if (currentUserId == id)
        {
            TempData["UserError"] = "ไม่สามารถลบบัญชีของตัวเองได้";
            return RedirectToAction("AdminList");
        }

        var user = _db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefault(u => u.UserId == id);

        if (user == null)
        {
            TempData["UserError"] = "ไม่พบบัญชี staff ที่ต้องการลบ";
            return RedirectToAction("AdminList");
        }

        if (IsLastSuperAdmin(user.UserId))
        {
            TempData["UserError"] = "ไม่สามารถลบ superadmin คนสุดท้ายได้";
            return RedirectToAction("AdminList");
        }

        _db.UserRoles.RemoveRange(user.UserRoles);
        _db.Users.Remove(user);
        _db.SaveChanges();

        TempData["UserSuccess"] = $"ลบบัญชี staff {user.Email} เรียบร้อยแล้ว";
        return RedirectToAction("AdminList");
    }

    // สร้างตาราง role-permission สำหรับจัดการสิทธิ์การเข้าถึงหลังบ้าน
    public IActionResult Permissions()
    {
        var roles = _db.Roles
            .ToList()
            .OrderByDescending(r => BackOfficeAccessHelper.GetRolePriority(r.RoleName))
            .ToList();
        var permissions = _db.Permissions
            .OrderBy(p => p.PermissionName)
            .ToList()
            .Select(p => new PermissionColumnViewModel
            {
                PermissionId = p.PermissionId,
                PermissionName = p.PermissionName ?? string.Empty,
                DisplayName = BuildPermissionLabel(p.PermissionName)
            })
            .ToList();
        var assigned = _db.RolePermissions.ToList();

        var vm = new RolePermissionMatrixViewModel
        {
            Permissions = permissions,
            Roles = roles.Select(role => new RolePermissionRowViewModel
            {
                RoleId = role.RoleId,
                RoleName = role.RoleName,
                SelectedPermissionIds = assigned
                    .Where(rp => rp.RoleId == role.RoleId && rp.PermissionId.HasValue)
                    .Select(rp => rp.PermissionId!.Value)
                    .ToList(),
                AvailablePermissions = permissions
                    .Where(permission => IsPermissionVisibleForRole(role.RoleName, permission.PermissionName))
                    .ToList(),
                EmptyStateMessage = GetEmptyStateMessage(role.RoleName)
            })
            .Where(role => role.AvailablePermissions.Any())
            .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // แทนที่การกำหนด permission ของแต่ละ role ตามข้อมูลที่ส่งมาจากฟอร์มตาราง
    public IActionResult SaveRolePermissions(RolePermissionBulkUpdateViewModel data)
    {
        var submittedRoles = data.Roles
            .Where(role => role.RoleId > 0)
            .ToList();

        if (!submittedRoles.Any())
        {
            return RedirectToAction("Permissions");
        }

        var allPermissionIds = _db.Permissions.Select(p => p.PermissionId).ToList();
        var permissions = _db.Permissions
            .Where(p => p.PermissionName != null)
            .Select(p => new
            {
                p.PermissionId,
                PermissionName = p.PermissionName!
            })
            .ToList();

        foreach (var submittedRole in submittedRoles)
        {
            var role = _db.Roles.FirstOrDefault(r => r.RoleId == submittedRole.RoleId);
            if (role == null)
            {
                continue;
            }

            var visiblePermissionIds = permissions
                .Where(p => IsPermissionVisibleForRole(role.RoleName, p.PermissionName))
                .Select(p => p.PermissionId)
                .ToList();

            var effectivePermissionIds = role.RoleName == "superadmin"
                ? allPermissionIds
                : submittedRole.SelectedPermissionIds
                    .Distinct()
                    .Where(id => visiblePermissionIds.Contains(id))
                    .ToList();

            var existing = _db.RolePermissions.Where(rp => rp.RoleId == role.RoleId).ToList();
            _db.RolePermissions.RemoveRange(existing);

            foreach (var permissionId in effectivePermissionIds)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.RoleId,
                    PermissionId = permissionId
                });
            }
        }

        _db.SaveChanges();
        TempData["PermissionSuccess"] = "บันทึก permission ของทุก role เรียบร้อยแล้ว";
        return RedirectToAction("Permissions");
    }

    // แปลงข้อมูลผู้ใช้ฝั่งพนักงานให้เป็น view model สำหรับหน้าจอ admin และ superadmin
    private AdminViewModel MapAdminUser(User user)
    {
        return new AdminViewModel
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            Roles = user.UserRoles
                .Select(ur => ur.Role.RoleName)
                .OrderByDescending(BackOfficeAccessHelper.GetRolePriority)
                .ToList()
        };
    }

    // สร้างข้อมูลโปรไฟล์หลังบ้านแบบเต็มพร้อม permission และข้อมูลที่อยู่
    private BackOfficeProfileViewModel BuildBackOfficeProfile(User user)
    {
        var defaultAddress = user.Addresses.OrderByDescending(a => a.IsDefault == true).FirstOrDefault();
        List<string> permissions;

        if (user.UserRoles.Any(ur => ur.Role.RoleName == "superadmin"))
        {
            permissions = _db.Permissions
                .OrderBy(p => p.PermissionName)
                .Select(p => BuildPermissionLabel(p.PermissionName))
                .ToList();
        }
        else
        {
            var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
            permissions = _db.RolePermissions
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleId.HasValue && roleIds.Contains(rp.RoleId.Value) && rp.Permission != null)
                .Select(rp => rp.Permission!.PermissionName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .OrderBy(name => name)
                .AsEnumerable()
                .Select(BuildPermissionLabel)
                .ToList();
        }

        return new BackOfficeProfileViewModel
        {
            Profile = MapAdminUser(user),
            DefaultAddressFull = BuildAddressText(defaultAddress),
            DefaultAddressPreview = BuildShortAddress(defaultAddress),
            AddressCount = user.Addresses.Count,
            Addresses = user.Addresses
                .OrderByDescending(a => a.IsDefault == true)
                .ThenByDescending(a => a.AddressId)
                .Select(a => new AddressItemViewModel
                {
                    AddressId = a.AddressId,
                    AddressLine = a.AddressLine ?? string.Empty,
                    City = a.City,
                    Province = a.Province,
                    PostalCode = a.PostalCode,
                    IsDefault = a.IsDefault ?? false
                })
                .ToList(),
            Permissions = permissions
        };
    }

    // แปลง order ให้เป็นการ์ดสรุปแบบย่อเมื่อหน้าหลังบ้านต้องใช้ข้อมูลคำสั่งซื้อ
    private static ProfileRecentOrderViewModel MapRecentOrder(Order order)
    {
        var items = order.OrderItems.ToList();
        var firstItem = items.FirstOrDefault();
        var imageUrl = firstItem?.Product?.ProductImages
            ?.OrderByDescending(i => i.IsMain == true)
            .Select(i => i.ImageUrl)
            .FirstOrDefault() ?? string.Empty;

        var itemSummary = firstItem?.Product?.Name ?? "-";
        if (items.Count > 1)
        {
            itemSummary += $" +{items.Count - 1} more";
        }

        return new ProfileRecentOrderViewModel
        {
            OrderId = order.OrderId,
            OrderNumber = order.OrderNumber ?? $"ORD{order.OrderId}",
            CreatedAt = order.CreatedAt,
            Status = order.Status ?? "pending",
            FinalAmount = order.FinalAmount ?? 0,
            ThumbnailUrl = imageUrl,
            ItemSummary = itemSummary,
            ItemCount = items.Count
        };
    }

    // แปลงข้อมูลที่อยู่ให้เป็นข้อความสำหรับแสดงผลอย่างปลอดภัย
    private static string BuildAddressText(Address? address)
    {
        if (address == null)
        {
            return "-";
        }

        return string.IsNullOrWhiteSpace(address.AddressLine) ? "-" : address.AddressLine.Trim();
    }

    // ย่อข้อความที่อยู่ให้สั้นลงสำหรับบล็อก UI แบบย่อ
    private static string BuildShortAddress(Address? address)
    {
        var fullAddress = BuildAddressText(address);
        if (fullAddress.Length <= 60)
        {
            return fullAddress;
        }

        return fullAddress[..57] + "...";
    }

    // คืนรายการ role ของพนักงานที่สามารถเลือกได้จากฟอร์มเพิ่มหรือแก้ไข
    private List<RoleOptionViewModel> GetStaffRoleOptions()
    {
        return _db.Roles
            .ToList()
            .Where(r => r.RoleName == "admin" || r.RoleName == "superadmin")
            .OrderByDescending(r => BackOfficeAccessHelper.GetRolePriority(r.RoleName))
            .Select(r => new RoleOptionViewModel
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName
            })
            .ToList();
    }

    // ค้นหา role id จากชื่อ role
    private int GetRoleIdByName(string roleName)
    {
        return _db.Roles.Where(r => r.RoleName == roleName).Select(r => r.RoleId).FirstOrDefault();
    }

    // ตรวจว่าผู้ใช้ที่เลือกเป็น superadmin คนสุดท้ายของระบบหรือไม่
    private bool IsLastSuperAdmin(int userId)
    {
        if (!HasRole(userId, "superadmin"))
        {
            return false;
        }

        var superAdminCount = _db.UserRoles.Include(ur => ur.Role).Count(ur => ur.Role.RoleName == "superadmin");
        return superAdminCount <= 1;
    }

    // ตรวจว่า role id นี้เป็น role ของ superadmin หรือไม่
    private bool IsSuperAdminRoleId(int roleId)
    {
        return _db.Roles.Any(r => r.RoleId == roleId && r.RoleName == "superadmin");
    }

    // ตรวจว่าผู้ใช้มี role ที่ระบุอยู่ในปัจจุบันหรือไม่
    private bool HasRole(int userId, string roleName)
    {
        return _db.UserRoles.Include(ur => ur.Role).Any(ur => ur.UserId == userId && ur.Role.RoleName == roleName);
    }

    // แปลงคีย์ของ permission ให้เป็นชื่อที่คนอ่านเข้าใจง่ายใน UI
    private static string BuildPermissionLabel(string? permissionName)
    {
        return (permissionName ?? string.Empty) switch
        {
            "dashboard.view" => "ดูแดชบอร์ดหลังบ้าน",
            "products.manage" => "จัดการสินค้า",
            "stock.manage" => "จัดการสต็อก",
            "banners.manage" => "จัดการแบนเนอร์",
            "promotions.manage" => "จัดการโปรโมชัน",
            "brands.manage" => "จัดการแบรนด์",
            "orders.view" => "ดูรายการสั่งซื้อ",
            "orders.manage" => "จัดการการชำระเงินและการจัดส่ง",
            "customers.manage" => "จัดการลูกค้า",
            "admins.manage" => "จัดการบัญชีแอดมิน",
            "permissions.manage" => "จัดการสิทธิ์การเข้าถึง",
            "superadmin.dashboard" => "เข้าใช้งาน SuperAdmin Console",
            _ => permissionName ?? string.Empty
        };
    }

    // ตัดสินใจว่าควรแสดง permission ใดสำหรับ role นั้นในตารางหรือไม่
    private static bool IsPermissionVisibleForRole(string? roleName, string permissionName)
    {
        var normalizedRole = (roleName ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedRole switch
        {
            "superadmin" => true,
            "admin" => AdminVisiblePermissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase),
            "user" => UserVisiblePermissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase),
            _ => false
        };
    }

    // ส่งข้อความสถานะว่างเมื่อ role นั้นไม่มี permission ที่ตั้งค่าได้ในตาราง
    private static string GetEmptyStateMessage(string? roleName)
    {
        return string.Equals(roleName, "user", StringComparison.OrdinalIgnoreCase)
            ? "role user ไม่มีสิทธิ์หลังบ้านให้ตั้งค่าในหน้านี้"
            : "ไม่มีสิทธิ์ที่เกี่ยวข้องให้ตั้งค่า";
    }
}
