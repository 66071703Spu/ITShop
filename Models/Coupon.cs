using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class Coupon
{
    public int CouponId { get; set; }

    public int? OwnerUserId { get; set; }

    public string? Code { get; set; }

    public decimal? DiscountValue { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public int? UsageLimit { get; set; }

    public virtual User? OwnerUser { get; set; }

    public virtual ICollection<CouponRedemption> CouponRedemptions { get; set; } = new List<CouponRedemption>();

    public virtual ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();

    public virtual ICollection<UserInvite> RewardInvites { get; set; } = new List<UserInvite>();
}
