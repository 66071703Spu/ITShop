using ITShop.Models;
using ITShop.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ITShop.Helpers;

public static class CouponPromotionHelper
{
    // คืนรายการคูปองที่ user ควรมองเห็นตามเวลา เจ้าของคูปอง และบริบทสินค้าที่กำลังซื้อ
    public static List<CouponDisplayViewModel> GetVisibleCoupons(
        Csi402dbContext db,
        int? userId,
        IEnumerable<CouponContextItemViewModel>? contextItems = null,
        DateTime? now = null)
    {
        var evaluatedAt = now ?? DateTime.Now;
        var normalizedContextItems = contextItems?
            .Where(item => item.ProductId > 0)
            .ToList() ?? new List<CouponContextItemViewModel>();
        var productIds = normalizedContextItems
            .Select(item => item.ProductId)
            .ToHashSet();

        var promotions = db.Promotions
            .Include(p => p.Coupon)
                .ThenInclude(c => c!.CouponRedemptions)
            .Include(p => p.Products)
            .Where(p => (p.PromotionType ?? string.Empty).ToLower() == "coupon")
            .Where(p => p.CouponId.HasValue && p.Coupon != null && p.Coupon.Code != null)
            .OrderBy(p => p.StartDate ?? DateTime.MinValue)
            .ThenBy(p => p.Name)
            .ToList();

        return promotions
            .Select(promotion => MapCoupon(promotion, userId, normalizedContextItems, productIds, evaluatedAt))
            .Where(coupon => coupon != null)
            .Select(coupon => coupon!)
            .OrderByDescending(coupon => coupon.IsCurrentlyEligible)
            .ThenBy(coupon => coupon.MinimumSpendRemaining > 0 ? 1 : 0)
            .ThenBy(coupon => coupon.Code)
            .ToList();
    }

    // แปลง promotion แบบ coupon ให้เป็นข้อมูลแสดงผลพร้อมตรวจเงื่อนไขการใช้งานครบชุด
    private static CouponDisplayViewModel? MapCoupon(
        Promotion promotion,
        int? userId,
        IReadOnlyCollection<CouponContextItemViewModel> contextItems,
        IReadOnlySet<int> productIds,
        DateTime evaluatedAt)
    {
        var coupon = promotion.Coupon;
        if (coupon == null || string.IsNullOrWhiteSpace(coupon.Code))
        {
            return null;
        }

        if (coupon.OwnerUserId.HasValue && (!userId.HasValue || coupon.OwnerUserId.Value != userId.Value))
        {
            return null;
        }

        if ((promotion.StartDate.HasValue && promotion.StartDate > evaluatedAt)
            || (promotion.EndDate.HasValue && promotion.EndDate < evaluatedAt))
        {
            return null;
        }

        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate < evaluatedAt)
        {
            return null;
        }

        if (coupon.UsageLimit.HasValue && coupon.CouponRedemptions.Count >= coupon.UsageLimit.Value)
        {
            return null;
        }

        if (userId.HasValue && coupon.CouponRedemptions.Any(redemption => redemption.UserId == userId.Value))
        {
            return null;
        }

        var appliesToAllProducts = !promotion.Products.Any();
        var matchesCurrentProducts = productIds.Count == 0
            || appliesToAllProducts
            || promotion.Products.Any(product => productIds.Contains(product.ProductId));

        if (productIds.Count > 0 && !matchesCurrentProducts)
        {
            return null;
        }

        var eligibleSubtotal = CalculateEligibleSubtotal(promotion, appliesToAllProducts, contextItems);
        var minimumOrderSubtotal = promotion.MinimumOrderSubtotal;
        var minimumSpendRemaining = minimumOrderSubtotal.HasValue
            ? Math.Max(0m, minimumOrderSubtotal.Value - eligibleSubtotal)
            : 0m;
        var isCurrentlyEligible = productIds.Count == 0
            ? !minimumOrderSubtotal.HasValue
            : minimumSpendRemaining <= 0m;

        return new CouponDisplayViewModel
        {
            PromotionId = promotion.PromotionId,
            CouponId = coupon.CouponId,
            Code = coupon.Code,
            PromotionName = promotion.Name ?? "Coupon Promotion",
            DiscountType = promotion.DiscountType ?? string.Empty,
            DiscountValue = promotion.DiscountValue ?? 0m,
            DiscountLabel = PromotionPriceCalculator.BuildDiscountLabel(promotion.DiscountType, promotion.DiscountValue) ?? "-",
            MinimumOrderSubtotal = minimumOrderSubtotal,
            EligibleSubtotal = eligibleSubtotal,
            MinimumSpendRemaining = minimumSpendRemaining,
            UsageLimit = coupon.UsageLimit,
            UsedCount = coupon.CouponRedemptions.Count,
            AppliesToAllProducts = appliesToAllProducts,
            ApplicableProductCount = promotion.Products.Count,
            ApplicableProductSummary = BuildApplicableProductSummary(promotion, appliesToAllProducts),
            IsCurrentlyEligible = isCurrentlyEligible,
            EligibilityMessage = BuildEligibilityMessage(appliesToAllProducts, minimumOrderSubtotal, minimumSpendRemaining, productIds.Count > 0)
        };
    }

    // คำนวณยอด subtotal ของสินค้าที่เข้าเงื่อนไขคูปองเท่านั้น
    private static decimal CalculateEligibleSubtotal(
        Promotion promotion,
        bool appliesToAllProducts,
        IReadOnlyCollection<CouponContextItemViewModel> contextItems)
    {
        if (contextItems.Count == 0)
        {
            return 0m;
        }

        if (appliesToAllProducts)
        {
            return contextItems.Sum(item => item.Subtotal);
        }

        var promotionProductIds = promotion.Products
            .Select(product => product.ProductId)
            .ToHashSet();

        return contextItems
            .Where(item => promotionProductIds.Contains(item.ProductId))
            .Sum(item => item.Subtotal);
    }

    // สรุปรายชื่อสินค้าที่คูปองนี้ครอบคลุมเพื่อใช้แสดงใน UI
    private static string BuildApplicableProductSummary(Promotion promotion, bool appliesToAllProducts)
    {
        if (appliesToAllProducts)
        {
            return "ใช้ได้ทั้งร้าน";
        }

        var names = promotion.Products
            .OrderBy(product => product.Name)
            .Select(product => product.Name)
            .ToList();

        if (names.Count == 0)
        {
            return "-";
        }

        return string.Join(", ", names.Take(3)) + (names.Count > 3 ? " ..." : string.Empty);
    }

    // สร้างข้อความอธิบายว่าคูปองพร้อมใช้หรือยังขาดเงื่อนไขอะไรอยู่
    private static string BuildEligibilityMessage(bool appliesToAllProducts, decimal? minimumOrderSubtotal, decimal minimumSpendRemaining, bool hasContext)
    {
        if (minimumOrderSubtotal.HasValue && minimumSpendRemaining > 0m)
        {
            return $"ซื้อสินค้าเพิ่มอีก {minimumSpendRemaining:N2} บาท เพื่อใช้คูปองนี้";
        }

        if (minimumOrderSubtotal.HasValue && !hasContext)
        {
            return $"ขั้นต่ำ {minimumOrderSubtotal.Value:N2} บาท";
        }

        return appliesToAllProducts ? "พร้อมใช้ได้ทั้งร้าน" : "พร้อมใช้กับสินค้าที่ร่วมรายการ";
    }
}