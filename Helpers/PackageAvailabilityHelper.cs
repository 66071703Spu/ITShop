using ITShop.Models;
using Microsoft.EntityFrameworkCore;

namespace ITShop.Helpers;

public static class PackageAvailabilityHelper
{
    public static bool IsComset(Product product) =>
        string.Equals(product.Category?.Name, "Comset", StringComparison.OrdinalIgnoreCase);

    public static ProductPackage? FindPackage(Csi402dbContext db, Product product)
    {
        if (!IsComset(product))
        {
            return null;
        }

        var packages = db.ProductPackages
            .Include(package => package.PackageItems)
                .ThenInclude(item => item.Product)
            .AsQueryable();

        return packages.FirstOrDefault(package => package.PackageId == product.ProductId)
            ?? packages.FirstOrDefault(package => package.Name == product.Name);
    }

    public static int GetAvailableStock(Product product, ProductPackage? package)
    {
        var bundleStock = Math.Max(product.Stock ?? 0, 0);
        if (!IsComset(product))
        {
            return bundleStock;
        }

        if (package == null || package.PackageItems.Count == 0)
        {
            return 0;
        }

        var components = package.PackageItems.ToList();
        if (components.Any(item => item.Product == null || !item.ProductId.HasValue
            || (item.Quantity ?? 0) <= 0
            || string.Equals(item.Product.Status, "inactive", StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        foreach (var group in components.GroupBy(item => item.ProductId!.Value))
        {
            var neededPerBundle = group.Sum(item => item.Quantity!.Value);
            var componentStock = Math.Max(group.First().Product!.Stock ?? 0, 0);
            bundleStock = Math.Min(bundleStock, componentStock / neededPerBundle);
        }

        return bundleStock;
    }
}
