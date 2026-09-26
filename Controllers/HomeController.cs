using System.Diagnostics;
using ITShop.Helpers;
using ITShop.Models;
using ITShop.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BannerEntity = ITShop.Models.Banner;
using ProductEntity = ITShop.Models.Product;

namespace ITShop.Controllers;

public class HomeController : Controller
{
    private readonly Csi402dbContext _db;

    public HomeController(Csi402dbContext db)
    {
        _db = db;
    }

    // สร้างข้อมูลหน้าแรกของร้านโดยจัดกลุ่มแบนเนอร์และส่วนสินค้าเด่น
    public IActionResult Index()
    {
        // โหลดสินค้าและข้อมูลที่เกี่ยวข้องสำหรับประกอบแต่ละส่วนของหน้าแรก
        var products = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.ProductImages)
            .Include(p => p.Category)
            .Include(p => p.Promotions)
            .Include(p => p.OrderItems).ThenInclude(item => item.Order)
            .Where(p => p.Status == null || p.Status.ToLower() != "inactive")
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        // แปลงสินค้าจริงเป็น view model หรือใช้ข้อมูลตัวอย่างเมื่อยังไม่มีสินค้า
        var allProducts = products.Count > 0
            ? products.Select(MapProduct).ToList()
            : GetSampleProducts();

        const string defaultBannerImage = "/images/banners/demo-banner.png";

        // จัดข้อมูลที่เตรียมไว้ให้เป็นแต่ละ section ที่หน้า Home ต้องใช้
        var vm = new HomeViewModel
        {
            FeaturedBrands = _db.Brands
                .ToList()
                .GroupBy(b => (b.BrandName ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(b => b.BrandId).First())
                .OrderBy(b => b.BrandName)
                .Select(b => new BrandViewModel
                {
                    BrandId = b.BrandId,
                    BrandName = b.BrandName,
                    LogoUrl = b.LogoUrl,
                    ProductCount = b.Products.Count
                })
                .ToList(),
            Promotions = allProducts
                .Where(p => p.ShowInPromotion)
                .Take(6)
                .ToList(),
            NewArrivals = allProducts
                .OrderByDescending(p => p.CreatedAt ?? DateTime.MinValue)
                .Take(6)
                .ToList(),
            BestSellers = allProducts
                .Where(p => p.TotalSold > 0)
                .OrderByDescending(p => p.TotalSold)
                .ThenByDescending(p => p.CreatedAt ?? DateTime.MinValue)
                .Take(6)
                .ToList(),
            CPUs = GetProductsByCategory(allProducts, "CPU"),
            GraphicsCards = GetProductsByCategory(allProducts, "Graphics Card"),
            Laptops = GetProductsByCategory(allProducts, "Laptop"),
            TopBanners = GetBanners("home_top", new List<string>
            {
                defaultBannerImage,
                defaultBannerImage
            }),
            MiddleBanners = GetBanners("home_middle", new List<string>
            {
                defaultBannerImage,
                defaultBannerImage,
                defaultBannerImage
            }),
            BottomBanners = GetBanners("home_bottom", new List<string>
            {
                defaultBannerImage,
                defaultBannerImage
            })
        };

        // ถ้ายังไม่มียอดขาย ให้ใช้สินค้ามาใหม่แทนเพื่อไม่ให้ส่วนขายดีว่าง
        if (vm.BestSellers.Count == 0)
        {
            vm.BestSellers = allProducts
                .OrderByDescending(p => p.CreatedAt ?? DateTime.MinValue)
                .Take(6)
                .ToList();
        }

        return View(vm);
    }

    // เปิดหน้าแสดงข้อมูลนโยบายความเป็นส่วนตัว
    public IActionResult Privacy()
    {
        return View();
    }

    
    // คัดสินค้าที่เตรียมไว้แล้วให้เหลือเฉพาะหมวดที่ต้องการบนหน้าแรก
    private static List<ProductViewModel> GetProductsByCategory(List<ProductViewModel> products, string categoryName)
    {
        return products
            .Where(p => string.Equals((p.CategoryName ?? string.Empty).Trim(), categoryName, StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToList();
    }

    
    // เปิดหน้าเกี่ยวกับร้าน
    public IActionResult About()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
   
    // เปิดหน้า error มาตรฐานของระบบ MVC
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

   
    // โหลดแบนเนอร์ตามตำแหน่งที่ต้องการและใช้รูปสำรองเมื่อยังไม่มีข้อมูลจริง
    private List<BannerViewModel> GetBanners(string position, List<string> fallbackImages)
    {
        var now = DateTime.Now;

        // โหลดเฉพาะแบนเนอร์ที่เปิดใช้งานและอยู่ในช่วงเวลาที่แสดงได้
        var banners = _db.Banners
            .Where(b => b.Position == position && (b.IsActive ?? false))
            .Where(b => !b.StartDate.HasValue || b.StartDate <= now)
            .Where(b => !b.EndDate.HasValue || b.EndDate >= now)
            .OrderBy(b => b.DisplayOrder)
            .ThenByDescending(b => b.CreatedAt)
            .ToList()
            .Select(MapBanner)
            .ToList();

        // ถ้ายังไม่มีแบนเนอร์จากหลังบ้าน ให้ใช้รูปสำรองแทน
        if (banners.Count == 0)
        {
            banners = fallbackImages
                .Select((image, index) => new BannerViewModel
                {
                    Title = $"Banner {index + 1}",
                    ImageUrl = image,
                    Position = position,
                    IsActive = true,
                    DisplayOrder = index + 1
                })
                .ToList();
        }

        return banners;
    }

   
    // แปลงข้อมูลแบนเนอร์จาก entity ให้อยู่ในรูปที่ view ใช้งานได้ง่าย
    private static BannerViewModel MapBanner(BannerEntity banner)
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

    
    // แปลงข้อมูลสินค้าเป็นการ์ดสินค้าสำหรับหน้าร้านพร้อมข้อมูลโปรโมชัน
    private static ProductViewModel MapProduct(ProductEntity product)
    {
        // คำนวณราคาหลังหักโปรโมชันก่อนส่งไปแสดงบนหน้าเว็บ
        var promotionPrice = PromotionPriceCalculator.Calculate(product);

        return new ProductViewModel
        {
            ProductId = product.ProductId,
            Name = product.Name,
            Description = product.Description,
            Price = promotionPrice.FinalPrice,
            Stock = product.Stock,
            Sku = product.Sku,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            BrandId = product.BrandId,
            BrandName = product.Brand?.BrandName,
            Status = product.Status,
            CreatedAt = product.CreatedAt,
            ImageUrl = product.ProductImages
                .OrderByDescending(i => i.IsMain == true)
                .Select(i => i.ImageUrl)
                .FirstOrDefault(),
            Images = product.ProductImages
                .OrderByDescending(i => i.IsMain == true)
                .Select(i => i.ImageUrl ?? "https://placehold.co/300x300?text=No+Image")
                .ToList(),
            ShowInPromotion = PromotionPriceCalculator.HasAutoApplyPromotion(product),
            TotalSold = product.OrderItems
                .Where(item => item.Order.Status is "paid" or "packed" or "shipped" or "in_transit" or "delivered")
                .Sum(item => item.Quantity),
            OriginalPrice = promotionPrice.OriginalPrice,
            DiscountAmount = promotionPrice.DiscountAmount,
            HasAutoAppliedPromotion = promotionPrice.HasDiscount,
            ActivePromotionName = promotionPrice.PromotionName,
            DiscountLabel = promotionPrice.DiscountLabel
        };
    }

   
    // เตรียมสินค้าตัวอย่างไว้ใช้เมื่อฐานข้อมูลยังไม่มีข้อมูลพอสำหรับหน้าแรก
    private static List<ProductViewModel> GetSampleProducts()
    {
        return new List<ProductViewModel>
        {
            new ProductViewModel
            {
                ProductId = 1,
                Name = "Gaming Monitor",
                Price = 5990,
                Description = "Sample product for home page",
                CategoryName = "Monitor",
                CreatedAt = DateTime.Now.AddDays(-4),
                ShowInPromotion = true,
                TotalSold = 8,
                Images = new List<string> { "https://placehold.co/300x300?text=Monitor" }
            },
            new ProductViewModel
            {
                ProductId = 2,
                Name = "Mechanical Keyboard",
                Price = 2490,
                Description = "Sample product for home page",
                CategoryName = "Keyboard",
                CreatedAt = DateTime.Now.AddDays(-1),
                TotalSold = 12,
                Images = new List<string> { "https://placehold.co/300x300?text=Keyboard" }
            },
            new ProductViewModel
            {
                ProductId = 3,
                Name = "CPU Ryzen",
                Price = 8990,
                Description = "Sample product for home page",
                CategoryName = "CPU",
                CreatedAt = DateTime.Now.AddDays(-2),
                ShowInPromotion = true,
                TotalSold = 20,
                Images = new List<string> { "https://placehold.co/300x300?text=CPU" }
            }
        };
    }
}
