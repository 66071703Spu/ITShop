namespace ITShop.ViewModels;

public class CouponContextItemViewModel
{
    public int ProductId { get; set; }
    public decimal Subtotal { get; set; }
}

public class CouponDisplayViewModel
{
    public int PromotionId { get; set; }
    public int CouponId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public string DiscountLabel { get; set; } = string.Empty;
    public decimal? MinimumOrderSubtotal { get; set; }
    public decimal EligibleSubtotal { get; set; }
    public decimal MinimumSpendRemaining { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public int? RemainingUses => UsageLimit.HasValue ? Math.Max(0, UsageLimit.Value - UsedCount) : null;
    public bool AppliesToAllProducts { get; set; }
    public int ApplicableProductCount { get; set; }
    public string ApplicableProductSummary { get; set; } = string.Empty;
    public bool IsCurrentlyEligible { get; set; }
    public string EligibilityMessage { get; set; } = string.Empty;
}

public class CouponHistoryItemViewModel
{
    public string Code { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public string DiscountLabel { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public decimal OrderFinalAmount { get; set; }
    public DateTime? UsedAt { get; set; }
}