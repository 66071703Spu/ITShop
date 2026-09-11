using ITShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ITShop.Helpers;

public static class InvitePromotionHelper
{
    // เก็บข้อมูล invite ที่ผ่านการตรวจสอบแล้วเพื่อส่งต่อให้ flow สมัครสมาชิกใช้งานได้ทันที
    public sealed class InviteSignupContext
    {
        public required string InviteCode { get; init; }
        public required User InviterUser { get; init; }
        public required Promotion Promotion { get; init; }
    }

    // ปรับ invite code ให้เป็นรูปแบบมาตรฐานก่อนนำไปค้นหาในระบบ
    public static string? NormalizeInviteCode(string? inviteCode)
    {
        if (string.IsNullOrWhiteSpace(inviteCode))
        {
            return null;
        }

        return inviteCode.Trim().ToUpperInvariant();
    }

    // หาโปรโมชัน invite ที่ยัง active อยู่ตัวหลักเพื่อใช้กับการสมัครผ่านลิงก์เชิญ
    public static Promotion? GetPrimaryActiveInvitePromotion(Csi402dbContext db, DateTime? now = null)
    {
        var evaluatedAt = now ?? DateTime.Now;

        return db.Promotions
            .Include(p => p.Products)
            .Where(p => (p.PromotionType ?? string.Empty).ToLower() == "invite")
            .Where(p => !p.StartDate.HasValue || p.StartDate <= evaluatedAt)
            .Where(p => !p.EndDate.HasValue || p.EndDate >= evaluatedAt)
            .OrderByDescending(p => p.StartDate ?? DateTime.MinValue)
            .ThenByDescending(p => p.PromotionId)
            .FirstOrDefault();
    }

    // รับประกันว่า user มี invite code ถาวรและไม่ซ้ำกับคนอื่นในระบบ
    public static string EnsurePersistentInviteCode(Csi402dbContext db, User user, bool saveChanges = true)
    {
        if (!string.IsNullOrWhiteSpace(user.InviteCode))
        {
            return user.InviteCode;
        }

        var existingCode = db.UserInvites
            .Where(invite => invite.InviterUserId == user.UserId && invite.InviteCode != null)
            .OrderBy(invite => invite.CreatedAt)
            .Select(invite => invite.InviteCode)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(existingCode)
            && !db.Users.Any(other => other.UserId != user.UserId && other.InviteCode == existingCode))
        {
            user.InviteCode = existingCode;
        }

        if (string.IsNullOrWhiteSpace(user.InviteCode))
        {
            user.InviteCode = GenerateUniquePersistentInviteCode(db);
        }

        if (saveChanges)
        {
            db.SaveChanges();
        }

        return user.InviteCode;
    }

    // ตรวจว่า invite code ใช้งานได้จริงและผูกกับโปรโมชัน invite ที่ยังไม่หมดอายุ
    public static InviteSignupContext? GetValidInviteSignupContext(Csi402dbContext db, string? inviteCode, DateTime? now = null)
    {
        var normalizedInviteCode = NormalizeInviteCode(inviteCode);
        if (normalizedInviteCode == null)
        {
            return null;
        }

        var inviter = db.Users.FirstOrDefault(user => user.InviteCode == normalizedInviteCode);
        if (inviter == null)
        {
            return null;
        }

        var promotion = GetPrimaryActiveInvitePromotion(db, now);
        if (promotion == null)
        {
            return null;
        }

        return new InviteSignupContext
        {
            InviteCode = normalizedInviteCode,
            InviterUser = inviter,
            Promotion = promotion
        };
    }

    // สร้าง invite code ถาวรที่ไม่ซ้ำในระบบพร้อมมี fallback เมื่อสุ่มชนหลายครั้ง
    public static string GenerateUniquePersistentInviteCode(Csi402dbContext db)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = $"INV{Random.Shared.Next(100000, 999999)}{Random.Shared.Next(10, 99)}";
            if (!db.Users.Any(user => user.InviteCode == code))
            {
                return code;
            }
        }

        return $"INV{Guid.NewGuid():N}"[..14].ToUpperInvariant();
    }

    // สร้าง coupon code สำหรับรางวัลจากการเชิญเพื่อนโดยไม่ให้ซ้ำกับของเดิม
    public static string GenerateUniqueRewardCouponCode(Csi402dbContext db)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var code = $"REWARD{Random.Shared.Next(100000, 999999)}";
            if (!db.Coupons.Any(coupon => coupon.Code == code))
            {
                return code;
            }
        }

        return $"REWARD{Guid.NewGuid():N}"[..16].ToUpperInvariant();
    }

    // ประกอบลิงก์สมัครสมาชิกที่แนบ invite code ให้พร้อมใช้งานบนเว็บจริง
    public static string BuildInviteLink(HttpRequest request, string inviteCode)
    {
        return $"{request.Scheme}://{request.Host}/Account/Signup?invite={Uri.EscapeDataString(inviteCode)}";
    }

    // สรุปว่าสิทธิ์ของ reward coupon ใช้กับสินค้าใดบ้าง
    public static string BuildInviteProductSummary(Promotion promotion)
    {
        if (!promotion.Products.Any())
        {
            return "reward coupon ใช้ได้ทั้งร้าน";
        }

        var names = promotion.Products
            .OrderBy(product => product.Name)
            .Select(product => product.Name)
            .ToList();

        return string.Join(", ", names.Take(3)) + (names.Count > 3 ? " ..." : string.Empty);
    }

    // ออกรางวัลให้ผู้เชิญเมื่อผู้ถูกเชิญทำคำสั่งซื้อแรกสำเร็จเป็นครั้งแรก
    public static string? GrantInviteRewardForFirstOrder(Csi402dbContext db, int invitedUserId, DateTime? now = null)
    {
        var evaluatedAt = now ?? DateTime.Now;
        var inviteRecord = db.UserInvites
            .Include(invite => invite.Promotion)
                .ThenInclude(promotion => promotion!.Products)
            .FirstOrDefault(invite => invite.InvitedUserId == invitedUserId && invite.Status == "registered" && !invite.RewardGiven);

        if (inviteRecord?.Promotion == null)
        {
            return null;
        }

        var rewardCode = GenerateUniqueRewardCouponCode(db);
        var rewardExpiry = evaluatedAt.AddDays(30);
        var rewardCoupon = new Coupon
        {
            OwnerUserId = inviteRecord.InviterUserId,
            Code = rewardCode,
            DiscountValue = inviteRecord.Promotion.DiscountValue,
            ExpiryDate = rewardExpiry,
            UsageLimit = 1
        };

        var rewardPromotion = new Promotion
        {
            Name = $"Invite Reward - {inviteRecord.Promotion.Name}",
            PromotionType = "coupon",
            DiscountType = inviteRecord.Promotion.DiscountType,
            DiscountValue = inviteRecord.Promotion.DiscountValue,
            StartDate = evaluatedAt,
            EndDate = rewardExpiry,
            IsAutoApply = false,
            MinimumOrderSubtotal = inviteRecord.Promotion.MinimumOrderSubtotal,
            Coupon = rewardCoupon
        };

        foreach (var product in inviteRecord.Promotion.Products)
        {
            rewardPromotion.Products.Add(product);
        }

        db.Promotions.Add(rewardPromotion);

        inviteRecord.RewardGiven = true;
        inviteRecord.Status = "rewarded";
        inviteRecord.RewardCoupon = rewardCoupon;
        inviteRecord.RewardedAt = evaluatedAt;

        return rewardCode;
    }
}