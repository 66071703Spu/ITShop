using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class InventoryTransaction
{
    public int TransactionId { get; set; }

    public int? ProductId { get; set; }

    public string? TransactionType { get; set; }

    public int? Quantity { get; set; }

    public int? ReferenceOrderId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Product? Product { get; set; }
}
