using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class package_item
{
    public int id { get; set; }

    public int? package_id { get; set; }

    public int? product_id { get; set; }

    public int? quantity { get; set; }

    public virtual product_package? package { get; set; }

    public virtual product? product { get; set; }
}
