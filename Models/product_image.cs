using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class product_image
{
    public int image_id { get; set; }

    public int? product_id { get; set; }

    public string? image_url { get; set; }

    public bool? is_main { get; set; }

    public virtual product? product { get; set; }
}
