using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public string? PaymentMethod { get; set; }

    public string? TransactionRef { get; set; }

    public string? PaymentStatus { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual Order Order { get; set; } = null!;
}
