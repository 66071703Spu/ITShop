using ITShop.Models;
using ITShop.ViewModels;
using ITShop.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductEntity = ITShop.Models.Product;

namespace ITShop.Controllers;

public class ProductController : Controller
{
    private readonly Csi402dbContext _db;
    private const string PromotionFilter = "promotion";
    private const string NewArrivalFilter = "newarrival";
    private const string BestSellerFilter = "bestseller";
    private const int NewArrivalDays = 30;
    private static readonly List<string> CategoryOrder = new()
    {
        "CPU",
        "Graphics Card",
        "Mainboard",
        "RAM",
        "Storage",
        "Power Supply",
        "Cooling",
        "Case",
        "Monitor",
        "Laptop",
        "Mouse",
        "Keyboard",
        "Headset",
        "Gaming Gear",
        "Comset"
    };

    public ProductController(Csi402dbContext db)
    {
        _db = db;
    }

    // แสดงหน้ารายการสินค้าโดยรองรับการกรองหมวด ค้นหา สต็อก โปรโมชัน และการเรียงลำดับ
    public IActionResult Index(
        int? categoryId,
        int? brandId,
        string? brand,
        string? category,
        string? search,
        string? section,
        string? specialFilter,
        bool promotionOnly = false,
        bool newArrivalOnly = false,
        bool bestSellerOnly = false,
        bool inStockOnly = false,
        bool outOfStockOnly = false,
        string? priceSort = null)
    {
        // รวมเงื่อนไขการกรองที่รับเข้ามาให้อยู่ในรูปแบบเดียวก่อน query สินค้า
        var selectedSpecialFilters = ResolveSpecialFilters(section, specialFilter, promotionOnly, newArrivalOnly, bestSellerOnly);
        var showPromotion = selectedSpecialFilters.Contains(PromotionFilter);
        var showNewArrival = selectedSpecialFilters.Contains(NewArrivalFilter);
        var showBestSeller = selectedSpecialFilters.Contains(BestSellerFilter);

        // เช็คว่าควรใช้ข้อมูลสินค้าจริงจากฐานข้อมูลหรือข้อมูลตัวอย่าง
        var hasAnyProductsInDatabase = _db.Products.Any(p => p.Status == null || p.Status.ToLower() != "inactive");

        // เริ่มจาก query สินค้าที่ยัง active แล้วค่อยเติมเงื่อนไขกรองทีละส่วน
        var query = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.ProductImages)
            .Include(p => p.Category)
            .Include(p => p.Promotions)
            .Include(p => p.OrderItems).ThenInclude(item => item.Order)
            .Where(p => p.Status == null || p.Status.ToLower() != "inactive")
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
            category ??= _db.Categories.FirstOrDefault(c => c.CategoryId == categoryId.Value)?.Name;
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim().Replace(" ", string.Empty).ToLower();

            query = query.Where(p =>
                (p.Category != null && p.Category.Name.ToLower().Replace(" ", string.Empty).Contains(normalizedCategory)) ||
                p.Name.ToLower().Replace(" ", string.Empty).Contains(normalizedCategory));
        }

        if (brandId.HasValue)
        {
            query = query.Where(p => p.BrandId == brandId.Value);
        }

        if (!string.IsNullOrWhiteSpace(brand))
        {
            var normalizedBrand = brand.Trim();
            query = query.Where(p => p.Brand != null && p.Brand.BrandName == normalizedBrand);
        }

        var selectedBrandName = !string.IsNullOrWhiteSpace(brand)
            ? brand.Trim()
            : brandId.HasValue
                ? _db.Brands.Where(b => b.BrandId == brandId.Value).Select(b => b.BrandName).FirstOrDefault()
                : null;

        // ค้นหาคำที่ผู้ใช้กรอกจากข้อมูลหลักของสินค้า
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.Name.Contains(search) ||
                (p.Description ?? string.Empty).Contains(search) ||
                (p.Sku ?? string.Empty).Contains(search) ||
                (p.Category != null && p.Category.Name.Contains(search)) ||
                (p.Brand != null && p.Brand.BrandName.Contains(search)));
        }

        // ดึงข้อมูลออกจากฐานข้อมูลแล้วแปลงเป็น view model สำหรับหน้า catalog
        var products = query
            .OrderByDescending(p => p.CreatedAt)
            .ToList()
            .Select(MapProduct)
            .ToList();

        // เติมรายละเอียดชิ้นส่วนให้สินค้าประเภท Comset
        foreach (var mappedProduct in products.Where(p => IsComsetProduct(p.CategoryName)))
        {
            var sourceProduct = query.FirstOrDefault(p => p.ProductId == mappedProduct.ProductId);
            if (sourceProduct != null)
            {
                ApplyPackageDetails(mappedProduct, sourceProduct);
            }
        }

        if (inStockOnly ^ outOfStockOnly)
        {
            products = products
                .Where(product => inStockOnly ? (product.Stock ?? 0) > 0 : (product.Stock ?? 0) <= 0)
                .ToList();
        }

        // กรองและเรียงลำดับข้อมูลรอบสุดท้ายก่อนส่งไปหน้า view
        products = ApplySpecialFilters(products, showPromotion, showNewArrival, showBestSeller);
        products = ApplySorting(products, showPromotion, showNewArrival, showBestSeller, priceSort);

        // ใช้สินค้าตัวอย่างเฉพาะตอนที่ฐานข้อมูลยังไม่มีสินค้าที่เปิดใช้งาน
        if (!hasAnyProductsInDatabase)
        {
            products = GetSampleProducts();

            if (!string.IsNullOrWhiteSpace(category))
            {
                products = products
                    .Where(p => (p.CategoryName ?? string.Empty).Contains(category, StringComparison.OrdinalIgnoreCase)
                             || p.Name.Contains(category, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                products = products
                    .Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                             || (p.Description ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
                             || (p.CategoryName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
                             || (p.BrandName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            products = ApplyStockFilter(products, inStockOnly, outOfStockOnly);
            products = ApplySpecialFilters(products, showPromotion, showNewArrival, showBestSeller);
            products = ApplySorting(products, showPromotion, showNewArrival, showBestSeller, priceSort);
        }

        // เตรียมรายการหมวดและแบรนด์สำหรับแสดงตัวกรองในหน้า catalog
        var categories = _db.Categories
            .Select(c => c.Name)
            .Distinct()
            .AsEnumerable()
            .OrderBy(name => GetCategorySortOrder(name))
            .ThenBy(name => name)
            .ToList();

        if (categories.Count == 0)
        {
            categories = CategoryOrder.ToList();
        }

        var brands = _db.Brands
            .OrderBy(b => b.BrandName)
            .ToList();

        ViewBag.Categories = categories;
        ViewBag.Brands = brands;
        ViewBag.CurrentCategory = category ?? string.Empty;
        ViewBag.CurrentBrandId = brandId;
        ViewBag.Search = search ?? string.Empty;
        ViewBag.ShowPromotion = showPromotion;
        ViewBag.ShowNewArrival = showNewArrival;
        ViewBag.ShowBestSeller = showBestSeller;
        ViewBag.InStockOnly = inStockOnly;
        ViewBag.OutOfStockOnly = outOfStockOnly;
        ViewBag.PriceSort = priceSort ?? string.Empty;
        ViewBag.PageTitle = GetPageTitle(category, selectedBrandName, search, showPromotion, showNewArrival, showBestSeller);

        return View("ProductCatalog", products);
    }

    // แสดงหน้ารายละเอียดสินค้าพร้อมคูปอง สินค้าที่เกี่ยวข้อง และข้อมูลชุดสินค้า
    public IActionResult ProductDetail(int id)
    {
        // โหลดสินค้าตาม id พร้อมข้อมูลที่หน้า detail ต้องใช้
        var product = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.ProductImages)
            .Include(p => p.Category)
            .Include(p => p.Promotions)
            .Include(p => p.OrderItems).ThenInclude(item => item.Order)
            .Where(p => p.Status == null || p.Status.ToLower() != "inactive")
            .FirstOrDefault(p => p.ProductId == id);

        // ถ้าไม่พบสินค้าที่ขอมา ให้ fallback ไปสินค้าจริงตัวอื่นหรือข้อมูลตัวอย่าง
        if (product == null)
        {
            var fallback = _db.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductImages)
                .Include(p => p.Category)
                .Include(p => p.Promotions)
                .Include(p => p.OrderItems).ThenInclude(item => item.Order)
                .Where(p => p.Status == null || p.Status.ToLower() != "inactive")
                .FirstOrDefault();

            if (fallback != null)
            {
                var fallbackViewModel = MapProduct(fallback);
                ApplyPackageDetails(fallbackViewModel, fallback);
                fallbackViewModel.RelatedProducts = GetRelatedProducts(fallback, fallbackViewModel.ProductId);
                return View(fallbackViewModel);
            }

            var sampleProduct = GetSampleProducts().FirstOrDefault(p => p.ProductId == id)
                               ?? GetSampleProducts().First();
            sampleProduct.RelatedProducts = GetSampleProducts()
                .Where(p => p.ProductId != sampleProduct.ProductId)
                .Where(p => string.Equals(p.CategoryName, sampleProduct.CategoryName, StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .ToList();
            return View(sampleProduct);
        }

        // เตรียมข้อมูลหลักของสินค้า รายละเอียดชุด คูปองที่ใช้ได้ และสินค้าที่เกี่ยวข้อง
        var viewModel = MapProduct(product);
        ApplyPackageDetails(viewModel, product);
        var currentUserId = HttpContext.Session.GetInt32("UserId");
        viewModel.AvailableCoupons = CouponPromotionHelper.GetVisibleCoupons(
            _db,
            currentUserId,
            new[]
            {
                new CouponContextItemViewModel
                {
                    ProductId = product.ProductId,
                    Subtotal = viewModel.Price
                }
            });
        viewModel.RelatedProducts = GetRelatedProducts(product, product.ProductId);

        return View(viewModel);
    }

    // รวมค่า filter จากหลายรูปแบบให้กลายเป็นชุดเงื่อนไขพิเศษแบบเดียวกัน
    private static HashSet<string> ResolveSpecialFilters(string? section, string? specialFilter, bool promotionOnly, bool newArrivalOnly, bool bestSellerOnly)
    {
        var filters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var normalizedSection = NormalizeSpecialFilter(section);
        if (!string.IsNullOrWhiteSpace(normalizedSection))
        {
            filters.Add(normalizedSection);
        }

        var normalizedSpecialFilter = NormalizeSpecialFilter(specialFilter);
        if (!string.IsNullOrWhiteSpace(normalizedSpecialFilter))
        {
            filters.Add(normalizedSpecialFilter);
        }

        if (promotionOnly)
        {
            filters.Add(PromotionFilter);
        }

        if (newArrivalOnly)
        {
            filters.Add(NewArrivalFilter);
        }

        if (bestSellerOnly)
        {
            filters.Add(BestSellerFilter);
        }

        return filters;
    }

    // ปรับชื่อ filter ให้เป็นรูปแบบมาตรฐานแม้จะรับค่ามาหลายแบบ
    private static string NormalizeSpecialFilter(string? value)
    {
        return (value ?? string.Empty).Trim().ToLower() switch
        {
            "promotion" => PromotionFilter,
            "new" => NewArrivalFilter,
            "newarrival" => NewArrivalFilter,
            "bestseller" => BestSellerFilter,
            _ => string.Empty
        };
    }

    // สร้างชื่อหัวข้อหน้าตามเงื่อนไขที่ผู้ใช้เลือกอยู่
    private static string GetPageTitle(string? category, string? brandName, string? search, bool showPromotion, bool showNewArrival, bool showBestSeller)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            return $"Category: {category}";
        }

        if (!string.IsNullOrWhiteSpace(brandName))
        {
            return $"Brand: {brandName}";
        }

        var selectedCount = new[] { showPromotion, showNewArrival, showBestSeller }.Count(value => value);

        if (selectedCount > 1)
        {
            return "Filtered Products";
        }

        if (showPromotion)
        {
            return "Promotion Products";
        }

        if (showNewArrival)
        {
            return "New Arrival Products";
        }

        if (showBestSeller)
        {
            return "Best Seller Products";
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            return $"Search: {search}";
        }

        return "All Products";
    }

    // กรองรายการสินค้าในหน่วยความจำตามสถานะมีสต็อกหรือหมดสต็อก
    private static List<ProductViewModel> ApplyStockFilter(List<ProductViewModel> products, bool inStockOnly, bool outOfStockOnly)
    {
        if (!(inStockOnly ^ outOfStockOnly))
        {
            return products;
        }

        return inStockOnly
            ? products.Where(p => (p.Stock ?? 0) > 0).ToList()
            : products.Where(p => (p.Stock ?? 0) <= 0).ToList();
    }

    // กรองสินค้าตามเงื่อนไขโปรโมชัน สินค้าใหม่ และสินค้าขายดี
    private static List<ProductViewModel> ApplySpecialFilters(List<ProductViewModel> products, bool showPromotion, bool showNewArrival, bool showBestSeller)
    {
        if (!showPromotion && !showNewArrival && !showBestSeller)
        {
            return products;
        }

        return products
            .Where(p => (showPromotion && p.ShowInPromotion)
                     || (showNewArrival && (p.CreatedAt ?? DateTime.MinValue) >= DateTime.Now.AddDays(-NewArrivalDays))
                     || (showBestSeller && p.TotalSold > 0))
            .ToList();
    }

    // เรียงลำดับรายการสินค้าตามรูปแบบที่ผู้ใช้เลือก
    private static List<ProductViewModel> ApplySorting(List<ProductViewModel> products, bool showPromotion, bool showNewArrival, bool showBestSeller, string? priceSort)
    {
        var normalizedSort = (priceSort ?? string.Empty).Trim().ToLower();

        return normalizedSort switch
        {
            "low-high" => products.OrderBy(p => p.Price).ThenBy(p => p.Name).ToList(),
            "high-low" => products.OrderByDescending(p => p.Price).ThenBy(p => p.Name).ToList(),
            _ when showBestSeller => products
                .OrderByDescending(p => p.TotalSold)
                .ThenByDescending(p => p.CreatedAt ?? DateTime.MinValue)
                .ToList(),
            _ when showNewArrival => products
                .OrderByDescending(p => p.CreatedAt ?? DateTime.MinValue)
                .ToList(),
            _ when showPromotion => products
                .OrderByDescending(p => p.CreatedAt ?? DateTime.MinValue)
                .ThenBy(p => p.Name)
                .ToList(),
            _ => products
                .OrderByDescending(p => p.CreatedAt ?? DateTime.MinValue)
                .ToList()
        };
    }

    // เลือกสินค้าที่เกี่ยวข้องจากหมวด แบรนด์ และคำสำคัญที่คล้ายกัน
    private List<ProductViewModel> GetRelatedProducts(ProductEntity product, int currentProductId)
    {
        // ดึงคำสำคัญของสินค้าปัจจุบันไว้ใช้เปรียบเทียบกับสินค้าอื่น
        var currentTokens = ExtractTokens(product.Name, product.Description, product.Category?.Name);
        var currentPrice = product.Price;

        // ให้คะแนนสินค้าที่อาจเกี่ยวข้องจากหมวด แบรนด์ คำที่ซ้ำ และช่วงราคา
        var candidates = _db.Products
            .Include(p => p.Brand)
            .Include(p => p.ProductImages)
            .Include(p => p.Category)
            .Include(p => p.Promotions)
            .Include(p => p.OrderItems).ThenInclude(item => item.Order)
            .Where(p => p.ProductId != currentProductId)
            .ToList()
            .Where(p => p.Status == null || !string.Equals(p.Status.Trim(), "inactive", StringComparison.OrdinalIgnoreCase))
            .Where(p => (p.Stock ?? 0) > 0)
            .Select(p => new
            {
                Product = p,
                Score = CalculateRelatedScore(p, product.CategoryId, product.BrandId, currentTokens, currentPrice)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => Math.Abs(item.Product.Price - currentPrice))
            .ThenByDescending(item => item.Product.CreatedAt ?? DateTime.MinValue)
            .Take(4)
            .Select(item => MapProduct(item.Product))
            .ToList();

        // ถ้าไม่มีตัวที่คะแนนดีพอ ให้ใช้สินค้าที่มีสต็อกและใหม่ที่สุดแทน
        if (candidates.Count > 0)
        {
            return candidates;
        }

        return _db.Products
            .Include(p => p.Brand)
            .Include(p => p.ProductImages)
            .Include(p => p.Category)
            .Include(p => p.Promotions)
            .Include(p => p.OrderItems).ThenInclude(item => item.Order)
            .Where(p => p.ProductId != currentProductId)
            .Where(p => p.Status == null || p.Status.ToLower() != "inactive")
            .Where(p => (p.Stock ?? 0) > 0)
            .OrderByDescending(p => p.CreatedAt)
            .Take(4)
            .ToList()
            .Select(MapProduct)
            .ToList();
    }

    // คำนวณคะแนนความใกล้เคียงของสินค้า candidate กับสินค้าปัจจุบัน
    private static int CalculateRelatedScore(ProductEntity candidate, int? currentCategoryId, int? currentBrandId, HashSet<string> currentTokens, decimal currentPrice)
    {
        var score = 0;

        if (candidate.CategoryId == currentCategoryId)
        {
            score += 1000;
        }

        if (candidate.BrandId.HasValue && candidate.BrandId == currentBrandId)
        {
            score += 180;
        }

        var candidateTokens = ExtractTokens(candidate.Name, candidate.Description, candidate.Category?.Name);
        var sharedTokenCount = candidateTokens.Intersect(currentTokens).Count();
        score += sharedTokenCount * 120;

        var priceDifference = Math.Abs(candidate.Price - currentPrice);
        if (priceDifference <= 1000)
        {
            score += 120;
        }
        else if (priceDifference <= 3000)
        {
            score += 70;
        }
        else if (priceDifference <= 7000)
        {
            score += 30;
        }

        if (candidate.Promotions.Any())
        {
            score += 20;
        }

        if ((candidate.Stock ?? 0) > 10)
        {
            score += 10;
        }

        return score;
    }

    // เติมรายละเอียดชุดสินค้าหรือ Comset ลงใน view model
    private void ApplyPackageDetails(ProductViewModel viewModel, ProductEntity product)
    {
        // ทำงานต่อเฉพาะสินค้าหมวด Comset เท่านั้น
        if (!IsComsetProduct(product.Category?.Name))
        {
            return;
        }

        // หา package ที่ตรงกันจาก product id หรือชื่อที่ปรับรูปแบบแล้ว
        var packageQuery = _db.ProductPackages
            .Include(pp => pp.PackageItems)
                .ThenInclude(pi => pi.Product)
                    .ThenInclude(p => p!.Category)
            .Include(pp => pp.PackageItems)
                .ThenInclude(pi => pi.Product)
                    .ThenInclude(p => p!.ProductImages)
            .AsQueryable();

        var normalizedName = (product.Name ?? string.Empty).Trim();
        var package = packageQuery.FirstOrDefault(pp => pp.PackageId == product.ProductId);

        if (package == null && !string.IsNullOrWhiteSpace(normalizedName))
        {
            package = packageQuery.FirstOrDefault(pp => pp.Name != null && pp.Name.Trim() == normalizedName);
        }

        if (package == null)
        {
            viewModel.IsPackageProduct = true;
            viewModel.HasUnavailablePackageItems = true;
            viewModel.Stock = 0;
            return;
        }

        // คัดลอกข้อมูลสรุป package และรายการชิ้นส่วนไปใส่ใน view model
        viewModel.IsPackageProduct = true;
        viewModel.PackageName = package.Name;
        viewModel.PackageComponents = package.PackageItems
            .OrderBy(item => item.Id)
            .Select(MapPackageComponent)
            .ToList();
        viewModel.PackageItemCount = viewModel.PackageComponents.Sum(item => item.Quantity);
        viewModel.Stock = PackageAvailabilityHelper.GetAvailableStock(product, package);
        viewModel.HasUnavailablePackageItems = viewModel.Stock <= 0;
    }

    // แปลงข้อมูลชิ้นส่วนใน package ให้เป็นรูปแบบที่หน้าเว็บใช้แสดงผล
    private static PackageComponentViewModel MapPackageComponent(PackageItem item)
    {
        var product = item.Product;
        var isMissing = product == null || string.Equals(product.Status, "inactive", StringComparison.OrdinalIgnoreCase);
        var isOutOfStock = !isMissing && (product!.Stock ?? 0) < (item.Quantity ?? 1);

        return new PackageComponentViewModel
        {
            PackageItemId = item.Id,
            ProductId = isMissing ? null : product!.ProductId,
            Name = isMissing ? "ไม่มีสินค้า" : product!.Name,
            Quantity = item.Quantity ?? 1,
            CategoryName = isMissing ? null : product!.Category?.Name,
            Description = isMissing ? "ชิ้นส่วนนี้ถูกลบออกหรือปิดการใช้งานแล้ว" : BuildPackageComponentDescription(product!),
            ImageUrl = isMissing
                ? "https://placehold.co/300x300?text=No+Product"
                : product!.ProductImages
                    .OrderByDescending(image => image.IsMain == true)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault() ?? "https://placehold.co/300x300?text=No+Image",
            IsMissing = isMissing,
            IsOutOfStock = isOutOfStock
        };
    }

    // สร้างคำอธิบายสั้นของสินค้าย่อยที่อยู่ในชุดสินค้า
    private static string BuildPackageComponentDescription(ProductEntity product)
    {
        var description = (product.Description ?? string.Empty).Trim();
        if (description.Length > 110)
        {
            description = description[..107] + "...";
        }

        var category = string.IsNullOrWhiteSpace(product.Category?.Name) ? "ทั่วไป" : product.Category!.Name;
        var stockLabel = (product.Stock ?? 0) > 0 ? $"คงเหลือ {product.Stock ?? 0} ชิ้น" : "สินค้าหมด";

        return string.IsNullOrWhiteSpace(description)
            ? $"หมวดหมู่ {category} | {stockLabel}"
            : $"{description} | หมวดหมู่ {category} | {stockLabel}";
    }

    // เช็คว่าหมวดสินค้านี้เป็น Comset หรือไม่
    private static bool IsComsetProduct(string? categoryName)
    {
        return string.Equals((categoryName ?? string.Empty).Trim(), "Comset", StringComparison.OrdinalIgnoreCase);
    }

    // แยกคำสำคัญจากข้อความสินค้าเพื่อใช้จับคู่สินค้าที่เกี่ยวข้อง
    private static HashSet<string> ExtractTokens(params string?[] values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!
                .Split(new[] { ' ', '-', '(', ')', '/', ',', '.', '+', '_' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(token => token.Trim().ToLowerInvariant())
            .Where(token => token.Length >= 3)
            .ToHashSet();
    }

    // คืนค่าลำดับหมวดที่อยากให้ใช้ตอนเรียงหมวดในหน้า catalog
    private static int GetCategorySortOrder(string? name)
    {
        var index = CategoryOrder.FindIndex(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : int.MaxValue;
    }

    // แปลงข้อมูลสินค้าให้พร้อมใช้ในหน้า catalog และหน้า detail
    private static ProductViewModel MapProduct(ProductEntity product)
    {
        // คำนวณราคาหลังส่วนลดก่อนนำไปใส่ใน model สำหรับแสดงผล
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
                .Select(i => i.ImageUrl ?? "https://placehold.co/600x400?text=No+Image")
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

    // เตรียมสินค้าตัวอย่างไว้ใช้เมื่อยังไม่มีข้อมูล catalog จริง
    private static List<ProductViewModel> GetSampleProducts()
    {
        return new List<ProductViewModel>
        {
            new ProductViewModel
            {
                ProductId = 1,
                Name = "Gaming Monitor",
                Price = 5990,
                Description = "หน้าจอสำหรับเล่นเกม ใช้งานลื่นและคมชัด",
                CategoryName = "Monitor",
                Stock = 10,
                CreatedAt = DateTime.Now.AddDays(-4),
                ShowInPromotion = true,
                TotalSold = 8,
                Images = new List<string>
                {
                    "https://placehold.co/600x400?text=Gaming+Monitor",
                    "https://placehold.co/600x400?text=Monitor+Side"
                }
            },
            new ProductViewModel
            {
                ProductId = 2,
                Name = "Mechanical Keyboard",
                Price = 2490,
                Description = "คีย์บอร์ดสำหรับเล่นเกมและทำงาน",
                CategoryName = "Keyboard",
                Stock = 15,
                CreatedAt = DateTime.Now.AddDays(-1),
                TotalSold = 12,
                Images = new List<string>
                {
                    "https://placehold.co/600x400?text=Keyboard",
                    "https://placehold.co/600x400?text=Keyboard+RGB"
                }
            },
            new ProductViewModel
            {
                ProductId = 3,
                Name = "CPU Ryzen",
                Price = 8990,
                Description = "ซีพียูสำหรับคอมประกอบ เล่นเกมและทำงานได้ดี",
                CategoryName = "CPU",
                Stock = 8,
                CreatedAt = DateTime.Now.AddDays(-2),
                ShowInPromotion = true,
                TotalSold = 20,
                Images = new List<string>
                {
                    "https://placehold.co/600x400?text=CPU+Ryzen",
                    "https://placehold.co/600x400?text=CPU+Box"
                }
            },
            new ProductViewModel
            {
                ProductId = 4,
                Name = "RTX Graphics Card",
                Price = 14990,
                Description = "การ์ดจอสำหรับงานกราฟิกและเล่นเกม",
                CategoryName = "Graphics Card",
                Stock = 5,
                Images = new List<string>
                {
                    "https://placehold.co/600x400?text=Graphics+Card"
                }
            },
            new ProductViewModel
            {
                ProductId = 5,
                Name = "Gaming Mouse",
                Price = 990,
                Description = "เมาส์สำหรับเล่นเกมจับถนัดมือ",
                CategoryName = "Mouse",
                Stock = 20,
                Images = new List<string>
                {
                    "https://placehold.co/600x400?text=Gaming+Mouse"
                }
            },
            new ProductViewModel
            {
                ProductId = 6,
                Name = "Laptop Pro",
                Price = 25990,
                Description = "โน้ตบุ๊กสำหรับทำงานและเรียน",
                CategoryName = "Laptop",
                Stock = 6,
                Images = new List<string>
                {
                    "https://placehold.co/600x400?text=Laptop"
                }
            },
            new ProductViewModel
            {
                ProductId = 7,
                Name = "Comset Starter Kit",
                Price = 1290,
                Description = "อุปกรณ์คอมเซ็ตพื้นฐานสำหรับเริ่มต้นใช้งาน",
                CategoryName = "Comset",
                Stock = 12,
                IsPackageProduct = true,
                PackageName = "Comset Starter Kit",
                PackageItemCount = 3,
                Images = new List<string>
                {
                    "https://placehold.co/600x400?text=Comset"
                },
                PackageComponents = new List<PackageComponentViewModel>
                {
                    new()
                    {
                        PackageItemId = 1,
                        ProductId = 3,
                        Name = "CPU Ryzen",
                        Quantity = 1,
                        CategoryName = "CPU",
                        Description = "ซีพียูสำหรับคอมประกอบ เล่นเกมและทำงานได้ดี | หมวดหมู่ CPU | คงเหลือ 8 ชิ้น",
                        ImageUrl = "https://placehold.co/300x300?text=CPU+Ryzen"
                    },
                    new()
                    {
                        PackageItemId = 2,
                        ProductId = 2,
                        Name = "Mechanical Keyboard",
                        Quantity = 1,
                        CategoryName = "Keyboard",
                        Description = "คีย์บอร์ดสำหรับเล่นเกมและทำงาน | หมวดหมู่ Keyboard | คงเหลือ 15 ชิ้น",
                        ImageUrl = "https://placehold.co/300x300?text=Keyboard"
                    },
                    new()
                    {
                        PackageItemId = 3,
                        Name = "ไม่มีสินค้า",
                        Quantity = 1,
                        Description = "ชิ้นส่วนนี้ถูกลบออกหรือปิดการใช้งานแล้ว",
                        ImageUrl = "https://placehold.co/300x300?text=No+Product",
                        IsMissing = true
                    }
                },
                HasUnavailablePackageItems = true
            }
        };
    }
}
