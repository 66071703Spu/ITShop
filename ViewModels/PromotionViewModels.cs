namespace ITShop.ViewModels
{
    public class PromotionViewModel
    {
        public int PromotionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PromotionType { get; set; }
        public int? BrandEventBrandId { get; set; }
        public string PromotionTypeLabel { get; set; } = string.Empty;
        public string PromotionTypeDescription { get; set; } = string.Empty;
        public string? DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsAutoApply { get; set; }
        public string? CouponCode { get; set; }
        public int? CouponUsageLimit { get; set; }
        public int CouponRedemptionCount { get; set; }
        public decimal? MinimumOrderSubtotal { get; set; }
        public bool IsActiveNow { get; set; }
        public int AssignedProductCount { get; set; }
        public string AssignedProductSummary { get; set; } = string.Empty;
        public List<int> ProductIds { get; set; } = new();
        public List<PromotionProductOptionViewModel> ProductOptions { get; set; } = new();
    }

    public class PromotionProductOptionViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public int? BrandId { get; set; }
        public string? BrandName { get; set; }
        public decimal Price { get; set; }
        public bool IsSelected { get; set; }
    }
}