using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class Banner
{
    public int BannerId { get; set; }

    public string? Title { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string Position { get; set; } = null!;

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool? IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}
