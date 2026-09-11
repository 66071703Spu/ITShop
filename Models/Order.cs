using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class Order
{
    public int OrderId { get; set; }

    public string? OrderNumber { get; set; }

    public int UserId { get; set; }

    public decimal? TotalAmount { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal? ShippingFee { get; set; }

    public decimal? FinalAmount { get; set; }

    public string? Status { get; set; }

    public string? CancelReason { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();

    public virtual User User { get; set; } = null!;
}
