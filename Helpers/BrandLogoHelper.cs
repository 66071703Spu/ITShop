using System.Text;
using System.Text.Json;
using ITShop.Models;

namespace ITShop.Helpers;

public static class BrandLogoHelper
{
    private static readonly Lazy<Dictionary<string, string>> DefaultLogoUrls = new(() =>
    {
        using var stream = typeof(BrandLogoHelper).Assembly
            .GetManifestResourceStream("ITShop.Data.brand-logo-sources.json")
            ?? throw new InvalidOperationException("Brand logo manifest is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateArray().ToDictionary(
            item => item.GetProperty("name").GetString()!,
            item => "/images/brands/" + item.GetProperty("file").GetString()!,
            StringComparer.OrdinalIgnoreCase);
    });

    // รายชื่อแบรนด์ตั้งต้นที่ใช้ seed ระบบให้มีข้อมูลพร้อมใช้งาน
    private static readonly string[] DefaultBrands =
    {
        "AMD",
        "Intel",
        "NVIDIA",
        "ASUS",
        "MSI",
        "Gigabyte",
        "Corsair",
        "G.Skill",
        "Kingston",
        "Samsung",
        "Western Digital",
        "Seagate",
        "Seasonic",
        "Cooler Master",
        "NZXT",
        "Noctua",
        "Lian Li",
        "LG",
        "Lenovo",
        "Logitech",
        "Razer",
        "SteelSeries",
        "Keychron",
        "HyperX",
        "Elgato"
    };

    // เติมแบรนด์และโลโก้ตัวอย่าง โดยคงโลโก้ที่ผู้ดูแลอัปโหลดเองไว้
    public static void SeedDefaultBrands(Csi402dbContext db)
    {
        var existingBrands = db.Brands.ToList();

        foreach (var brandName in DefaultBrands)
        {
            var logoUrl = DefaultLogoUrls.Value.GetValueOrDefault(brandName);
            var brand = existingBrands.FirstOrDefault(b => string.Equals(b.BrandName, brandName, StringComparison.OrdinalIgnoreCase));

            if (brand == null)
            {
                db.Brands.Add(new Brand
                {
                    BrandName = brandName,
                    LogoUrl = logoUrl
                });
            }
            else if (string.IsNullOrWhiteSpace(brand.LogoUrl)
                || brand.LogoUrl.Equals("/uploads/brands/bulk-brand.png", StringComparison.OrdinalIgnoreCase))
            {
                brand.LogoUrl = logoUrl;
            }
        }

        db.SaveChanges();
    }

    // แปลงชื่อแบรนด์ให้เป็น slug ที่เหมาะกับการใช้เป็นส่วนหนึ่งของชื่อไฟล์
    public static string SlugifyBrandName(string value)
    {
        var builder = new StringBuilder();

        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length == 0 || builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }
}
