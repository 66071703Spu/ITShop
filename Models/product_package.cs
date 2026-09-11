using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class product_package
{
    public int package_id { get; set; }

    public string? name { get; set; }

    public decimal? price { get; set; }

    public virtual ICollection<package_item> package_items { get; set; } = new List<package_item>();
}
