using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class inventory_transaction
{
    public int transaction_id { get; set; }

    public int? product_id { get; set; }

    public string? transaction_type { get; set; }

    public int? quantity { get; set; }

    public int? reference_order_id { get; set; }

    public DateTime? created_at { get; set; }

    public virtual product? product { get; set; }
}
