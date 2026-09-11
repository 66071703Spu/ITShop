using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITShop.Helpers;
using ITShop.Models;
using ITShop.ViewModels;
using System.Linq;

namespace ITShop.Controllers;

public class AccountController : Controller
{
    private readonly Csi402dbContext _db;

    public AccountController(Csi402dbContext db)
    {
        _db = db;
    }

    // 🔹 หน้าเข้าสู่ระบบ
    // เปิดหน้าล็อกอิน
    public IActionResult Login()
    {
        return View();
    }

    // 🔹 ตรวจสอบการเข้าสู่ระบบ
    [HttpPost]
    [ValidateAntiForgeryToken]
    // ตรวจสอบข้อมูลเข้าสู่ระบบ เก็บค่าใน session และพาไปหน้าตาม role หลักของผู้ใช้
    public IActionResult Login(LoginViewModel data)
    {
        if (string.IsNullOrWhiteSpace(data.Email) || string.IsNullOrWhiteSpace(data.Password))
        {
            ViewBag.Error = "กรุณากรอก Email และ Password";
            return View(data);
        }

        var user = _db.Users
            .FirstOrDefault(u => u.Email == data.Email && u.PasswordHash == data.Password);

        if (user == null)
        {
            ViewBag.Error = "Email หรือ Password ไม่ถูกต้อง";
            return View(data);
        }

        // เก็บ session แบบง่าย ๆ
        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("UserEmail", user.Email);
        var primaryRole = _db.UserRoles
            .Where(ur => ur.UserId == user.UserId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.RoleId, (_, role) => role.RoleName)
            .ToList()
            .OrderByDescending(GetRolePriority)
            .FirstOrDefault() ?? "user";
        HttpContext.Session.SetString("UserRole", primaryRole);

        if (string.Equals(primaryRole, "superadmin", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Dashboard", "SuperAdmin");
        }

        if (string.Equals(primaryRole, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        return RedirectToAction("Index", "Home");
    }

    // 🔹 หน้าสมัครสมาชิก
    // เปิดหน้าสมัครสมาชิกและโหลดข้อมูล invite จาก query string ถ้ามี
    public IActionResult Signup(string? invite = null)
    {
        var viewModel = new SignupViewModel
        {
            InviteCode = InvitePromotionHelper.NormalizeInviteCode(invite)
        };

        if (viewModel.InviteCode != null)
        {
            PopulateInviteSignupContext(viewModel);
        }

        return View(viewModel);
    }

    // 🔹 ลงทะเบียนผู้ใช้
    [HttpPost]
    [ValidateAntiForgeryToken]
    // สร้างบัญชีผู้ใช้ใหม่ กำหนด role เริ่มต้น ที่อยู่ และความเชื่อมโยงกับ invite ถ้ามี
    public IActionResult Register(SignupViewModel data)
    {
        data.InviteCode = InvitePromotionHelper.NormalizeInviteCode(data.InviteCode);

        if (string.IsNullOrWhiteSpace(data.FirstName) ||
            string.IsNullOrWhiteSpace(data.Email) ||
            string.IsNullOrWhiteSpace(data.PasswordHash))
        {
            PopulateInviteSignupContext(data);
            ViewBag.Error = "กรุณากรอก First Name, Email และ Password";
            return View("Signup", data);
        }

        var checkEmail = _db.Users.FirstOrDefault(u => u.Email == data.Email);
        if (checkEmail != null)
        {
            PopulateInviteSignupContext(data);
            ViewBag.Error = "Email นี้มีในระบบแล้ว";
            return View("Signup", data);
        }

        InvitePromotionHelper.InviteSignupContext? inviteContext = null;
        if (data.InviteCode != null)
        {
            inviteContext = InvitePromotionHelper.GetValidInviteSignupContext(_db, data.InviteCode, DateTime.Now);
            if (inviteContext == null)
            {
                PopulateInviteSignupContext(data);
                ViewBag.Error = "Invite code หรือลิงก์เชิญนี้ไม่ถูกต้องหรือถูกใช้งานไปแล้ว";
                return View("Signup", data);
            }
        }

        var user = new User
        {
            FirstName = data.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(data.LastName) ? "-" : data.LastName.Trim(),
            Email = data.Email.Trim(),
            PasswordHash = data.PasswordHash, // โปรเจกต์นี้เก็บแบบง่ายก่อน
            PhoneNumber = data.PhoneNumber,
            Status = "active",
            CreatedAt = DateTime.Now
        };

        _db.Users.Add(user);
        _db.SaveChanges();

        if (!string.IsNullOrWhiteSpace(data.Address))
        {
            var address = new Address
            {
                UserId = user.UserId,
                AddressLine = data.Address,
                IsDefault = true
            };

            _db.Addresses.Add(address);
            _db.SaveChanges();
        }

        var userRole = new UserRole
        {
            UserId = user.UserId,
            RoleId = 1
        };

        _db.UserRoles.Add(userRole);

        InvitePromotionHelper.EnsurePersistentInviteCode(_db, user, saveChanges: false);

        if (inviteContext != null)
        {
            _db.UserInvites.Add(new UserInvite
            {
                InviterUserId = inviteContext.InviterUser.UserId,
                PromotionId = inviteContext.Promotion.PromotionId,
                InviteCode = inviteContext.InviteCode,
                InvitedEmail = user.Email,
                InvitedUserId = user.UserId,
                Status = "registered",
                RewardGiven = false,
                CreatedAt = DateTime.Now
            });
        }

        _db.SaveChanges();

        TempData["Success"] = "สมัครสมาชิกสำเร็จแล้ว กรุณาเข้าสู่ระบบ";
        return RedirectToAction("Login");
    }

    // 🔹 หน้าโปรไฟล์
    // สร้างข้อมูลหน้าโปรไฟล์ลูกค้าพร้อมคำสั่งซื้อล่าสุด คูปอง ที่อยู่ และประวัติ invite
    public IActionResult Profile()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login");
        }

        var user = _db.Users
            .Include(u => u.Addresses)
            .Include(u => u.CouponRedemptions)
                .ThenInclude(redemption => redemption.Coupon)
            .Include(u => u.UserInviteInviterUsers)
                .ThenInclude(invite => invite.Promotion)
                    .ThenInclude(promotion => promotion!.Products)
            .Include(u => u.UserInviteInviterUsers)
                .ThenInclude(invite => invite.RewardCoupon)
            .FirstOrDefault(u => u.UserId == userId.Value);

        if (user == null)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        var inviteCode = InvitePromotionHelper.EnsurePersistentInviteCode(_db, user);

        var defaultAddress = user.Addresses
            .OrderByDescending(a => a.IsDefault == true)
            .FirstOrDefault();

        var orders = _db.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImages)
            .Include(o => o.Payments)
            .Where(o => o.UserId == userId.Value)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToList();

        var couponPromotionNames = _db.Promotions
            .Where(p => p.CouponId.HasValue)
            .Select(p => new { p.CouponId, p.Name, p.DiscountType, p.DiscountValue })
            .ToList()
            .Where(p => p.CouponId.HasValue)
            .GroupBy(p => p.CouponId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.First());

        var couponHistory = user.CouponRedemptions
            .OrderByDescending(redemption => redemption.UsedAt ?? DateTime.MinValue)
            .Select(redemption =>
            {
                couponPromotionNames.TryGetValue(redemption.CouponId, out var promotionMeta);
                var linkedOrder = orders.FirstOrDefault(order => order.OrderId == redemption.OrderId);

                return new CouponHistoryItemViewModel
                {
                    Code = redemption.Coupon?.Code ?? "-",
                    PromotionName = promotionMeta?.Name ?? "Coupon Promotion",
                    DiscountLabel = PromotionPriceCalculator.BuildDiscountLabel(promotionMeta?.DiscountType, promotionMeta?.DiscountValue) ?? "-",
                    OrderNumber = linkedOrder?.OrderNumber ?? "-",
                    OrderFinalAmount = linkedOrder?.FinalAmount ?? 0m,
                    UsedAt = redemption.UsedAt
                };
            })
            .ToList();

        var availableCoupons = CouponPromotionHelper.GetVisibleCoupons(_db, user.UserId);
        var activeInvitePromotion = InvitePromotionHelper.GetPrimaryActiveInvitePromotion(_db);
        var referralHistory = user.UserInviteInviterUsers
            .Where(invite => invite.InvitedUserId.HasValue)
            .OrderByDescending(invite => invite.CreatedAt)
            .Select(invite => new ProfileInviteLinkViewModel
            {
                InviteCode = invite.InviteCode,
                PromotionName = invite.Promotion?.Name ?? "Invite Promotion",
                Status = invite.Status,
                InvitedEmail = invite.InvitedEmail,
                RewardCouponCode = invite.RewardCoupon?.Code,
                CreatedAt = invite.CreatedAt,
                RewardedAt = invite.RewardedAt
            })
            .ToList();

        var vm = new ProfileDashboardViewModel
        {
            Profile = new SignupViewModel
            {
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                Address = defaultAddress?.AddressLine
            },
            DefaultAddressFull = BuildAddressText(defaultAddress),
            DefaultAddressPreview = BuildShortAddress(defaultAddress),
            AddressCount = user.Addresses.Count,
            TotalOrders = _db.Orders.Count(o => o.UserId == userId.Value),
            ActiveOrders = _db.Orders.Count(o => o.UserId == userId.Value && o.Status != "delivered" && o.Status != "cancelled"),
            LifetimeSpent = _db.Orders.Where(o => o.UserId == userId.Value).Sum(o => o.FinalAmount ?? 0),
            RecentOrders = orders.Select(MapRecentOrder).ToList(),
            AvailableCoupons = availableCoupons,
            CouponHistory = couponHistory,
            InviteOverview = new ProfileInviteOverviewViewModel
            {
                InviteCode = inviteCode,
                InviteLink = InvitePromotionHelper.BuildInviteLink(Request, inviteCode),
                PromotionName = activeInvitePromotion?.Name ?? "Invite Program",
                RewardLabel = PromotionPriceCalculator.BuildDiscountLabel(activeInvitePromotion?.DiscountType, activeInvitePromotion?.DiscountValue) ?? "-",
                ApplicableProductSummary = activeInvitePromotion == null
                    ? "ยังไม่มี invite promotion ที่เปิดใช้งาน"
                    : InvitePromotionHelper.BuildInviteProductSummary(activeInvitePromotion),
                MinimumOrderSubtotal = activeInvitePromotion?.MinimumOrderSubtotal,
                IsRewardActive = activeInvitePromotion != null
            },
            ReferralHistory = referralHistory
        };

        return View(vm);
    }

    // เปิดฟอร์มแก้ไขโปรไฟล์ของผู้ใช้ปัจจุบัน
    public IActionResult EditProfile()
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        var vm = new EditProfileViewModel
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // บันทึกการแก้ไขโปรไฟล์หลังตรวจข้อมูลที่จำเป็นและอีเมลซ้ำ
    public IActionResult EditProfile(EditProfileViewModel data)
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        if (!ModelState.IsValid)
        {
            return View(data);
        }

        var normalizedEmail = data.Email.Trim();
        var duplicateEmail = _db.Users.Any(u => u.UserId != user.UserId && u.Email == normalizedEmail);
        if (duplicateEmail)
        {
            ModelState.AddModelError(nameof(data.Email), "Email นี้มีในระบบแล้ว");
            return View(data);
        }

        user.FirstName = data.FirstName.Trim();
        user.LastName = string.IsNullOrWhiteSpace(data.LastName) ? "-" : data.LastName.Trim();
        user.Email = normalizedEmail;
        user.PhoneNumber = string.IsNullOrWhiteSpace(data.PhoneNumber) ? null : data.PhoneNumber.Trim();

        _db.SaveChanges();
        HttpContext.Session.SetString("UserEmail", user.Email);
        TempData["Success"] = "อัปเดตข้อมูลส่วนตัวเรียบร้อยแล้ว";
        return RedirectToAction("Profile");
    }

    // แสดงที่อยู่ทั้งหมดที่ผู้ใช้ปัจจุบันบันทึกไว้
    public IActionResult ManageAddresses()
    {
        var user = GetCurrentUser(includeAddresses: true);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        var vm = new ManageAddressesViewModel
        {
            Addresses = user.Addresses
                .OrderByDescending(a => a.IsDefault == true)
                .ThenBy(a => a.AddressId)
                .Select(a => new AddressItemViewModel
                {
                    AddressId = a.AddressId,
                    AddressLine = a.AddressLine ?? string.Empty,
                    City = a.City,
                    Province = a.Province,
                    PostalCode = a.PostalCode,
                    IsDefault = a.IsDefault == true
                })
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // เพิ่มที่อยู่ใหม่และสามารถตั้งให้เป็นที่อยู่เริ่มต้นได้
    public IActionResult AddAddress(AddressInputViewModel data)
    {
        var user = GetCurrentUser(includeAddresses: true);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        if (!ModelState.IsValid)
        {
            TempData["AddressError"] = "กรุณากรอกที่อยู่หลักให้ครบ";
            return RedirectToAction("ManageAddresses");
        }

        var makeDefault = data.IsDefault || !user.Addresses.Any();
        if (makeDefault)
        {
            foreach (var item in user.Addresses)
            {
                item.IsDefault = false;
            }
        }

        _db.Addresses.Add(new Address
        {
            UserId = user.UserId,
            AddressLine = data.AddressLine.Trim(),
            City = null,
            Province = null,
            PostalCode = null,
            IsDefault = makeDefault
        });

        _db.SaveChanges();
        TempData["AddressSuccess"] = "เพิ่มที่อยู่เรียบร้อยแล้ว";
        return RedirectToAction("ManageAddresses");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ตั้งที่อยู่ที่เลือกให้เป็นที่อยู่จัดส่งเริ่มต้น
    public IActionResult SetDefaultAddress(int id)
    {
        var user = GetCurrentUser(includeAddresses: true);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        var address = user.Addresses.FirstOrDefault(a => a.AddressId == id);
        if (address == null)
        {
            TempData["AddressError"] = "ไม่พบที่อยู่ที่ต้องการตั้งค่า";
            return RedirectToAction("ManageAddresses");
        }

        foreach (var item in user.Addresses)
        {
            item.IsDefault = item.AddressId == id;
        }

        _db.SaveChanges();
        TempData["AddressSuccess"] = "ตั้งค่าที่อยู่เริ่มต้นเรียบร้อยแล้ว";
        return RedirectToAction("ManageAddresses");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบที่อยู่ที่บันทึกไว้และเลื่อนที่อยู่อื่นขึ้นมาแทนถ้าลบตัวที่เป็นค่าเริ่มต้น
    public IActionResult DeleteAddress(int id)
    {
        var user = GetCurrentUser(includeAddresses: true);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        var address = user.Addresses.FirstOrDefault(a => a.AddressId == id);
        if (address == null)
        {
            TempData["AddressError"] = "ไม่พบที่อยู่ที่ต้องการลบ";
            return RedirectToAction("ManageAddresses");
        }

        var wasDefault = address.IsDefault == true;
        _db.Addresses.Remove(address);
        _db.SaveChanges();

        if (wasDefault)
        {
            var nextAddress = _db.Addresses.FirstOrDefault(a => a.UserId == user.UserId);
            if (nextAddress != null)
            {
                nextAddress.IsDefault = true;
                _db.SaveChanges();
            }
        }

        TempData["AddressSuccess"] = "ลบที่อยู่เรียบร้อยแล้ว";
        return RedirectToAction("ManageAddresses");
    }

    // เปิดฟอร์มเปลี่ยนรหัสผ่านสำหรับผู้ใช้ที่ล็อกอินอยู่
    public IActionResult ChangePassword()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return RedirectToAction("Login");
        }

        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // เปลี่ยนรหัสผ่านหลังตรวจรหัสเดิมและช่องยืนยันรหัสผ่าน
    public IActionResult ChangePassword(ChangePasswordViewModel data)
    {
        var user = GetCurrentUser();
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        if (!ModelState.IsValid)
        {
            return View(data);
        }

        if (user.PasswordHash != data.CurrentPassword)
        {
            ModelState.AddModelError(nameof(data.CurrentPassword), "รหัสผ่านปัจจุบันไม่ถูกต้อง");
            return View(data);
        }

        if (data.NewPassword != data.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(data.ConfirmPassword), "ยืนยันรหัสผ่านใหม่ไม่ตรงกัน");
            return View(data);
        }

        user.PasswordHash = data.NewPassword;
        _db.SaveChanges();

        TempData["Success"] = "เปลี่ยนรหัสผ่านเรียบร้อยแล้ว";
        return RedirectToAction("Profile");
    }

    // 🔹 หน้าลืมรหัสผ่าน
    // เปิดหน้าตั้งรหัสผ่านใหม่แบบง่าย
    public IActionResult ForgotPassword()
    {
        return View();
    }

    // 🔹 ตั้งรหัสผ่านใหม่แบบง่ายในระบบ
    [HttpPost]
    [ValidateAntiForgeryToken]
    // ตั้งรหัสผ่านใหม่จากอีเมลหลังตรวจข้อมูลรหัสผ่านใหม่
    public IActionResult ForgotPassword(ForgotPasswordViewModel data)
    {
        if (string.IsNullOrWhiteSpace(data.Email) ||
            string.IsNullOrWhiteSpace(data.NewPassword) ||
            string.IsNullOrWhiteSpace(data.ConfirmPassword))
        {
            ViewBag.Error = "กรุณากรอกข้อมูลให้ครบ";
            return View(data);
        }

        if (data.NewPassword != data.ConfirmPassword)
        {
            ViewBag.Error = "ยืนยันรหัสผ่านไม่ตรงกัน";
            return View(data);
        }

        var user = _db.Users.FirstOrDefault(u => u.Email == data.Email);
        if (user == null)
        {
            ViewBag.Error = "ไม่พบบัญชีผู้ใช้นี้";
            return View(data);
        }

        user.PasswordHash = data.NewPassword;
        _db.SaveChanges();

        TempData["Success"] = "รีเซ็ตรหัสผ่านเรียบร้อยแล้ว";
        return RedirectToAction("Login");
    }

    // 🔹 ออกจากระบบ
    // ล้าง session และออกจากระบบของผู้ใช้ปัจจุบัน
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    // 🔹 รายชื่อผู้ใช้ (เหมือนที่คุณเคยทำ)
    // ส่งต่อไปยังหน้ารายชื่อลูกค้าที่ดูแลโดย AdminController
    public IActionResult Userlist()
    {
        return RedirectToAction("Userlist", "Admin");
    }

    // 🔹 ลบผู้ใช้
    // ลบบัญชีผู้ใช้จากฝั่งแอดมินเมื่อบัญชีนั้นยังไม่มีประวัติคำสั่งซื้อ
    public IActionResult Delete(string email)
    {
        var user = _db.Users
            .Include(u => u.Addresses)
            .Include(u => u.AuditLogs)
            .Include(u => u.Carts)
                .ThenInclude(c => c.CartItems)
            .Include(u => u.CouponRedemptions)
            .Include(u => u.Orders)
            .Include(u => u.UserInviteInvitedUsers)
            .Include(u => u.UserInviteInviterUsers)
            .Include(u => u.UserRoles)
            .FirstOrDefault(u => u.Email == email);

        if (user == null)
        {
            TempData["UserError"] = "ไม่พบผู้ใช้ที่ต้องการลบ";
            return RedirectToAction("Userlist", "Admin");
        }

        if (user.Orders.Any())
        {
            TempData["UserError"] = $"ไม่สามารถลบผู้ใช้ {user.Email} ได้ เพราะมีประวัติคำสั่งซื้อแล้ว";
            return RedirectToAction("Userlist", "Admin");
        }

        var inviteRecords = user.UserInviteInvitedUsers
            .Concat(user.UserInviteInviterUsers)
            .GroupBy(i => i.InviteId)
            .Select(g => g.First())
            .ToList();

        var cartItems = user.Carts
            .SelectMany(c => c.CartItems)
            .ToList();

        _db.CartItems.RemoveRange(cartItems);
        _db.Addresses.RemoveRange(user.Addresses);
        _db.AuditLogs.RemoveRange(user.AuditLogs);
        _db.CouponRedemptions.RemoveRange(user.CouponRedemptions);
        _db.UserInvites.RemoveRange(inviteRecords);
        _db.UserRoles.RemoveRange(user.UserRoles);
        _db.Carts.RemoveRange(user.Carts);
        _db.Users.Remove(user);
        _db.SaveChanges();

        TempData["UserSuccess"] = $"ลบผู้ใช้ {user.Email} เรียบร้อยแล้ว";

        return RedirectToAction("Userlist", "Admin");
    }

    // โหลดข้อมูลผู้ใช้ปัจจุบันจาก session และเลือกโหลดที่อยู่เพิ่มได้
    private User? GetCurrentUser(bool includeAddresses = false)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return null;
        }

        IQueryable<User> query = _db.Users;
        if (includeAddresses)
        {
            query = query.Include(u => u.Addresses);
        }

        return query.FirstOrDefault(u => u.UserId == userId.Value);
    }

    // เติมข้อมูลที่เกี่ยวกับ invite ลงในฟอร์มสมัครเมื่อมี invite code ส่งมา
    private void PopulateInviteSignupContext(SignupViewModel data)
    {
        var normalizedInviteCode = InvitePromotionHelper.NormalizeInviteCode(data.InviteCode);
        data.InviteCode = normalizedInviteCode;

        if (normalizedInviteCode == null)
        {
            data.InviterName = null;
            data.InvitePromotionName = null;
            return;
        }

        var inviteContext = InvitePromotionHelper.GetValidInviteSignupContext(_db, normalizedInviteCode, DateTime.Now);
        if (inviteContext == null)
        {
            ViewBag.InviteWarning = "Invite code หรือลิงก์เชิญนี้ไม่ถูกต้องหรือยังไม่มี invite promotion ที่เปิดใช้งาน";
            data.InviterName = null;
            data.InvitePromotionName = null;
            return;
        }

        data.InviterName = string.Join(' ', new[] { inviteContext.InviterUser.FirstName, inviteContext.InviterUser.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value) && value != "-"));
        data.InvitePromotionName = inviteContext.Promotion.Name;
    }

    // แปลงข้อมูล order ให้เป็นการ์ดสรุปแบบย่อสำหรับหน้าโปรไฟล์
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

    // แปลงข้อมูลที่อยู่ให้เป็นข้อความบรรทัดเดียวสำหรับแสดงผล
    private static string BuildAddressText(Address? address)
    {
        if (address == null)
        {
            return "-";
        }

        return string.IsNullOrWhiteSpace(address.AddressLine) ? "-" : address.AddressLine.Trim();
    }

    // ตัดข้อความที่อยู่ให้สั้นลงสำหรับแสดงใน dashboard แบบย่อ
    private static string BuildShortAddress(Address? address)
    {
        var fullAddress = BuildAddressText(address);
        if (fullAddress.Length <= 60)
        {
            return fullAddress;
        }

        return fullAddress[..57] + "...";
    }

    // คืนค่าลำดับความสำคัญสำหรับใช้แสดง role เมื่อผู้ใช้มีหลาย role
    private static int GetRolePriority(string? roleName)
    {
        return (roleName ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "superadmin" => 3,
            "admin" => 2,
            _ => 1
        };
    }
}