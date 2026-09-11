using Microsoft.AspNetCore.Http;

namespace ITShop.ViewModels
{
    public class BannerViewModel
    {
        public int BannerId { get; set; }
        public string? Title { get; set; }
        public string? ImageUrl { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string Position { get; set; } = "home_top";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; } = 1;
        public DateTime? CreatedAt { get; set; }
    }
}
