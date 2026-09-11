using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class OrderStatusHistory
{
    public int StatusHistoryId { get; set; }

    public int? OrderId { get; set; }

    public string? Status { get; set; }

    public DateTime? ChangedAt { get; set; }

    public virtual Order? Order { get; set; }
}
