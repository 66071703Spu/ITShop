using ITShop.Models;

namespace ITShop.Helpers;

public static class PromotionPriceCalculator
{
    // คำนวณราคาที่ดีที่สุดจากโปรโมชัน auto-apply ทั้งหมดที่ใช้กับสินค้านี้ได้ในตอนนี้
    public static PromotionPriceResult Calculate(Product product, DateTime? now = null)
    {
        var evaluatedAt = now ?? DateTime.Now;
        var originalPrice = product.Price;

        var activePromotions = GetAutoApplyPromotions(product, evaluatedAt);

        if (activePromotions.Count == 0)
        {
            return new PromotionPriceResult(originalPrice, originalPrice, null, null);
        }

        PromotionPriceResult? bestResult = null;
        foreach (var promotion in activePromotions)
        {
            var finalPrice = CalculateDiscountedPrice(originalPrice, promotion);
            var candidate = new PromotionPriceResult(
                originalPrice,
                finalPrice,
                promotion.Name,
                BuildDiscountLabel(promotion));

            if (bestResult == null || candidate.FinalPrice < bestResult.FinalPrice)
            {
                bestResult = candidate;
            }
        }

        return bestResult ?? new PromotionPriceResult(originalPrice, originalPrice, null, null);
    }

    // ตรวจแบบเร็วว่าสินค้านี้มีโปรโมชัน auto-apply ที่ใช้ได้อยู่หรือไม่
    public static bool HasAutoApplyPromotion(Product product, DateTime? now = null)
    {
        return GetAutoApplyPromotions(product, now ?? DateTime.Now).Count > 0;
    }

    // คัดเฉพาะโปรโมชันที่เป็น auto-apply และยังอยู่ในช่วงเวลาที่ใช้งานได้จริง
    private static List<Promotion> GetAutoApplyPromotions(Product product, DateTime evaluatedAt)
    {
        return product.Promotions
            .Where(p => p.IsAutoApply == true)
            .Where(p => !string.Equals((p.PromotionType ?? string.Empty).Trim(), "coupon", StringComparison.OrdinalIgnoreCase))
            .Where(p => !string.Equals((p.PromotionType ?? string.Empty).Trim(), "invite", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.StartDate.HasValue || p.StartDate <= evaluatedAt)
            .Where(p => !p.EndDate.HasValue || p.EndDate >= evaluatedAt)
            .ToList();
    }

    // คำนวณราคาหลังหักส่วนลดของโปรโมชันหนึ่งตัวพร้อมกันค่าติดลบ
    private static decimal CalculateDiscountedPrice(decimal originalPrice, Promotion promotion)
    {
        var discountedPrice = originalPrice - CalculateDiscountAmount(originalPrice, promotion.DiscountType, promotion.DiscountValue);

        return Math.Round(Math.Max(0m, discountedPrice), 2, MidpointRounding.AwayFromZero);
    }

    // คำนวณจำนวนเงินส่วนลดจากประเภท percent หรือ fixed ให้อยู่ในช่วงที่ปลอดภัย
    public static decimal CalculateDiscountAmount(decimal baseAmount, string? discountType, decimal? discountValue)
    {
        var normalizedType = (discountType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedValue = discountValue ?? 0m;

        var discountAmount = normalizedType switch
        {
            "percent" => baseAmount * (normalizedValue / 100m),
            "fixed" => normalizedValue,
            _ => 0m
        };

        return Math.Round(Math.Max(0m, Math.Min(baseAmount, discountAmount)), 2, MidpointRounding.AwayFromZero);
    }

    // สร้างข้อความป้ายส่วนลดเพื่อแสดงบน UI ของสินค้าและคูปอง
    public static string? BuildDiscountLabel(string? discountType, decimal? discountValue)
    {
        var normalizedType = (discountType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedValue = discountValue ?? 0m;

        return normalizedType switch
        {
            "percent" => $"-{normalizedValue:0.##}%",
            "fixed" => $"-{normalizedValue:0.##} THB",
            _ => null
        };
    }

    // overload สำหรับดึงป้ายส่วนลดจาก entity โปรโมชันโดยตรง
    private static string? BuildDiscountLabel(Promotion promotion)
    {
        return BuildDiscountLabel(promotion.DiscountType, promotion.DiscountValue);
    }
}

// เก็บผลลัพธ์ราคาก่อนลด หลังลด และข้อมูลป้ายโปรโมชันไว้รวมกันใน object เดียว
public sealed record PromotionPriceResult(
    decimal OriginalPrice,
    decimal FinalPrice,
    string? PromotionName,
    string? DiscountLabel)
{
    // บอกว่าสุดท้ายราคาถูกลดลงจริงหรือไม่
    public bool HasDiscount => FinalPrice < OriginalPrice;
    // คืนมูลค่าส่วนต่างที่ลดลงจากราคาเดิม
    public decimal DiscountAmount => Math.Max(0m, OriginalPrice - FinalPrice);
}