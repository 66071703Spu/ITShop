namespace ITShop.ViewModels
{
    public class HomeViewModel
    {
        public List<BrandViewModel> FeaturedBrands { get; set; } = new();
        public List<ProductViewModel> Promotions { get; set; } = new();
        public List<ProductViewModel> NewArrivals { get; set; } = new();
        public List<ProductViewModel> BestSellers { get; set; } = new();
        public List<ProductViewModel> CPUs { get; set; } = new();
        public List<ProductViewModel> GraphicsCards { get; set; } = new();
        public List<ProductViewModel> Laptops { get; set; } = new();
        public List<BannerViewModel> TopBanners { get; set; } = new();
        public List<BannerViewModel> MiddleBanners { get; set; } = new();
        public List<BannerViewModel> BottomBanners { get; set; } = new();
    }
}