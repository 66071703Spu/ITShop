using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class cart_item
{
    public int cart_item_id { get; set; }

    public int? cart_id { get; set; }

    public int? product_id { get; set; }

    public int? quantity { get; set; }

    public virtual cart? cart { get; set; }

    public virtual product? product { get; set; }
}
