using ITShop.Models;
using ITShop.ViewModels;
using ITShop.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ITShop.Controllers;

public class AdminController : Controller
{
    private static readonly Dictionary<string, string> ActionPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dashboard"] = "dashboard.view",
        ["ProductList"] = "products.manage",
        ["AddProduct"] = "products.manage",
        ["EditProduct"] = "products.manage",
        ["DeleteProduct"] = "products.manage",
        ["ManageStock"] = "stock.manage",
        ["UpdateStock"] = "stock.manage",
        ["BannerList"] = "banners.manage",
        ["AddBanner"] = "banners.manage",
        ["EditBanner"] = "banners.manage",
        ["DeleteBanner"] = "banners.manage",
        ["PromotionList"] = "promotions.manage",
        ["AddPromotion"] = "promotions.manage",
        ["EditPromotion"] = "promotions.manage",
        ["DeletePromotion"] = "promotions.manage",
        ["BrandList"] = "brands.manage",
        ["AddBrand"] = "brands.manage",
        ["EditBrand"] = "brands.manage",
        ["DeleteBrand"] = "brands.manage",
        ["DeleteBrandLogo"] = "brands.manage",
        ["OrderList"] = "orders.view",
        ["EditShipment"] = "orders.manage",
        ["ConfirmPayment"] = "orders.manage",
        ["Userlist"] = "customers.manage",
        ["CustomerProfile"] = "customers.manage",
        ["Adduser"] = "customers.manage",
        ["Edituser"] = "customers.manage",
        ["Deleteuser"] = "customers.manage"
    };

    private static readonly string[] ShippingProviderOptions =
    {
        "Thailand Post",
        "Flash Express",
        "Kerry Express"
    };

    private static readonly string[] ShippingStatusOptions =
    {
        "pending",
        "shipped",
        "in_transit",
        "delivered"
    };

    private readonly Csi402dbContext _db;
    private readonly IWebHostEnvironment _env;

    public AdminController(Csi402dbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // จำกัดการใช้งานหลังบ้านให้เฉพาะ role แอดมินและตรวจสิทธิ์ของแต่ละ action
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        var currentRole = BackOfficeAccessHelper.GetCurrentRole(HttpContext.Session);

        if (!userId.HasValue || !BackOfficeAccessHelper.IsAdminRole(currentRole))
        {
            TempData["AdminError"] = "กรุณาเข้าสู่ระบบด้วยบัญชีผู้ดูแลก่อน";
            context.Result = RedirectToAction("Login", "Account");
            return;
        }

        var actionName = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        if (ActionPermissions.TryGetValue(actionName, out var permissionName)
            && !BackOfficeAccessHelper.HasPermission(_db, currentRole, permissionName))
        {
            TempData["AdminError"] = "คุณไม่มีสิทธิ์เข้าถึงหน้านี้";
            context.Result = RedirectToAction("Dashboard");
            return;
        }

        ViewBag.CurrentAdminRole = currentRole;
        ViewBag.IsSuperAdmin = BackOfficeAccessHelper.IsSuperAdmin(currentRole);

        base.OnActionExecuting(context);
    }

    // เปลี่ยนเส้นทางจาก route หลังบ้านแบบเก่าไปยังหน้า dashboard
    public IActionResult Lab()
    {
        return RedirectToAction("Dashboard");
    }

    // แสดงหน้า dashboard ของแอดมินพร้อมตัวเลขสรุปและคำสั่งซื้อล่าสุด
    public IActionResult Dashboard()
    {
        var vm = new AdminDashboardViewModel
        {
            TotalUsers = _db.Users.Count(),
            TotalProducts = _db.Products.Count(),
            TotalOrders = _db.Orders.Count(),
            LowStockCount = _db.Products.Count(p => (p.Stock ?? 0) <= 5),
            RecentOrders = _db.Orders
                .Include(o => o.User)
                .Include(o => o.Payments)
                .Include(o => o.Shipments)
                    .ThenInclude(s => s.Address)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToList()
                .Select(MapAdminOrder)
                .ToList()
        };

        return View(vm);
    }

    // แสดงโปรไฟล์หลังบ้านของแอดมินที่กำลังใช้งานอยู่
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

    // แสดงรายการสินค้าทั้งหมดสำหรับจัดการแคตตาล็อก
    public IActionResult ProductList()
    {
        var products = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProductImages)
            .OrderByDescending(p => p.CreatedAt)
            .ToList()
            .Select(MapProduct)
            .ToList();

        return View(products);
    }

    // แสดงรายการแบนเนอร์ทั้งหมดที่ใช้ในหน้าร้าน
    public IActionResult BannerList()
    {
        var banners = _db.Banners
            .OrderBy(b => b.Position)
            .ThenBy(b => b.DisplayOrder)
            .ThenByDescending(b => b.CreatedAt)
            .ToList()
            .Select(MapBanner)
            .ToList();

        return View(banners);
    }

    // แสดงรายการโปรโมชันทั้งหมดพร้อมสรุปคูปองและสินค้าที่ผูกไว้
    public IActionResult PromotionList()
    {
        var now = DateTime.Now;
        var promotions = _db.Promotions
            .Include(p => p.Coupon)
                .ThenInclude(c => c!.CouponRedemptions)
            .Include(p => p.Products)
                .ThenInclude(p => p.Category)
            .OrderByDescending(p => p.StartDate ?? DateTime.MinValue)
            .ThenBy(p => p.Name)
            .ToList()
            .Select(p => MapPromotion(p, now))
            .ToList();

        return View(promotions);
    }

    // แสดงรายการแบรนด์ทั้งหมดพร้อมสรุปสินค้าที่เชื่อมโยงอยู่
    public IActionResult BrandList()
    {
        var brands = _db.Brands
            .Include(b => b.Products)
            .OrderBy(b => b.BrandName)
            .ToList()
            .Select(MapBrand)
            .ToList();

        return View(brands);
    }

    // เปิดฟอร์มเพิ่มแบรนด์
    public IActionResult AddBrand()
    {
        return View(new BrandViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // สร้างแบรนด์ใหม่และอัปโหลดโลโก้ได้ถ้ามีไฟล์ส่งมา
    public IActionResult AddBrand(BrandViewModel data)
    {
        var normalizedName = NormalizeBrandName(data.BrandName);
        if (normalizedName == null)
        {
            ViewBag.Error = "กรุณากรอกชื่อแบรนด์";
            return View(data);
        }

        if (_db.Brands.Any(b => b.BrandName.ToLower() == normalizedName.ToLower()))
        {
            ViewBag.Error = "ชื่อแบรนด์นี้มีอยู่แล้ว";
            return View(data);
        }

        if (!TryPrepareBrandLogoUrl(data, null, normalizedName, out var logoUrl, out var logoError))
        {
            ViewBag.Error = logoError;
            return View(data);
        }

        _db.Brands.Add(new Brand
        {
            BrandName = normalizedName,
            LogoUrl = logoUrl
        });
        _db.SaveChanges();

        TempData["BrandSuccess"] = $"เพิ่มแบรนด์ {normalizedName} เรียบร้อยแล้ว";
        return RedirectToAction("BrandList");
    }

    // เปิดฟอร์มแก้ไขแบรนด์ที่มีอยู่แล้ว
    public IActionResult EditBrand(int id)
    {
        var brand = _db.Brands
            .Include(b => b.Products)
            .FirstOrDefault(b => b.BrandId == id);

        if (brand == null)
        {
            return RedirectToAction("BrandList");
        }

        return View(MapBrand(brand));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // บันทึกรายละเอียดแบรนด์และเปลี่ยนโลโก้เมื่อมีการอัปโหลดไฟล์ใหม่
    public IActionResult EditBrand(BrandViewModel data)
    {
        var brand = _db.Brands.FirstOrDefault(b => b.BrandId == data.BrandId);
        if (brand == null)
        {
            return RedirectToAction("BrandList");
        }

        var normalizedName = NormalizeBrandName(data.BrandName);
        if (normalizedName == null)
        {
            ViewBag.Error = "กรุณากรอกชื่อแบรนด์";
            return View(data);
        }

        if (_db.Brands.Any(b => b.BrandId != data.BrandId && b.BrandName.ToLower() == normalizedName.ToLower()))
        {
            ViewBag.Error = "ชื่อแบรนด์นี้มีอยู่แล้ว";
            data.LogoUrl = brand.LogoUrl;
            return View(data);
        }

        if (!TryPrepareBrandLogoUrl(data, brand.LogoUrl, normalizedName, out var logoUrl, out var logoError))
        {
            ViewBag.Error = logoError;
            data.LogoUrl = brand.LogoUrl;
            return View(data);
        }

        brand.BrandName = normalizedName;
        brand.LogoUrl = logoUrl;
        _db.SaveChanges();

        TempData["BrandSuccess"] = $"อัปเดตแบรนด์ {normalizedName} เรียบร้อยแล้ว";
        return RedirectToAction("BrandList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบโลโก้ของแบรนด์ออกโดยไม่ลบตัวแบรนด์
    public IActionResult DeleteBrandLogo(int id)
    {
        var brand = _db.Brands.FirstOrDefault(b => b.BrandId == id);
        if (brand == null)
        {
            TempData["BrandError"] = "ไม่พบแบรนด์ที่ต้องการจัดการโลโก้";
            return RedirectToAction("BrandList");
        }

        if (string.IsNullOrWhiteSpace(brand.LogoUrl))
        {
            TempData["BrandError"] = $"แบรนด์ {brand.BrandName} ยังไม่มีโลโก้ให้ลบ";
            return RedirectToAction("EditBrand", new { id });
        }

        if (brand.LogoUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            DeleteUploadedFile(brand.LogoUrl);
        }

        brand.LogoUrl = null;
        _db.SaveChanges();

        TempData["BrandSuccess"] = $"ลบโลโก้ของแบรนด์ {brand.BrandName} เรียบร้อยแล้ว";
        return RedirectToAction("EditBrand", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบแบรนด์ได้เฉพาะเมื่อไม่มีสินค้าที่เปิดใช้งานอยู่พึ่งพาแบรนด์นี้
    public IActionResult DeleteBrand(int id)
    {
        var brand = _db.Brands
            .Include(b => b.Products)
            .FirstOrDefault(b => b.BrandId == id);

        if (brand == null)
        {
            TempData["BrandError"] = "ไม่พบแบรนด์ที่ต้องการลบ";
            return RedirectToAction("BrandList");
        }

        if (brand.Products.Any())
        {
            TempData["BrandError"] = $"ลบแบรนด์ {brand.BrandName} ไม่ได้ เพราะยังมีสินค้าใช้งานอยู่";
            return RedirectToAction("BrandList");
        }

        _db.Brands.Remove(brand);
        _db.SaveChanges();
        DeleteUploadedFile(brand.LogoUrl);

        TempData["BrandSuccess"] = $"ลบแบรนด์ {brand.BrandName} เรียบร้อยแล้ว";
        return RedirectToAction("BrandList");
    }

    // เปิดฟอร์มเพิ่มโปรโมชันพร้อมค่าเริ่มต้นที่เหมาะสม
    public IActionResult AddPromotion()
    {
        return View(new PromotionViewModel
        {
            PromotionType = "seasonal",
            BrandEventBrandId = null,
            DiscountType = "percent",
            DiscountValue = 0,
            MinimumOrderSubtotal = null,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(7),
            CouponUsageLimit = 1,
            ProductOptions = GetPromotionProductOptions(null)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // สร้างโปรโมชัน คูปองที่เกี่ยวข้อง และรายการสินค้าที่ผูกกับโปรโมชัน
    public IActionResult AddPromotion(PromotionViewModel data)
    {
        if (!TryValidatePromotion(data, null, out var errorMessage))
        {
            return PromotionFormView("AddPromotion", data, errorMessage);
        }

        var effectiveProductIds = ResolvePromotionProductIds(data);
        var promotionType = NormalizePromotionType(data.PromotionType);

        var promotion = new Promotion
        {
            Name = data.Name.Trim(),
            PromotionType = promotionType,
            DiscountType = NormalizeTextValue(data.DiscountType, "percent"),
            DiscountValue = data.DiscountValue ?? 0,
            MinimumOrderSubtotal = data.MinimumOrderSubtotal,
            StartDate = data.StartDate,
            EndDate = data.EndDate,
            IsAutoApply = (promotionType == "coupon" || promotionType == "invite") ? false : data.IsAutoApply
        };

        SyncPromotionCoupon(promotion, data);

        AttachProductsToPromotion(promotion, effectiveProductIds);

        _db.Promotions.Add(promotion);
        _db.SaveChanges();

        return RedirectToAction("PromotionList");
    }

    // เปิดฟอร์มแก้ไขโปรโมชันที่มีอยู่แล้ว
    public IActionResult EditPromotion(int id)
    {
        var promotion = _db.Promotions
            .Include(p => p.Coupon)
                .ThenInclude(c => c!.CouponRedemptions)
            .Include(p => p.Products)
                .ThenInclude(p => p.Category)
            .FirstOrDefault(p => p.PromotionId == id);

        if (promotion == null)
        {
            return RedirectToAction("PromotionList");
        }

        return View(MapPromotionForEdit(promotion));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // บันทึกกติกาโปรโมชัน การตั้งค่าคูปอง และสินค้าที่ผูกกับโปรโมชัน
    public IActionResult EditPromotion(PromotionViewModel data)
    {
        var promotion = _db.Promotions
            .Include(p => p.Coupon)
                .ThenInclude(c => c!.CouponRedemptions)
            .Include(p => p.Products)
            .FirstOrDefault(p => p.PromotionId == data.PromotionId);

        if (promotion == null)
        {
            return RedirectToAction("PromotionList");
        }

        if (!TryValidatePromotion(data, promotion, out var errorMessage))
        {
            return PromotionFormView("EditPromotion", data, errorMessage);
        }

        var effectiveProductIds = ResolvePromotionProductIds(data);
        var promotionType = NormalizePromotionType(data.PromotionType);

        promotion.Name = data.Name.Trim();
        promotion.PromotionType = promotionType;
        promotion.DiscountType = NormalizeTextValue(data.DiscountType, "percent");
        promotion.DiscountValue = data.DiscountValue ?? 0;
        promotion.MinimumOrderSubtotal = data.MinimumOrderSubtotal;
        promotion.StartDate = data.StartDate;
        promotion.EndDate = data.EndDate;
        promotion.IsAutoApply = (promotionType == "coupon" || promotionType == "invite") ? false : data.IsAutoApply;

        SyncPromotionCoupon(promotion, data);

        SyncPromotionProducts(promotion, effectiveProductIds);

        _db.Promotions.Update(promotion);
        _db.SaveChanges();

        return RedirectToAction("PromotionList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบโปรโมชันและลบคูปองที่ผูกอยู่ได้เมื่อคูปองยังไม่เคยถูกใช้
    public IActionResult DeletePromotion(int id)
    {
        var promotion = _db.Promotions
            .Include(p => p.Coupon)
                .ThenInclude(c => c!.CouponRedemptions)
            .Include(p => p.Products)
            .FirstOrDefault(p => p.PromotionId == id);

        if (promotion != null)
        {
            promotion.Products.Clear();

            if (promotion.Coupon != null && !promotion.Coupon.CouponRedemptions.Any())
            {
                _db.Coupons.Remove(promotion.Coupon);
            }

            _db.Promotions.Remove(promotion);
            _db.SaveChanges();
        }

        return RedirectToAction("PromotionList");
    }

    // เปิดฟอร์มเพิ่มแบนเนอร์พร้อมค่าเริ่มต้นของวันและลำดับแสดงผล
    public IActionResult AddBanner()
    {
        return View(new BannerViewModel
        {
            Position = "home_top",
            IsActive = true,
            DisplayOrder = 1,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddMonths(1)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // สร้างแบนเนอร์ใหม่สำหรับหน้าร้านและอัปโหลดรูปภาพ
    public IActionResult AddBanner(BannerViewModel data)
    {
        if (data.EndDate.HasValue && data.StartDate.HasValue && data.EndDate < data.StartDate)
        {
            ViewBag.Error = "วันสิ้นสุดต้องไม่น้อยกว่าวันเริ่มต้น";
            return View(data);
        }

        if (!TryPrepareBannerImageUrl(data, null, out var imageUrl, out var imageError))
        {
            ViewBag.Error = imageError;
            return View(data);
        }

        var banner = new Banner
        {
            Title = data.Title,
            ImageUrl = imageUrl!,
            Position = string.IsNullOrWhiteSpace(data.Position) ? "home_top" : data.Position,
            StartDate = data.StartDate,
            EndDate = data.EndDate,
            IsActive = data.IsActive,
            DisplayOrder = data.DisplayOrder,
            CreatedAt = DateTime.Now
        };

        _db.Banners.Add(banner);
        _db.SaveChanges();

        return RedirectToAction("BannerList");
    }

    // เปิดฟอร์มแก้ไขแบนเนอร์ที่มีอยู่แล้ว
    public IActionResult EditBanner(int id)
    {
        var banner = _db.Banners.FirstOrDefault(b => b.BannerId == id);
        if (banner == null)
        {
            return RedirectToAction("BannerList");
        }

        return View(MapBanner(banner));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // บันทึกช่วงเวลา ตำแหน่ง และรูปภาพของแบนเนอร์
    public IActionResult EditBanner(BannerViewModel data)
    {
        var banner = _db.Banners.FirstOrDefault(b => b.BannerId == data.BannerId);
        if (banner == null)
        {
            return RedirectToAction("BannerList");
        }

        if (data.EndDate.HasValue && data.StartDate.HasValue && data.EndDate < data.StartDate)
        {
            ViewBag.Error = "วันสิ้นสุดต้องไม่น้อยกว่าวันเริ่มต้น";
            data.ImageUrl = banner.ImageUrl;
            return View(data);
        }

        if (!TryPrepareBannerImageUrl(data, banner.ImageUrl, out var imageUrl, out var imageError))
        {
            ViewBag.Error = imageError;
            data.ImageUrl = banner.ImageUrl;
            return View(data);
        }

        var previousImageUrl = banner.ImageUrl;
        banner.Title = data.Title;
        banner.ImageUrl = imageUrl!;
        banner.Position = string.IsNullOrWhiteSpace(data.Position) ? "home_top" : data.Position;
        banner.StartDate = data.StartDate;
        banner.EndDate = data.EndDate;
        banner.IsActive = data.IsActive;
        banner.DisplayOrder = data.DisplayOrder;

        _db.Banners.Update(banner);
        _db.SaveChanges();
        if (!string.Equals(previousImageUrl, imageUrl, StringComparison.OrdinalIgnoreCase))
        {
            DeleteUploadedFile(previousImageUrl);
        }

        return RedirectToAction("BannerList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบแบนเนอร์ออกจากรายการของหน้าร้าน
    public IActionResult DeleteBanner(int id)
    {
        var banner = _db.Banners.FirstOrDefault(b => b.BannerId == id);
        if (banner != null)
        {
            _db.Banners.Remove(banner);
            _db.SaveChanges();
            DeleteUploadedFile(banner.ImageUrl);
        }

        return RedirectToAction("BannerList");
    }

    // เปิดฟอร์มเพิ่มสินค้าและเตรียมข้อมูลสำหรับ dropdown ที่เกี่ยวข้อง
    public IActionResult AddProduct()
    {
        PopulateProductFormOptions();
        return View(new ProductViewModel
        {
            Status = "active",
            IsActive = true,
            Stock = 0,
            PackageItemInputs = new List<PackageItemInputViewModel> { new() }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // สร้างสินค้า โครงสร้าง package ถ้ามี และรูปหลักของสินค้า
    public IActionResult AddProduct(ProductViewModel data)
    {
        var comsetCategoryId = GetComsetCategoryId();
        var isComsetProduct = data.IsPackageProduct;
        var normalizedPackageItems = NormalizePackageItems(data.PackageItemInputs);

        data.IsPackageProduct = isComsetProduct;
        data.PackageItemInputs = normalizedPackageItems.Any()
            ? normalizedPackageItems
            : isComsetProduct
                ? new List<PackageItemInputViewModel> { new() }
                : new List<PackageItemInputViewModel>();

        if (string.IsNullOrWhiteSpace(data.Name) || data.Price <= 0)
        {
            ViewBag.Error = "กรุณากรอกชื่อสินค้าและราคาที่ถูกต้อง";
            PopulateProductFormOptions();
            return View(data);
        }

        if (!isComsetProduct && comsetCategoryId.HasValue && data.CategoryId == comsetCategoryId.Value)
        {
            ViewBag.Error = "หากต้องการใช้หมวดหมู่ Comset กรุณาเปิดสวิตช์ Comset และกำหนดรายการสินค้าในเซต";
            PopulateProductFormOptions();
            return View(data);
        }

        if (isComsetProduct)
        {
            if (!comsetCategoryId.HasValue)
            {
                ViewBag.Error = "ไม่พบหมวดหมู่ Comset ในระบบ";
                PopulateProductFormOptions();
                return View(data);
            }

            if (!normalizedPackageItems.Any())
            {
                ViewBag.Error = "กรุณาเพิ่มสินค้าอย่างน้อย 1 รายการในเซต";
                PopulateProductFormOptions();
                return View(data);
            }

            var selectedProductIds = normalizedPackageItems
                .Where(item => item.ProductId.HasValue)
                .Select(item => item.ProductId!.Value)
                .Distinct()
                .ToList();

            var validProductIds = _db.Products
                .Include(p => p.Category)
                .Where(p => selectedProductIds.Contains(p.ProductId)
                    && (p.Category == null || p.Category.Name == null || p.Category.Name.ToLower() != "comset")
                    && (p.Status == null || p.Status.ToLower() != "inactive"))
                .Select(p => p.ProductId)
                .ToHashSet();

            if (selectedProductIds.Count != validProductIds.Count)
            {
                ViewBag.Error = "มีสินค้าบางรายการในเซตไม่ถูกต้อง หรือเป็น Comset ซ้อนกัน";
                PopulateProductFormOptions();
                return View(data);
            }

            data.CategoryId = comsetCategoryId.Value;
        }

        if (!TryPrepareImageUrl(data, null, out var imageUrl, out var imageError))
        {
            ViewBag.Error = imageError;
            PopulateProductFormOptions();
            return View(data);
        }

        var brandId = data.BrandId;

        var product = new Product
        {
            Name = data.Name.Trim(),
            Description = data.Description,
            Price = data.Price,
            Stock = data.Stock ?? 0,
            Sku = null,
            CategoryId = data.CategoryId,
            BrandId = brandId,
            Status = "active",
            CreatedAt = DateTime.Now
        };

        _db.Products.Add(product);
        _db.SaveChanges();

        product.Sku = NormalizeSkuValue(data.Sku, product.ProductId);
        _db.SaveChanges();

        if (isComsetProduct)
        {
            var package = new ProductPackage
            {
                PackageId = product.ProductId,
                Name = product.Name,
                Price = product.Price
            };

            _db.ProductPackages.Add(package);

            var packageItems = normalizedPackageItems
                .Where(item => item.ProductId.HasValue)
                .Select(item => new PackageItem
                {
                    PackageId = package.PackageId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                })
                .ToList();

            _db.PackageItems.AddRange(packageItems);
            _db.SaveChanges();
        }

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            _db.ProductImages.Add(new ProductImage
            {
                ProductId = product.ProductId,
                ImageUrl = imageUrl,
                IsMain = true
            });
            _db.SaveChanges();
        }

        return RedirectToAction("ProductList");
    }

    // เปิดฟอร์มแก้ไขสินค้าที่มีอยู่ในแคตตาล็อก
    public IActionResult EditProduct(int id)
    {
        var product = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.Promotions)
            .Include(p => p.OrderItems).ThenInclude(item => item.Order)
            .FirstOrDefault(p => p.ProductId == id);

        if (product == null)
        {
            return RedirectToAction("ProductList");
        }

        PopulateProductFormOptions();
        var viewModel = MapProduct(product);
        viewModel.IsPackageProduct = string.Equals(product.Category?.Name, "Comset", StringComparison.OrdinalIgnoreCase);
        viewModel.PackageItemInputs = GetPackageItemInputs(product.ProductId);

        if (viewModel.IsPackageProduct && !viewModel.PackageItemInputs.Any())
        {
            viewModel.PackageItemInputs.Add(new PackageItemInputViewModel());
        }

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // บันทึกการแก้ไขสินค้า โครงสร้าง package และรูปหลักของสินค้า
    public IActionResult EditProduct(ProductViewModel data)
    {
        var comsetCategoryId = GetComsetCategoryId();
        var isComsetProduct = data.IsPackageProduct;
        var normalizedPackageItems = NormalizePackageItems(data.PackageItemInputs);

        data.IsPackageProduct = isComsetProduct;
        data.PackageItemInputs = normalizedPackageItems.Any()
            ? normalizedPackageItems
            : isComsetProduct
                ? new List<PackageItemInputViewModel> { new() }
                : new List<PackageItemInputViewModel>();

        var product = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.Promotions)
            .FirstOrDefault(p => p.ProductId == data.ProductId);

        if (product == null)
        {
            return RedirectToAction("ProductList");
        }

        if (string.IsNullOrWhiteSpace(data.Name) || data.Price <= 0)
        {
            ViewBag.Error = "กรุณากรอกชื่อสินค้าและราคาที่ถูกต้อง";
            PopulateProductFormOptions();
            return View(data);
        }

        if (!isComsetProduct && comsetCategoryId.HasValue && data.CategoryId == comsetCategoryId.Value)
        {
            ViewBag.Error = "หากต้องการใช้หมวดหมู่ Comset กรุณาเปิดสวิตช์ Comset และกำหนดรายการสินค้าในเซต";
            PopulateProductFormOptions();
            return View(data);
        }

        if (isComsetProduct)
        {
            if (!comsetCategoryId.HasValue)
            {
                ViewBag.Error = "ไม่พบหมวดหมู่ Comset ในระบบ";
                PopulateProductFormOptions();
                return View(data);
            }

            if (!normalizedPackageItems.Any())
            {
                ViewBag.Error = "กรุณาเพิ่มสินค้าอย่างน้อย 1 รายการในเซต";
                PopulateProductFormOptions();
                return View(data);
            }

            var selectedProductIds = normalizedPackageItems
                .Where(item => item.ProductId.HasValue)
                .Select(item => item.ProductId!.Value)
                .Distinct()
                .ToList();

            var validProductIds = _db.Products
                .Include(p => p.Category)
                .Where(p => selectedProductIds.Contains(p.ProductId)
                    && p.ProductId != product.ProductId
                    && (p.Category == null || p.Category.Name == null || p.Category.Name.ToLower() != "comset")
                    && (p.Status == null || p.Status.ToLower() != "inactive"))
                .Select(p => p.ProductId)
                .ToHashSet();

            if (selectedProductIds.Count != validProductIds.Count)
            {
                ViewBag.Error = "มีสินค้าบางรายการในเซตไม่ถูกต้อง หรือเป็น Comset ซ้อนกัน";
                PopulateProductFormOptions();
                return View(data);
            }

            data.CategoryId = comsetCategoryId.Value;
        }

        var currentImageUrl = product.ProductImages
            .OrderByDescending(i => i.IsMain == true)
            .Select(i => i.ImageUrl)
            .FirstOrDefault();

        if (!TryPrepareImageUrl(data, currentImageUrl, out var imageUrl, out var imageError))
        {
            ViewBag.Error = imageError;
            PopulateProductFormOptions();
            return View(data);
        }

        var brandId = data.BrandId;

        product.Name = data.Name;
        product.Description = data.Description;
        product.Price = data.Price;
        product.Stock = data.Stock ?? 0;
        product.Sku = NormalizeSkuValue(data.Sku, product.ProductId);
        product.CategoryId = isComsetProduct && comsetCategoryId.HasValue ? comsetCategoryId.Value : data.CategoryId;
        product.BrandId = brandId;
        product.Status = data.IsActive ? "active" : "inactive";

        SyncProductPackage(product, isComsetProduct, normalizedPackageItems);

        var mainImage = product.ProductImages.FirstOrDefault(i => i.IsMain == true) ?? product.ProductImages.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (mainImage == null)
            {
                _db.ProductImages.Add(new ProductImage
                {
                    ProductId = product.ProductId,
                    ImageUrl = imageUrl,
                    IsMain = true
                });
            }
            else
            {
                mainImage.ImageUrl = imageUrl;
            }
        }

        _db.Products.Update(product);
        _db.SaveChanges();

        return RedirectToAction("ProductList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบสินค้าเมื่อยังไม่มีประวัติคำสั่งซื้อและล้างข้อมูลที่เกี่ยวข้อง
    public IActionResult DeleteProduct(int id)
    {
        var product = _db.Products
            .Include(p => p.CartItems)
            .Include(p => p.InventoryTransactions)
            .Include(p => p.OrderItems)
            .Include(p => p.PackageItems)
            .Include(p => p.ProductImages)
            .Include(p => p.Promotions)
            .FirstOrDefault(p => p.ProductId == id);

        if (product == null)
        {
            TempData["ProductError"] = "ไม่พบสินค้าที่ต้องการลบ";
            return RedirectToAction("ProductList");
        }

        if (product.OrderItems.Any())
        {
            TempData["ProductError"] = "สินค้านี้มีประวัติคำสั่งซื้อแล้ว จึงลบถาวรไม่ได้ กรุณาใช้สวิตช์ Active ในหน้าแก้ไขแทน";
            return RedirectToAction("ProductList");
        }

        foreach (var image in product.ProductImages.ToList())
        {
            DeleteUploadedFile(image.ImageUrl);
        }

        product.Promotions.Clear();
        RemoveProductPackage(product.ProductId);
        _db.CartItems.RemoveRange(product.CartItems);
        _db.InventoryTransactions.RemoveRange(product.InventoryTransactions);
        _db.PackageItems.RemoveRange(product.PackageItems);
        _db.ProductImages.RemoveRange(product.ProductImages);
        _db.Products.Remove(product);
        _db.SaveChanges();

        TempData["ProductSuccess"] = $"ลบสินค้า {product.Name} เรียบร้อยแล้ว";

        return RedirectToAction("ProductList");
    }

    // แสดงหน้าจัดการสต็อกของสินค้าทั้งหมด
    public IActionResult ManageStock()
    {
        var products = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProductImages)
            .OrderBy(p => p.Name)
            .ToList()
            .Select(MapProduct)
            .ToList();

        return View(products);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // อัปเดตสต็อกของสินค้าหนึ่งรายการพร้อมบันทึกประวัติการปรับคลัง
    public IActionResult UpdateStock(int productId, int stock)
    {
        var product = _db.Products.FirstOrDefault(p => p.ProductId == productId);
        if (product != null)
        {
            var oldStock = product.Stock ?? 0;
            product.Stock = stock < 0 ? 0 : stock;
            _db.Products.Update(product);

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                ProductId = product.ProductId,
                TransactionType = product.Stock >= oldStock ? "IN" : "ADJUST",
                Quantity = Math.Abs((product.Stock ?? 0) - oldStock),
                CreatedAt = DateTime.Now
            });

            _db.SaveChanges();
        }

        return RedirectToAction("ManageStock");
    }

    // แสดงรายการคำสั่งซื้อทั้งหมดสำหรับตรวจสอบในฝั่งหลังบ้าน
    public IActionResult OrderList()
    {
        var orders = _db.Orders
            .Include(o => o.User)
            .Include(o => o.Payments)
            .Include(o => o.Shipments)
                .ThenInclude(s => s.Address)
            .OrderByDescending(o => o.CreatedAt)
            .ToList()
            .Select(MapAdminOrder)
            .ToList();

        return View(orders);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmPayment(int orderId)
    {
        using var transaction = _db.Database.BeginTransaction();
        var order = _db.Orders
            .Include(value => value.Payments)
            .FirstOrDefault(value => value.OrderId == orderId);
        var payment = order?.Payments.OrderByDescending(value => value.PaymentId).FirstOrDefault();
        if (order == null || payment == null || order.Status == "cancelled"
            || payment.PaymentStatus != "pending")
        {
            TempData["OrderError"] = "คำสั่งซื้อนี้ไม่สามารถยืนยันการชำระเงินได้";
            return RedirectToAction(nameof(OrderList));
        }

        var paymentRows = _db.Database.ExecuteSqlInterpolated($@"
            UPDATE payments SET payment_status = 'paid', paid_at = {DateTime.Now}
            WHERE payment_id = {payment.PaymentId} AND payment_status = 'pending';");
        if (paymentRows != 1)
        {
            transaction.Rollback();
            TempData["OrderError"] = "รายการชำระเงินนี้ถูกยืนยันไปแล้ว";
            return RedirectToAction(nameof(OrderList));
        }

        payment.PaymentStatus = "paid";
        payment.PaidAt = DateTime.Now;
        if (order.Status == "pending")
        {
            order.Status = "paid";
            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                Status = "paid",
                ChangedAt = DateTime.Now
            });
        }

        _db.SaveChanges();
        transaction.Commit();
        TempData["OrderSuccess"] = $"ยืนยันการชำระเงินของ {order.OrderNumber} แล้ว";
        return RedirectToAction(nameof(OrderList));
    }

    // เปิดหน้าจอแก้ไขข้อมูลจัดส่งของ shipment ล่าสุดใน order
    public IActionResult EditShipment(int orderId)
    {
        var order = _db.Orders
            .Include(o => o.User)
            .Include(o => o.Shipments)
                .ThenInclude(s => s.Address)
            .FirstOrDefault(o => o.OrderId == orderId);

        if (order == null)
        {
            TempData["OrderError"] = "ไม่พบคำสั่งซื้อที่ต้องการ";
            return RedirectToAction("OrderList");
        }

        if (order.Status == "cancelled")
        {
            TempData["OrderError"] = "คำสั่งซื้อที่ยกเลิกแล้วไม่สามารถแก้ไข shipment ได้";
            return RedirectToAction("OrderList");
        }

        var shipment = order.Shipments
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        if (shipment == null)
        {
            TempData["OrderError"] = "คำสั่งซื้อนี้ยังไม่มีข้อมูล shipment ให้แก้ไข";
            return RedirectToAction("OrderList");
        }

        if (shipment.Status is "failed" or "returned")
        {
            TempData["OrderError"] = "shipment ที่สิ้นสุดแล้วไม่สามารถแก้ไขผ่านฟอร์มนี้ได้";
            return RedirectToAction("OrderList");
        }

        return View(BuildShipmentUpdateViewModel(order, shipment));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // บันทึกผู้ให้บริการขนส่ง สถานะ เลขติดตาม และเวลาที่เกี่ยวข้อง
    public IActionResult EditShipment(AdminShipmentUpdateViewModel data)
    {
        var shipment = _db.Shipments
            .Include(s => s.Order)
                .ThenInclude(o => o.User)
            .Include(s => s.Address)
            .FirstOrDefault(s => s.ShipmentId == data.ShipmentId && s.OrderId == data.OrderId);

        if (shipment?.Order == null)
        {
            TempData["OrderError"] = "ไม่พบ shipment ที่ต้องการอัปเดต";
            return RedirectToAction("OrderList");
        }

        if (shipment.Order.Status == "cancelled" || shipment.Status is "failed" or "returned")
        {
            TempData["OrderError"] = "shipment ของคำสั่งซื้อที่ยกเลิกแล้วไม่สามารถแก้ไขได้";
            return RedirectToAction("OrderList");
        }

        if (!ShippingProviderOptions.Contains(data.ShippingProvider, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(data.ShippingProvider), "ผู้จัดส่งที่เลือกไม่ถูกต้อง");
        }

        if (!ShippingStatusOptions.Contains(data.ShippingStatus, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(data.ShippingStatus), "สถานะ shipment ที่เลือกไม่ถูกต้อง");
        }

        var currentShippingStatus = shipment.Status.ToLowerInvariant();
        var nextShippingStatus = data.ShippingStatus?.ToLowerInvariant();
        if ((currentShippingStatus == "shipped" && nextShippingStatus is not ("shipped" or "in_transit" or "delivered"))
            || (currentShippingStatus == "in_transit" && nextShippingStatus is not ("in_transit" or "delivered"))
            || (currentShippingStatus == "delivered" && nextShippingStatus != "delivered"))
        {
            ModelState.AddModelError(nameof(data.ShippingStatus), "ไม่สามารถย้อนสถานะหลังจัดส่งแล้ว");
        }

        if (!ModelState.IsValid)
        {
            data.OrderNumber = shipment.Order.OrderNumber ?? $"ORD{shipment.Order.OrderId}";
            data.CustomerName = shipment.Order.User?.Email ?? "-";
            data.ShippingAddress = BuildAddressText(shipment.Address);
            data.ShippingProviderOptions = ShippingProviderOptions.ToList();
            data.ShippingStatusOptions = ShippingStatusOptions.ToList();
            return View(data);
        }

        using var transaction = _db.Database.BeginTransaction();
        var paymentIsPaid = _db.Payments.Any(payment => payment.OrderId == data.OrderId && payment.PaymentStatus == "paid");
        var nextOrderStatus = nextShippingStatus == "pending"
            ? paymentIsPaid ? "paid" : "pending"
            : nextShippingStatus!;
        if (shipment.Order.Status != nextOrderStatus)
        {
            var updatedRows = _db.Database.ExecuteSqlInterpolated($@"
                UPDATE orders SET status = {nextOrderStatus}
                WHERE order_id = {data.OrderId} AND (status IS NULL OR status <> 'cancelled');");
            if (updatedRows != 1)
            {
                transaction.Rollback();
                TempData["OrderError"] = "สถานะคำสั่งซื้อเปลี่ยนไปแล้ว กรุณาเปิดหน้าอีกครั้ง";
                return RedirectToAction("OrderList");
            }

            shipment.Order.Status = nextOrderStatus;
            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = data.OrderId,
                Status = nextOrderStatus,
                ChangedAt = DateTime.Now
            });
        }

        shipment.ShippingProvider = ShippingProviderOptions
            .First(option => string.Equals(option, data.ShippingProvider, StringComparison.OrdinalIgnoreCase));
        shipment.Status = ShippingStatusOptions
            .First(option => string.Equals(option, data.ShippingStatus, StringComparison.OrdinalIgnoreCase));
        shipment.TrackingNumber = string.IsNullOrWhiteSpace(data.TrackingNumber)
            ? null
            : data.TrackingNumber.Trim();

        ApplyShipmentTimestamps(shipment);

        _db.SaveChanges();
        transaction.Commit();

        TempData["OrderSuccess"] = $"อัปเดต shipment ของคำสั่งซื้อ {shipment.Order.OrderNumber ?? $"ORD{shipment.Order.OrderId}"} เรียบร้อยแล้ว";
        return RedirectToAction("OrderList");
    }

    // แสดงรายชื่อลูกค้าที่แอดมินมีสิทธิ์จัดการได้
    public IActionResult Userlist()
    {
        var users = _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == "user"))
            .OrderBy(u => u.CreatedAt)
            .ToList()
            .Select(u => new AdminViewModel
            {
                UserId = u.UserId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                Roles = u.UserRoles
                    .Select(ur => ur.Role.RoleName)
                    .OrderByDescending(BackOfficeAccessHelper.GetRolePriority)
                    .ToList()
            })
            .ToList();

        return View(users);
    }

    // แสดงหน้าโปรไฟล์หลังบ้านของลูกค้าหนึ่งบัญชี
    public IActionResult CustomerProfile(int id)
    {
        var user = _db.Users
            .Include(u => u.Addresses)
            .Include(u => u.CouponRedemptions)
                .ThenInclude(redemption => redemption.Coupon)
            .Include(u => u.UserInviteInviterUsers)
                .ThenInclude(invite => invite.Promotion)
            .Include(u => u.UserInviteInviterUsers)
                .ThenInclude(invite => invite.RewardCoupon)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefault(u => u.UserId == id);

        if (user == null || !HasRole(user.UserId, "user"))
        {
            TempData["UserError"] = "ไม่พบบัญชีลูกค้าที่ต้องการ";
            return RedirectToAction("Userlist");
        }

        var orders = _db.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.ProductImages)
            .Where(o => o.UserId == user.UserId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToList();

        var couponPromotionNames = _db.Promotions
            .Where(p => p.CouponId.HasValue)
            .Select(p => new { p.CouponId, p.Name, p.DiscountType, p.DiscountValue })
            .ToList()
            .Where(p => p.CouponId.HasValue)
            .GroupBy(p => p.CouponId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

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

        var defaultAddress = user.Addresses.OrderByDescending(a => a.IsDefault == true).FirstOrDefault();
        var availableCoupons = CouponPromotionHelper.GetVisibleCoupons(_db, user.UserId);
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

        var vm = new BackOfficeProfileViewModel
        {
            Profile = new AdminViewModel
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
            },
            DefaultAddressFull = BuildAddressText(defaultAddress),
            DefaultAddressPreview = BuildShortAddress(defaultAddress),
            AddressCount = user.Addresses.Count,
            TotalOrders = _db.Orders.Count(o => o.UserId == user.UserId),
            ActiveOrders = _db.Orders.Count(o => o.UserId == user.UserId && o.Status != "delivered" && o.Status != "cancelled"),
            LifetimeSpent = _db.Orders.Where(o => o.UserId == user.UserId).Sum(o => o.FinalAmount ?? 0),
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
            RecentOrders = orders.Select(MapRecentOrder).ToList(),
            AvailableCoupons = availableCoupons,
            CouponHistory = couponHistory,
            ReferralHistory = referralHistory
        };

        return View(vm);
    }

    // เปิดฟอร์มเพิ่มลูกค้า
    public IActionResult Adduser()
    {
        return View(new AdminUserFormViewModel
        {
            Status = "active",
            SelectedRoleId = 1,
            RoleOptions = GetCustomerRoleOptions()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // สร้างบัญชีลูกค้าใหม่และกำหนด role ผู้ใช้ให้
    public IActionResult Adduser(AdminUserFormViewModel data)
    {
        if (string.IsNullOrWhiteSpace(data.Email) || string.IsNullOrWhiteSpace(data.Password))
        {
            ViewBag.Error = "Email และ Password ห้ามว่าง";
            data.RoleOptions = GetCustomerRoleOptions();
            return View(data);
        }

        var normalizedEmail = data.Email.Trim();
        var checkEmail = _db.Users.FirstOrDefault(u => u.Email.ToLower() == normalizedEmail.ToLower());
        if (checkEmail != null)
        {
            ViewBag.Error = "Email นี้มีในระบบแล้ว";
            data.RoleOptions = GetCustomerRoleOptions();
            return View(data);
        }

        if (!_db.Roles.Any(r => r.RoleId == data.SelectedRoleId && r.RoleName == "user"))
        {
            ViewBag.Error = "Role ที่เลือกไม่ถูกต้อง";
            data.RoleOptions = GetCustomerRoleOptions();
            return View(data);
        }

        var user = new User();
        user.FirstName = string.IsNullOrWhiteSpace(data.FirstName) ? "User" : data.FirstName;
        user.LastName = string.IsNullOrWhiteSpace(data.LastName) ? "" : data.LastName;
        user.Email = normalizedEmail;
        user.PasswordHash = UserPasswordService.Hash(user, data.Password);
        user.PhoneNumber = data.PhoneNumber;
        user.Status = string.IsNullOrWhiteSpace(data.Status) ? "active" : data.Status;
        user.CreatedAt = DateTime.Now;

        _db.Users.Add(user);
        _db.SaveChanges();

        _db.UserRoles.Add(new UserRole
        {
            UserId = user.UserId,
            RoleId = data.SelectedRoleId
        });
        _db.SaveChanges();

        TempData["UserSuccess"] = $"เพิ่มผู้ใช้ {user.Email} เรียบร้อยแล้ว";

        return RedirectToAction("Userlist");
    }

    // เปิดฟอร์มแก้ไขข้อมูลลูกค้า
    public IActionResult Edituser(int id)
    {
        var user = _db.Users.FirstOrDefault(u => u.UserId == id);
        if (user == null)
        {
            return RedirectToAction("Userlist");
        }

        var selectedRoleId = _db.UserRoles
            .Where(ur => ur.UserId == user.UserId)
            .OrderByDescending(ur => ur.RoleId)
            .Select(ur => ur.RoleId)
            .FirstOrDefault();

        var data = new AdminUserFormViewModel
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status ?? "active",
            SelectedRoleId = selectedRoleId == 0 ? 1 : selectedRoleId,
            RoleOptions = GetCustomerRoleOptions()
        };

        return View("Useredit", data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // บันทึกการแก้ไขบัญชีลูกค้าโดยคง role ให้เป็นฝั่งลูกค้าเท่านั้น
    public IActionResult Edituser(AdminUserFormViewModel data)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        var user = _db.Users.FirstOrDefault(u => u.UserId == data.UserId);
        if (user == null)
        {
            return RedirectToAction("Userlist");
        }

        if (!_db.Roles.Any(r => r.RoleId == data.SelectedRoleId && r.RoleName == "user"))
        {
            ViewBag.Error = "Role ที่เลือกไม่ถูกต้อง";
            data.RoleOptions = GetCustomerRoleOptions();
            return View("Useredit", data);
        }

        if (!HasRole(user.UserId, "user"))
        {
            TempData["UserError"] = "admin จัดการได้เฉพาะบัญชีลูกค้าเท่านั้น";
            return RedirectToAction("Userlist");
        }

        var normalizedEmail = data.Email?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            ViewBag.Error = "กรุณากรอก Email";
            data.RoleOptions = GetCustomerRoleOptions();
            return View("Useredit", data);
        }

        if (_db.Users.Any(u => u.UserId != user.UserId && u.Email.ToLower() == normalizedEmail.ToLower()))
        {
            ViewBag.Error = "Email นี้มีในระบบแล้ว";
            data.RoleOptions = GetCustomerRoleOptions();
            return View("Useredit", data);
        }

        user.FirstName = string.IsNullOrWhiteSpace(data.FirstName) ? "User" : data.FirstName;
        user.LastName = string.IsNullOrWhiteSpace(data.LastName) ? "" : data.LastName;
        user.Email = normalizedEmail;
        user.PhoneNumber = data.PhoneNumber;
        user.Status = data.Status;

        _db.Users.Update(user);

        var existingRoles = _db.UserRoles.Where(ur => ur.UserId == user.UserId).ToList();
        _db.UserRoles.RemoveRange(existingRoles);
        _db.UserRoles.Add(new UserRole
        {
            UserId = user.UserId,
            RoleId = data.SelectedRoleId
        });

        _db.SaveChanges();

        if (currentUserId == user.UserId)
        {
            var updatedRole = _db.Roles
                .Where(r => r.RoleId == data.SelectedRoleId)
                .Select(r => r.RoleName)
                .FirstOrDefault() ?? "user";
            HttpContext.Session.SetString("UserRole", updatedRole);
        }

        TempData["UserSuccess"] = $"อัปเดตผู้ใช้ {user.Email} เรียบร้อยแล้ว";

        return RedirectToAction("Userlist");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // ลบบัญชีลูกค้าเมื่อยังไม่มีประวัติคำสั่งซื้อและสามารถลบได้อย่างปลอดภัย
    public IActionResult Deleteuser(int id)
    {
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        if (currentUserId == id)
        {
            TempData["UserError"] = "ไม่สามารถลบบัญชีของตัวเองได้";
            return RedirectToAction("Userlist");
        }

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
            .FirstOrDefault(u => u.UserId == id);

        if (user == null)
        {
            TempData["UserError"] = "ไม่พบผู้ใช้ที่ต้องการลบ";
            return RedirectToAction("Userlist");
        }

        if (user.Orders.Any())
        {
            TempData["UserError"] = $"ไม่สามารถลบผู้ใช้ {user.Email} ได้ เพราะมีประวัติคำสั่งซื้อแล้ว";
            return RedirectToAction("Userlist");
        }

        if (!HasRole(user.UserId, "user"))
        {
            TempData["UserError"] = "admin ลบได้เฉพาะบัญชีลูกค้าเท่านั้น";
            return RedirectToAction("Userlist");
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

        return RedirectToAction("Userlist");
    }

    // คืนค่าตัวเลือก role ที่อนุญาตให้ใช้ในฟอร์มจัดการลูกค้า
    private List<RoleOptionViewModel> GetCustomerRoleOptions()
    {
        return _db.Roles
            .Where(r => r.RoleName == "user")
            .Select(r => new RoleOptionViewModel
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName
            })
            .ToList();
    }

    // ตรวจว่าผู้ใช้มี role ที่ร้องขออยู่แล้วหรือไม่
    private bool HasRole(int userId, string roleName)
    {
        return _db.UserRoles
            .Include(ur => ur.Role)
            .Any(ur => ur.UserId == userId && ur.Role.RoleName == roleName);
    }

    // สร้างข้อมูลโปรไฟล์แอดมินพร้อมสิทธิ์และข้อมูลที่อยู่
    private BackOfficeProfileViewModel BuildBackOfficeProfile(User user)
    {
        var defaultAddress = user.Addresses.OrderByDescending(a => a.IsDefault == true).FirstOrDefault();
        var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();

        var permissions = _db.RolePermissions
            .Include(rp => rp.Permission)
            .Where(rp => rp.RoleId.HasValue && roleIds.Contains(rp.RoleId.Value) && rp.Permission != null)
            .Select(rp => rp.Permission!.PermissionName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(name => name)
            .AsEnumerable()
            .Select(BuildPermissionLabel)
            .ToList();

        return new BackOfficeProfileViewModel
        {
            Profile = new AdminViewModel
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
            },
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

    // แปลง order ให้เป็นการ์ดสรุปแบบย่อสำหรับหน้าโปรไฟล์
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

    // แปลง order ให้อยู่ในรูปแบบแบนสำหรับหน้า dashboard และรายการคำสั่งซื้อ
    private static AdminOrderViewModel MapAdminOrder(Order order)
    {
        var latestShipment = order.Shipments
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefault();

        return new AdminOrderViewModel
        {
            OrderId = order.OrderId,
            ShipmentId = latestShipment?.ShipmentId,
            OrderNumber = order.OrderNumber ?? $"ORD{order.OrderId}",
            CustomerName = order.User?.Email ?? "-",
            TotalAmount = order.FinalAmount ?? 0,
            Status = order.Status ?? "pending",
            PaymentMethod = order.Payments.OrderByDescending(p => p.PaidAt).Select(p => p.PaymentMethod).FirstOrDefault() ?? "-",
            PaymentStatus = order.Payments.OrderByDescending(p => p.PaidAt).Select(p => p.PaymentStatus).FirstOrDefault() ?? "-",
            ShippingProvider = latestShipment?.ShippingProvider ?? "-",
            ShippingStatus = order.Status == "cancelled" ? "cancelled" : latestShipment?.Status ?? "-",
            TrackingNumber = latestShipment?.TrackingNumber,
            ShippingAddress = BuildAddressText(latestShipment?.Address),
            CreatedAt = order.CreatedAt
        };
    }

    // สร้าง model ของฟอร์ม shipment จากข้อมูลปัจจุบันและรายการตัวเลือก
    private static AdminShipmentUpdateViewModel BuildShipmentUpdateViewModel(Order order, Shipment shipment)
    {
        return new AdminShipmentUpdateViewModel
        {
            OrderId = order.OrderId,
            ShipmentId = shipment.ShipmentId,
            OrderNumber = order.OrderNumber ?? $"ORD{order.OrderId}",
            CustomerName = order.User?.Email ?? "-",
            ShippingAddress = BuildAddressText(shipment.Address),
            ShippingProvider = shipment.ShippingProvider ?? ShippingProviderOptions[0],
            ShippingStatus = shipment.Status ?? ShippingStatusOptions[0],
            TrackingNumber = shipment.TrackingNumber,
            ShippingProviderOptions = ShippingProviderOptions.ToList(),
            ShippingStatusOptions = ShippingStatusOptions.ToList()
        };
    }

    // ตั้งค่าเวลาจัดส่งและเวลาส่งถึงให้ตรงกับสถานะ shipment ที่เลือก
    private static void ApplyShipmentTimestamps(Shipment shipment)
    {
        var status = (shipment.Status ?? string.Empty).Trim().ToLowerInvariant();
        var now = DateTime.Now;

        if (status == "pending")
        {
            shipment.ShippedAt = null;
            shipment.DeliveredAt = null;
            return;
        }

        if (status is "shipped" or "in_transit")
        {
            shipment.ShippedAt ??= now;
            shipment.DeliveredAt = null;
            return;
        }

        if (status == "delivered")
        {
            shipment.ShippedAt ??= now;
            shipment.DeliveredAt ??= now;
            return;
        }

        shipment.DeliveredAt = null;
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

    // ย่อข้อความที่อยู่ให้สั้นลงสำหรับ UI แบบย่อ
    private static string BuildShortAddress(Address? address)
    {
        var fullAddress = BuildAddressText(address);
        if (fullAddress.Length <= 60)
        {
            return fullAddress;
        }

        return fullAddress[..57] + "...";
    }

    // แปลงคีย์ของ permission ให้เป็นชื่อที่อ่านเข้าใจง่าย
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
            "customers.manage" => "จัดการลูกค้า",
            "admins.manage" => "จัดการบัญชีแอดมิน",
            "permissions.manage" => "จัดการสิทธิ์การเข้าถึง",
            "superadmin.dashboard" => "เข้าใช้งาน SuperAdmin Console",
            _ => permissionName ?? string.Empty
        };
    }

    // เตรียมหมวด แบรนด์ และรายการสินค้าประกอบสำหรับฟอร์มสินค้า
    private void PopulateProductFormOptions()
    {
        ViewBag.Categories = _db.Categories.OrderBy(c => c.Name).ToList();
        ViewBag.Brands = _db.Brands.OrderBy(b => b.BrandName).ToList();
        ViewBag.ComsetCategoryId = GetComsetCategoryId();
        ViewBag.ComponentProducts = _db.Products
            .Include(p => p.Category)
            .Where(p => (p.Category == null || p.Category.Name == null || p.Category.Name.ToLower() != "comset")
                && (p.Status == null || p.Status.ToLower() != "inactive"))
            .OrderBy(p => p.Name)
            .ToList();
    }

    // ค้นหา category id ที่ใช้กับสินค้าประเภท Comset
    private int? GetComsetCategoryId()
    {
        return _db.Categories
            .Where(c => c.Name.ToLower() == "comset")
            .Select(c => (int?)c.CategoryId)
            .FirstOrDefault();
    }

    // โหลด package item ที่บันทึกไว้เข้าไปในช่องกรอกของฟอร์มแก้ไข
    private List<PackageItemInputViewModel> GetPackageItemInputs(int productId)
    {
        return _db.PackageItems
            .Where(item => item.PackageId == productId)
            .OrderBy(item => item.Id)
            .Select(item => new PackageItemInputViewModel
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity ?? 1
            })
            .ToList();
    }

    // สร้าง แก้ไข หรือลบข้อมูล package ให้ตรงกับสถานะปัจจุบันของฟอร์มสินค้า
    private void SyncProductPackage(Product product, bool isComsetProduct, List<PackageItemInputViewModel> packageItems)
    {
        var package = _db.ProductPackages
            .Include(pp => pp.PackageItems)
            .FirstOrDefault(pp => pp.PackageId == product.ProductId);

        if (!isComsetProduct)
        {
            RemoveProductPackage(product.ProductId, package);
            return;
        }

        if (package == null)
        {
            package = new ProductPackage
            {
                PackageId = product.ProductId
            };

            _db.ProductPackages.Add(package);
        }
        else if (package.PackageItems.Any())
        {
            _db.PackageItems.RemoveRange(package.PackageItems);
        }

        package.Name = product.Name;
        package.Price = product.Price;

        var normalizedItems = packageItems
            .Where(item => item.ProductId.HasValue)
            .Select(item => new PackageItem
            {
                PackageId = product.ProductId,
                ProductId = item.ProductId,
                Quantity = item.Quantity
            })
            .ToList();

        if (normalizedItems.Any())
        {
            _db.PackageItems.AddRange(normalizedItems);
        }
    }

    // ลบ package เดิมและรายการย่อยเมื่อสินค้านั้นไม่ควรถูกมองเป็น package แล้ว
    private void RemoveProductPackage(int productId, ProductPackage? package = null)
    {
        package ??= _db.ProductPackages
            .Include(pp => pp.PackageItems)
            .FirstOrDefault(pp => pp.PackageId == productId);

        if (package == null)
        {
            return;
        }

        if (package.PackageItems.Any())
        {
            _db.PackageItems.RemoveRange(package.PackageItems);
        }

        _db.ProductPackages.Remove(package);
    }

    // ปรับข้อมูล package input โดยลบแถวว่างและบังคับจำนวนขั้นต่ำ
    private static List<PackageItemInputViewModel> NormalizePackageItems(IEnumerable<PackageItemInputViewModel>? packageItems)
    {
        return (packageItems ?? Enumerable.Empty<PackageItemInputViewModel>())
            .Where(item => item.ProductId.HasValue)
            .Select(item => new PackageItemInputViewModel
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity < 1 ? 1 : item.Quantity
            })
            .ToList();
    }

    // ตัดช่องว่างชื่อแบรนด์และแปลงค่าว่างให้เป็น null
    private static string? NormalizeBrandName(string? brandName)
    {
        return string.IsNullOrWhiteSpace(brandName) ? null : brandName.Trim();
    }

    // แปลงข้อมูลแบรนด์ให้อยู่ในรูปแบบ view model ที่ใช้ในหน้าจอแอดมิน
    private static BrandViewModel MapBrand(Brand brand)
    {
        var productNames = brand.Products
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToList();

        return new BrandViewModel
        {
            BrandId = brand.BrandId,
            BrandName = brand.BrandName,
            LogoUrl = brand.LogoUrl,
            ProductCount = productNames.Count,
            ProductSummary = productNames.Count == 0
                ? "-"
                : string.Join(", ", productNames.Take(3)) + (productNames.Count > 3 ? " ..." : string.Empty)
        };
    }

    // หา URL โลโก้ที่ถูกต้องของแบรนด์หลังจัดการไฟล์อัปโหลดแล้ว
    private bool TryPrepareBrandLogoUrl(BrandViewModel data, string? existingLogoUrl, string brandName, out string? logoUrl, out string? errorMessage)
    {
        if (TrySaveUploadedImage(data.LogoFile, "brands", $"{BrandLogoHelper.SlugifyBrandName(brandName)}_", out logoUrl, out errorMessage))
        {
            if (!string.IsNullOrWhiteSpace(existingLogoUrl) && existingLogoUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            {
                DeleteUploadedFile(existingLogoUrl);
            }

            return true;
        }

        if (data.LogoFile != null)
        {
            return false;
        }

        logoUrl = existingLogoUrl;
        errorMessage = null;
        return true;
    }

    // ทำให้สินค้าทุกตัวมีค่า SKU ในรูปแบบมาตรฐาน
    private static string NormalizeSkuValue(string? inputSku, int productId)
    {
        var trimmedSku = (inputSku ?? string.Empty).Trim().ToUpperInvariant();
        if (trimmedSku.StartsWith("SKU") && trimmedSku.Length >= 4 && trimmedSku.Skip(3).All(char.IsDigit))
        {
            return trimmedSku;
        }

        return $"SKU{productId:D3}";
    }

    // สร้างฟอร์มโปรโมชันใหม่พร้อม error validation และรายการสินค้าเวอร์ชันล่าสุด
    private IActionResult PromotionFormView(string viewName, PromotionViewModel data, string errorMessage)
    {
        ViewBag.Error = errorMessage;
        data.ProductOptions = GetPromotionProductOptions(data.ProductIds);
        return View(viewName, data);
    }

    // ตรวจ business rule ของโปรโมชันก่อนเริ่มสร้างหรือแก้ไขข้อมูล
    private bool TryValidatePromotion(PromotionViewModel data, Promotion? existingPromotion, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(data.Name))
        {
            errorMessage = "กรุณากรอกชื่อโปรโมชัน";
            return false;
        }

        if (data.EndDate.HasValue && data.StartDate.HasValue && data.EndDate < data.StartDate)
        {
            errorMessage = "วันสิ้นสุดต้องไม่น้อยกว่าวันเริ่มต้น";
            return false;
        }

        if ((data.DiscountValue ?? 0) < 0)
        {
            errorMessage = "ส่วนลดต้องมีค่าเท่ากับหรือมากกว่า 0";
            return false;
        }

        if (data.MinimumOrderSubtotal.HasValue && data.MinimumOrderSubtotal.Value < 0)
        {
            errorMessage = "ขั้นต่ำคำสั่งซื้อต้องมีค่าเท่ากับหรือมากกว่า 0";
            return false;
        }

        var discountType = NormalizeTextValue(data.DiscountType, "percent");
        if (discountType == "percent" && (data.DiscountValue ?? 0) > 100)
        {
            errorMessage = "ส่วนลดแบบเปอร์เซ็นต์ต้องไม่เกิน 100";
            return false;
        }

        var promotionType = NormalizePromotionType(data.PromotionType);
        if (promotionType == "coupon")
        {
            var normalizedCouponCode = NormalizeCouponCode(data.CouponCode);
            if (normalizedCouponCode == null)
            {
                errorMessage = "กรุณากรอก Coupon Code";
                return false;
            }

            if (data.CouponUsageLimit.HasValue && data.CouponUsageLimit.Value <= 0)
            {
                errorMessage = "Usage Limit ต้องมากกว่า 0 ถ้าระบุค่า";
                return false;
            }

            var currentCouponId = existingPromotion?.CouponId;
            var duplicateCoupon = _db.Coupons.Any(c =>
                c.Code != null
                && c.Code.ToLower() == normalizedCouponCode.ToLower()
                && (!currentCouponId.HasValue || c.CouponId != currentCouponId.Value));

            if (duplicateCoupon)
            {
                errorMessage = "Coupon Code นี้มีในระบบแล้ว";
                return false;
            }
        }

        if (promotionType == "brand_event")
        {
            if (!data.BrandEventBrandId.HasValue)
            {
                errorMessage = "กรุณาเลือกแบรนด์สำหรับ Brand Event";
                return false;
            }

            var hasProductsInBrand = _db.Products.Any(p =>
                p.BrandId == data.BrandEventBrandId.Value &&
                (p.Status ?? "active") != "inactive");

            if (!hasProductsInBrand)
            {
                errorMessage = "แบรนด์ที่เลือกยังไม่มีสินค้าที่ใช้งานได้สำหรับโปรโมชัน";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    // ตัดสินใจว่าโปรโมชันควรผูกกับ product id ใดบ้างตามประเภทโปรโมชัน
    private List<int> ResolvePromotionProductIds(PromotionViewModel data)
    {
        var promotionType = NormalizePromotionType(data.PromotionType);
        if (promotionType == "brand_event" && data.BrandEventBrandId.HasValue)
        {
            return _db.Products
                .Where(p => p.BrandId == data.BrandEventBrandId.Value && (p.Status ?? "active") != "inactive")
                .OrderBy(p => p.Name)
                .Select(p => p.ProductId)
                .ToList();
        }

        return data.ProductIds?
            .Distinct()
            .ToList() ?? new List<int>();
    }

    // สร้างรายการสินค้าที่ให้เลือกในฟอร์มโปรโมชัน
    private List<PromotionProductOptionViewModel> GetPromotionProductOptions(IEnumerable<int>? selectedProductIds)
    {
        var selectedIds = selectedProductIds?.ToHashSet() ?? new HashSet<int>();

        return _db.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Where(p => (p.Status ?? "active") != "inactive")
            .OrderBy(p => p.Name)
            .ToList()
            .Select(p => new PromotionProductOptionViewModel
            {
                ProductId = p.ProductId,
                Name = p.Name,
                CategoryName = p.Category?.Name,
                BrandId = p.BrandId,
                BrandName = p.Brand?.BrandName,
                Price = p.Price,
                IsSelected = selectedIds.Contains(p.ProductId)
            })
            .ToList();
    }

    // ผูกสินค้าที่เลือกเข้ากับโปรโมชันที่เพิ่งสร้างใหม่
    private void AttachProductsToPromotion(Promotion promotion, IEnumerable<int>? productIds)
    {
        var selectedIds = productIds?
            .Distinct()
            .ToList() ?? new List<int>();

        if (selectedIds.Count == 0)
        {
            return;
        }

        var selectedProducts = _db.Products
            .Where(p => selectedIds.Contains(p.ProductId))
            .ToList();

        foreach (var product in selectedProducts)
        {
            promotion.Products.Add(product);
        }
    }

    // ซิงก์ความเชื่อมโยงระหว่างโปรโมชันกับสินค้าในตอนแก้ไขโปรโมชัน
    private void SyncPromotionProducts(Promotion promotion, IEnumerable<int>? productIds)
    {
        var selectedIds = productIds?
            .Distinct()
            .ToHashSet() ?? new HashSet<int>();

        var productsToRemove = promotion.Products
            .Where(p => !selectedIds.Contains(p.ProductId))
            .ToList();

        foreach (var product in productsToRemove)
        {
            promotion.Products.Remove(product);
        }

        var currentIds = promotion.Products
            .Select(p => p.ProductId)
            .ToHashSet();

        var idsToAdd = selectedIds
            .Where(id => !currentIds.Contains(id))
            .ToList();

        if (idsToAdd.Count == 0)
        {
            return;
        }

        var productsToAdd = _db.Products
            .Where(p => idsToAdd.Contains(p.ProductId))
            .ToList();

        foreach (var product in productsToAdd)
        {
            promotion.Products.Add(product);
        }
    }

    // ตัดช่องว่างข้อความและใช้ค่า fallback เมื่อค่าเดิมว่าง
    private static string NormalizeTextValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    // แปลงชื่อประเภทโปรโมชันจาก UI ให้เป็นค่ามาตรฐานภายในระบบ
    private static string NormalizePromotionType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();

        return normalized switch
        {
            "invite" => "invite",
            "brand_event" => "brand_event",
            "coupon" => "coupon",
            "seasonal" => "seasonal",
            "manual" => "seasonal",
            "flash_sale" => "seasonal",
            "bundle" => "brand_event",
            "home" => "seasonal",
            _ => "seasonal"
        };
    }

    // คืนชื่อแสดงผลและคำอธิบายของประเภทโปรโมชัน
    private static (string Label, string Description) GetPromotionTypeMeta(string? promotionType)
    {
        var normalized = NormalizePromotionType(promotionType);

        return normalized switch
        {
            "invite" => (
                "Invite",
                "ใช้สร้าง invite link และแจก reward coupon ให้ผู้เชิญหลังเพื่อนสั่งซื้อครั้งแรก"
            ),
            "brand_event" => (
                "Brand Event",
                "โปรโมชันสำหรับสินค้าของแบรนด์เดียวกัน แนะนำให้เลือกสินค้าในแบรนด์เดียวกันผ่าน Brand filter"
            ),
            "coupon" => (
                "Coupon",
                "โค้ดส่วนลดที่ user ต้องนำไปกรอกใน cart หรือ checkout เพื่อรับส่วนลดตามสินค้าที่กำหนด"
            ),
            _ => (
                "Seasonal",
                "โปรโมชันตามช่วงเวลา เช่น 9.9 ปีใหม่ สงกรานต์ เหมาะกับส่วนลดตามวันที่เริ่มและสิ้นสุด"
            )
        };
    }

    // หา URL รูปแบนเนอร์ที่ถูกต้องหลังจัดการการอัปโหลดแล้ว
    private bool TryPrepareBannerImageUrl(BannerViewModel data, string? existingImageUrl, out string? imageUrl, out string? errorMessage)
    {
        if (TrySaveUploadedImage(data.ImageFile, "banners", "banner_", out imageUrl, out errorMessage))
        {
            return true;
        }

        if (data.ImageFile != null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(existingImageUrl))
        {
            imageUrl = null;
            errorMessage = "กรุณาอัปโหลดรูป Banner";
            return false;
        }

        imageUrl = existingImageUrl;
        errorMessage = null;
        return true;
    }

    // หา URL รูปสินค้าที่ถูกต้องหลังจัดการการอัปโหลดแล้ว
    private bool TryPrepareImageUrl(ProductViewModel data, string? existingImageUrl, out string? imageUrl, out string? errorMessage)
    {
        if (TrySaveUploadedImage(data.ImageFile, "products", null, out imageUrl, out errorMessage))
        {
            return true;
        }

        if (data.ImageFile != null)
        {
            return false;
        }

        imageUrl = existingImageUrl;
        errorMessage = null;
        return true;
    }

    // ตรวจสอบและบันทึกไฟล์รูปภาพที่อัปโหลดไว้ใต้ wwwroot/uploads
    private bool TrySaveUploadedImage(IFormFile? imageFile, string folderName, string? filePrefix, out string? imageUrl, out string? errorMessage)
    {
        const long maxImageBytes = 5 * 1024 * 1024;
        imageUrl = null;
        errorMessage = null;

        if (imageFile == null)
        {
            return false;
        }

        if (imageFile.Length == 0)
        {
            errorMessage = "ไฟล์รูปภาพว่างเปล่า";
            return false;
        }

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        if (!allowedExtensions.Contains(extension))
        {
            errorMessage = "รองรับเฉพาะไฟล์รูป .jpg, .jpeg, .png, .gif และ .webp";
            return false;
        }

        if (imageFile.Length > maxImageBytes)
        {
            errorMessage = "รูปภาพต้องมีขนาดไม่เกิน 5 MB";
            return false;
        }

        using (var source = imageFile.OpenReadStream())
        {
            Span<byte> header = stackalloc byte[12];
            var bytesRead = 0;
            while (bytesRead < header.Length)
            {
                var count = source.Read(header[bytesRead..]);
                if (count == 0) break;
                bytesRead += count;
            }

            if (!HasImageSignature(extension, header[..bytesRead]))
            {
                errorMessage = "ไฟล์ที่อัปโหลดไม่ใช่รูปภาพตามชนิดไฟล์ที่เลือก";
                return false;
            }
        }

        var webRootPath = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : _env.WebRootPath;

        var uploadFolder = Path.Combine(webRootPath, "uploads", folderName);
        Directory.CreateDirectory(uploadFolder);

        var fileName = $"{filePrefix}{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadFolder, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        imageFile.CopyTo(stream);

        imageUrl = $"/uploads/{folderName}/{fileName}";
        return true;
    }

    private static bool HasImageSignature(string extension, ReadOnlySpan<byte> header)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => header.Length >= 3 && header[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
            ".png" => header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".gif" => header.Length >= 6 && (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8)),
            ".webp" => header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    // ลบไฟล์รูปภาพที่เคยอัปโหลดออกจากดิสก์เมื่อไม่ต้องใช้งานแล้ว
    private void DeleteUploadedFile(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var webRootPath = string.IsNullOrWhiteSpace(_env.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : _env.WebRootPath;
        var filePath = Path.Combine(webRootPath, relativePath.Replace("uploads" + Path.DirectorySeparatorChar, string.Empty));
        var fullPath = Path.Combine(webRootPath, relativePath);

        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }

    // แปลงข้อมูลแบนเนอร์ให้เป็น view model ที่ใช้ในหน้าจอแบนเนอร์ของแอดมิน
    private static BannerViewModel MapBanner(Banner banner)
    {
        return new BannerViewModel
        {
            BannerId = banner.BannerId,
            Title = banner.Title,
            ImageUrl = banner.ImageUrl,
            Position = banner.Position,
            StartDate = banner.StartDate,
            EndDate = banner.EndDate,
            IsActive = banner.IsActive ?? false,
            DisplayOrder = banner.DisplayOrder,
            CreatedAt = banner.CreatedAt
        };
    }

    // แปลงข้อมูลโปรโมชันให้เป็นรูปแบบสรุปที่ใช้ในหน้ารายการของแอดมิน
    private static PromotionViewModel MapPromotion(Promotion promotion, DateTime now)
    {
        var assignedProductNames = promotion.Products
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .ToList();
        var promotionType = NormalizePromotionType(promotion.PromotionType);
        var typeMeta = GetPromotionTypeMeta(promotionType);

        return new PromotionViewModel
        {
            PromotionId = promotion.PromotionId,
            Name = promotion.Name ?? string.Empty,
            PromotionType = promotionType,
            PromotionTypeLabel = typeMeta.Label,
            PromotionTypeDescription = typeMeta.Description,
            DiscountType = promotion.DiscountType,
            DiscountValue = promotion.DiscountValue,
            StartDate = promotion.StartDate,
            EndDate = promotion.EndDate,
            IsAutoApply = promotion.IsAutoApply ?? false,
            CouponCode = promotion.Coupon?.Code,
            CouponUsageLimit = promotion.Coupon?.UsageLimit,
            CouponRedemptionCount = promotion.Coupon?.CouponRedemptions.Count ?? 0,
            MinimumOrderSubtotal = promotion.MinimumOrderSubtotal,
            IsActiveNow = (!promotion.StartDate.HasValue || promotion.StartDate <= now)
                       && (!promotion.EndDate.HasValue || promotion.EndDate >= now),
            AssignedProductCount = promotion.Products.Count,
            AssignedProductSummary = assignedProductNames.Count == 0
                ? "-"
                : string.Join(", ", assignedProductNames.Take(3)) + (assignedProductNames.Count > 3 ? " ..." : string.Empty),
            ProductIds = promotion.Products.Select(p => p.ProductId).ToList()
        };
    }

    // เติมข้อมูลสำหรับแก้ไขและตัวเลือกสินค้าให้กับ model สรุปโปรโมชัน
    private PromotionViewModel MapPromotionForEdit(Promotion promotion)
    {
        var viewModel = MapPromotion(promotion, DateTime.Now);
        var distinctBrandIds = promotion.Products
            .Where(p => p.BrandId.HasValue)
            .Select(p => p.BrandId!.Value)
            .Distinct()
            .ToList();

        if (NormalizePromotionType(promotion.PromotionType) == "brand_event" && distinctBrandIds.Count == 1)
        {
            viewModel.BrandEventBrandId = distinctBrandIds[0];
        }

        viewModel.ProductOptions = GetPromotionProductOptions(viewModel.ProductIds);
        return viewModel;
    }

    // สร้าง แก้ไข หรือลบข้อมูลคูปองที่เชื่อมกับโปรโมชัน
    private void SyncPromotionCoupon(Promotion promotion, PromotionViewModel data)
    {
        var promotionType = NormalizePromotionType(data.PromotionType);
        if (promotionType != "coupon")
        {
            if (promotion.Coupon != null && !promotion.Coupon.CouponRedemptions.Any())
            {
                _db.Coupons.Remove(promotion.Coupon);
            }

            promotion.Coupon = null;
            promotion.CouponId = null;
            return;
        }

        var normalizedCouponCode = NormalizeCouponCode(data.CouponCode)!;
        var usageLimit = data.CouponUsageLimit.HasValue && data.CouponUsageLimit.Value > 0
            ? data.CouponUsageLimit
            : null;

        var coupon = promotion.Coupon;
        if (coupon == null)
        {
            coupon = new Coupon();
            promotion.Coupon = coupon;
        }

        coupon.Code = normalizedCouponCode;
        coupon.DiscountValue = data.DiscountValue ?? 0m;
        coupon.ExpiryDate = data.EndDate;
        coupon.UsageLimit = usageLimit;
    }

    // ปรับรหัสคูปองให้เป็นตัวพิมพ์ใหญ่และตัดช่องว่างออก
    private static string? NormalizeCouponCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant();
    }

    // แปลงข้อมูลสินค้าให้เป็น view model ฝั่งแอดมินพร้อมข้อมูลยอดขายและการแสดงผล
    private static ProductViewModel MapProduct(Product product)
    {
        return new ProductViewModel
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            Sku = product.Sku,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            BrandId = product.BrandId,
            BrandName = product.Brand?.BrandName,
            Status = product.Status,
            IsActive = !string.Equals(product.Status?.Trim(), "inactive", StringComparison.OrdinalIgnoreCase),
            CreatedAt = product.CreatedAt,
            ImageUrl = product.ProductImages
                .OrderByDescending(i => i.IsMain == true)
                .Select(i => i.ImageUrl)
                .FirstOrDefault(),
            Images = product.ProductImages
                .OrderByDescending(i => i.IsMain == true)
                .Select(i => i.ImageUrl ?? "https://placehold.co/300x300?text=No+Image")
                .ToList(),
            ShowInPromotion = product.Promotions.Any(),
            TotalSold = product.OrderItems
                .Where(item => item.Order.Status is "paid" or "packed" or "shipped" or "in_transit" or "delivered")
                .Sum(item => item.Quantity)
        };
    }
}
