using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITShop.Helpers;
using ITShop.Models;
using ITShop.ViewModels;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace ITShop.Controllers;

public class AccountController : Controller
{
    private readonly Csi402dbContext _db;
    private readonly PasswordResetEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;

    public AccountController(Csi402dbContext db, PasswordResetEmailSender emailSender, ILogger<AccountController> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
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

        var user = _db.Users.FirstOrDefault(u => u.Email == data.Email.Trim());

        if (user == null || !UserPasswordService.Verify(user, data.Password)
            || string.Equals(user.Status, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.Error = "Email หรือ Password ไม่ถูกต้อง";
            return View(data);
        }

        if (!UserPasswordService.IsHashed(user.PasswordHash))
        {
            user.PasswordHash = UserPasswordService.Hash(user, data.Password);
            _db.SaveChanges();
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
        data.Email = data.Email?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(data.FirstName) ||
            string.IsNullOrWhiteSpace(data.Email) ||
            string.IsNullOrWhiteSpace(data.PasswordHash))
        {
            PopulateInviteSignupContext(data);
            ViewBag.Error = "กรุณากรอก First Name, Email และ Password";
            return View("Signup", data);
        }

        var checkEmail = _db.Users.FirstOrDefault(u => u.Email.ToLower() == data.Email.ToLower());
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
            Email = data.Email,
            PasswordHash = string.Empty,
            PhoneNumber = data.PhoneNumber,
            Status = "active",
            CreatedAt = DateTime.Now
        };

        user.PasswordHash = UserPasswordService.Hash(user, data.PasswordHash);

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

        if (!UserPasswordService.Verify(user, data.CurrentPassword))
        {
            ModelState.AddModelError(nameof(data.CurrentPassword), "รหัสผ่านปัจจุบันไม่ถูกต้อง");
            return View(data);
        }

        if (data.NewPassword != data.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(data.ConfirmPassword), "ยืนยันรหัสผ่านใหม่ไม่ตรงกัน");
            return View(data);
        }

        user.PasswordHash = UserPasswordService.Hash(user, data.NewPassword);
        _db.SaveChanges();

        TempData["Success"] = "เปลี่ยนรหัสผ่านเรียบร้อยแล้ว";
        return RedirectToAction("Profile");
    }

    // ขอส่งลิงก์รีเซ็ตรหัสผ่านไปยังอีเมลของบัญชี
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgotPassword(ForgotPasswordViewModel data)
    {
        if (!ModelState.IsValid)
        {
            return View(data);
        }

        if (!_emailSender.IsConfigured)
        {
            ViewBag.Error = "ระบบส่งอีเมลยังไม่พร้อมใช้งาน กรุณาติดต่อผู้ดูแลระบบ";
            return View(data);
        }

        var email = data.Email.Trim();
        var user = _db.Users.FirstOrDefault(u => u.Email == email);
        if (user != null)
        {
            var now = DateTime.UtcNow;
            var recentlyRequested = _db.PasswordResets.Any(reset =>
                reset.Email == email && reset.CreatedAt > now.AddMinutes(-2));
            if (!recentlyRequested)
            {
                var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
                var reset = new PasswordReset
                {
                    Email = email,
                    TokenHash = HashResetToken(token),
                    CreatedAt = now,
                    ExpiresAt = now.AddMinutes(30)
                };
                _db.PasswordResets.Add(reset);
                _db.SaveChanges();

                try
                {
                    _emailSender.Send(email, _emailSender.CreateResetUrl(token));
                }
                catch (Exception exception)
                {
                    _db.PasswordResets.Remove(reset);
                    _db.SaveChanges();
                    _logger.LogError(exception, "Could not send a password reset email.");
                    ViewBag.Error = "ส่งอีเมลไม่สำเร็จ กรุณาลองใหม่ภายหลัง";
                    return View(data);
                }
            }
        }

        ViewBag.Success = "หากอีเมลนี้มีบัญชีในระบบ เราได้ส่งลิงก์ตั้งรหัสผ่านใหม่แล้ว";
        return View(new ForgotPasswordViewModel());
    }

    [HttpGet]
    public IActionResult ResetPassword(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || FindValidReset(token) == null)
        {
            return View("ResetPasswordInvalid");
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResetPassword(ResetPasswordViewModel data)
    {
        if (!ModelState.IsValid)
        {
            return View(data);
        }

        if (data.NewPassword != data.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(data.ConfirmPassword), "ยืนยันรหัสผ่านใหม่ไม่ตรงกัน");
            return View(data);
        }

        using var transaction = _db.Database.BeginTransaction();
        var reset = FindValidReset(data.Token);
        if (reset == null)
        {
            return View("ResetPasswordInvalid");
        }

        var updated = _db.Database.ExecuteSqlInterpolated($@"
            UPDATE password_resets SET used_at = {DateTime.UtcNow}
            WHERE reset_id = {reset.ResetId} AND used_at IS NULL AND expires_at > {DateTime.UtcNow};");
        if (updated != 1)
        {
            transaction.Rollback();
            return View("ResetPasswordInvalid");
        }

        var user = _db.Users.FirstOrDefault(account => account.Email == reset.Email);
        if (user == null)
        {
            transaction.Rollback();
            return View("ResetPasswordInvalid");
        }

        user.PasswordHash = UserPasswordService.Hash(user, data.NewPassword);
        _db.PasswordResets
            .Where(other => other.Email == reset.Email && other.UsedAt == null)
            .ExecuteUpdate(setters => setters.SetProperty(other => other.UsedAt, DateTime.UtcNow));
        _db.SaveChanges();
        transaction.Commit();

        HttpContext.Session.Clear();
        TempData["Success"] = "ตั้งรหัสผ่านใหม่เรียบร้อยแล้ว กรุณาเข้าสู่ระบบ";
        return RedirectToAction(nameof(Login));
    }

    private PasswordReset? FindValidReset(string token)
    {
        if (token.Length > 128)
        {
            return null;
        }

        var hash = HashResetToken(token);
        return _db.PasswordResets.FirstOrDefault(reset =>
            reset.TokenHash == hash && reset.UsedAt == null && reset.ExpiresAt > DateTime.UtcNow);
    }

    private static string HashResetToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

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
