using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class Promotion
{
    public int PromotionId { get; set; }

    public int? CouponId { get; set; }

    public decimal? MinimumOrderSubtotal { get; set; }

    public string? Name { get; set; }

    public string? PromotionType { get; set; }

    public string? DiscountType { get; set; }

    public decimal? DiscountValue { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool? IsAutoApply { get; set; }

    public virtual Coupon? Coupon { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
