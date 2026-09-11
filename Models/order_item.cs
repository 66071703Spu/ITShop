using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class order_item
{
    public int order_item_id { get; set; }

    public int order_id { get; set; }

    public int product_id { get; set; }

    public int quantity { get; set; }

    public decimal unit_price { get; set; }

    public decimal total_price { get; set; }

    public virtual order order { get; set; } = null!;

    public virtual product product { get; set; } = null!;
}
