using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class UserInvite
{
    public int InviteId { get; set; }

    public int InviterUserId { get; set; }

    public int? PromotionId { get; set; }

    public string InviteCode { get; set; } = null!;

    public string? InvitedEmail { get; set; }

    public int? InvitedUserId { get; set; }

    public int? RewardCouponId { get; set; }

    public string Status { get; set; } = null!;

    public bool RewardGiven { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RewardedAt { get; set; }

    public virtual User? InvitedUser { get; set; }

    public virtual User InviterUser { get; set; } = null!;

    public virtual Promotion? Promotion { get; set; }

    public virtual Coupon? RewardCoupon { get; set; }
}
