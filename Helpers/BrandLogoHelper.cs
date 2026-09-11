using System.Text;
using ITShop.Models;

namespace ITShop.Helpers;

public static class BrandLogoHelper
{
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

    // เติมแบรนด์มาตรฐานลงฐานข้อมูลเมื่อยังไม่มีรายการนั้นอยู่
    public static void SeedDefaultBrands(Csi402dbContext db)
    {
        var existingBrands = db.Brands.ToList();

        foreach (var brandName in DefaultBrands)
        {
            var brand = existingBrands.FirstOrDefault(b => string.Equals(b.BrandName, brandName, StringComparison.OrdinalIgnoreCase));

            if (brand == null)
            {
                db.Brands.Add(new Brand
                {
                    BrandName = brandName
                });
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