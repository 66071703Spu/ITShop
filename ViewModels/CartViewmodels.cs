namespace ITShop.ViewModels
{
    public class CartViewModel
    {
        public int CartId { get; set; }
        public int CartItemId { get; set; }
        public int? UserId { get; set; }
        public int? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
        public int? Stock { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime? AddedAt { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool HasAutoAppliedPromotion { get; set; }
        public string? ActivePromotionName { get; set; }
        public string? DiscountLabel { get; set; }

        public decimal OriginalSubTotal => OriginalPrice * Quantity;
        public decimal SubTotal => Price * Quantity;
    }

    public class CheckoutAddressOptionViewModel
    {
        public int AddressId { get; set; }
        public string AddressLine { get; set; } = string.Empty;
        public string FullAddress { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class ShippingProviderOptionViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}