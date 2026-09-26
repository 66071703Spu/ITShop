namespace ITShop.ViewModels
{
    public class OrderViewModel
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal FinalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string ShippingProvider { get; set; } = string.Empty;
        public string ShippingStatus { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
        public string ShippingAddress { get; set; } = "-";
        public string? CancelReason { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? BrandSummary { get; set; }
        public List<OrderItemViewModel> Items { get; set; } = new();

        public bool CanCancel
        {
            get
            {
                var status = (Status ?? string.Empty).ToLower();
                var shippingStatus = (ShippingStatus ?? string.Empty).ToLower();
                return status is not ("paid" or "packed" or "shipped" or "in_transit" or "delivered" or "returned" or "cancelled")
                    && shippingStatus is not ("packed" or "shipped" or "in_transit" or "delivered" or "returned");
            }
        }
    }

    public class OrderItemViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? ImageUrl { get; set; }
    }
}
