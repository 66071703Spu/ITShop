using System.Reflection;
using System.Text.Json;

namespace ITShop.Helpers;

public static class CatalogProductImages
{
    private sealed record CatalogImage(string Name, string LocalPath);

    private static readonly Lazy<Dictionary<string, string>> KnownImages = new(() =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("ITShop.Data.catalog-image-sources.json")
            ?? throw new InvalidOperationException("Catalog image source list is missing.");
        var images = JsonSerializer.Deserialize<List<CatalogImage>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var urls = images.ToDictionary(image => image.Name, image => image.LocalPath,
            StringComparer.OrdinalIgnoreCase);
        urls["RTX 888"] = "/images/products/rtx-888.svg";
        urls["Short Url Test Product"] = "/images/products/short-url-test-product.svg";
        return urls;
    });

    public static string? GetImageUrl(string? productName) => productName != null
        && KnownImages.Value.TryGetValue(productName, out var imageUrl) ? imageUrl : null;
}
