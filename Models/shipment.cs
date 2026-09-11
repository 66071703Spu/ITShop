using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class Shipment
{
    public int ShipmentId { get; set; }

    public int OrderId { get; set; }

    public int AddressId { get; set; }

    public string? TrackingNumber { get; set; }

    public string? ShippingProvider { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Address Address { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
