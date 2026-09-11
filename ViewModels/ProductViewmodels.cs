using Microsoft.AspNetCore.Http;

namespace ITShop.ViewModels
{
    public class PackageItemInputViewModel
    {
        public int? ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class PackageComponentViewModel
    {
        public int PackageItemId { get; set; }
        public int? ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public string? CategoryName { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsMissing { get; set; }
        public bool IsOutOfStock { get; set; }
        public bool CanOpenProduct => ProductId.HasValue && !IsMissing;
        public string AvailabilityLabel => IsMissing ? "ไม่มีสินค้า" : IsOutOfStock ? "สินค้าหมด" : "พร้อมขาย";
    }

    public class ProductViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int? Stock { get; set; }
        public string? Sku { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int? BrandId { get; set; }
        public string? BrandName { get; set; }
        public string? Status { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; }
        public string? ImageUrl { get; set; }
        public IFormFile? ImageFile { get; set; }
        public List<string> Images { get; set; } = new();
        public List<ProductViewModel> RelatedProducts { get; set; } = new();
        public bool ShowInPromotion { get; set; }
        public int TotalSold { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool HasAutoAppliedPromotion { get; set; }
        public string? ActivePromotionName { get; set; }
        public string? DiscountLabel { get; set; }
        public List<CouponDisplayViewModel> AvailableCoupons { get; set; } = new();
        public bool IsPackageProduct { get; set; }
        public string? PackageName { get; set; }
        public int PackageItemCount { get; set; }
        public bool HasUnavailablePackageItems { get; set; }
        public List<PackageItemInputViewModel> PackageItemInputs { get; set; } = new();
        public List<PackageComponentViewModel> PackageComponents { get; set; } = new();

        // เก็บจำนวนสินค้าที่หน้า UI และตะกร้าใช้อ้างอิงร่วมกัน
        public int Quantity { get; set; } = 1;

        // เตรียมชื่อ property สำรองให้ controller และ view เดิมยังทำงานได้
        public int Id
        {
            get => ProductId;
            set => ProductId = value;
        }
    }
}