namespace ITShop.ViewModels
{
    public class AdminViewModel
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Address { get; set; }
        public List<string> Roles { get; set; } = new();

        // เตรียมชื่อ property สำรองเพื่อให้ view เก่ายังใช้งานต่อได้
        public string Username
        {
            get => Email;
            set => Email = value;
        }

        public string FullName
        {
            get => string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";
            set
            {
                var parts = (value ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                FirstName = parts.Length > 0 ? parts[0] : string.Empty;
                LastName = parts.Length > 1 ? parts[1] : string.Empty;
            }
        }

        public string Phone
        {
            get => PhoneNumber ?? string.Empty;
            set => PhoneNumber = value;
        }
    }

    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public int LowStockCount { get; set; }
        public List<AdminOrderViewModel> RecentOrders { get; set; } = new();
    }

    public class AdminOrderViewModel
    {
        public int OrderId { get; set; }
        public int? ShipmentId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string ShippingProvider { get; set; } = string.Empty;
        public string ShippingStatus { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
        public string ShippingAddress { get; set; } = "-";
        public DateTime? CreatedAt { get; set; }
        public bool HasShipment => ShipmentId.HasValue;
    }

    public class AdminShipmentUpdateViewModel
    {
        public int OrderId { get; set; }
        public int ShipmentId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = "-";
        public string ShippingProvider { get; set; } = string.Empty;
        public string ShippingStatus { get; set; } = string.Empty;
        public string? TrackingNumber { get; set; }
        public List<string> ShippingStatusOptions { get; set; } = new();
        public List<string> ShippingProviderOptions { get; set; } = new();
    }

    public class BrandViewModel
    {
        public int BrandId { get; set; }
        public string BrandName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public IFormFile? LogoFile { get; set; }
        public int ProductCount { get; set; }
        public string ProductSummary { get; set; } = string.Empty;
    }

    public class AdminUserFormViewModel
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Status { get; set; } = "active";
        public int SelectedRoleId { get; set; } = 1;
        public List<RoleOptionViewModel> RoleOptions { get; set; } = new();
    }

    public class RoleOptionViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }

    public class BackOfficeProfileViewModel
    {
        public AdminViewModel Profile { get; set; } = new();
        public string DefaultAddressPreview { get; set; } = "-";
        public string DefaultAddressFull { get; set; } = "-";
        public int AddressCount { get; set; }
        public int TotalOrders { get; set; }
        public int ActiveOrders { get; set; }
        public decimal LifetimeSpent { get; set; }
        public List<AddressItemViewModel> Addresses { get; set; } = new();
        public List<ProfileRecentOrderViewModel> RecentOrders { get; set; } = new();
        public List<CouponDisplayViewModel> AvailableCoupons { get; set; } = new();
        public List<CouponHistoryItemViewModel> CouponHistory { get; set; } = new();
        public List<ProfileInviteLinkViewModel> ReferralHistory { get; set; } = new();
        public List<string> Permissions { get; set; } = new();

        public string AvatarInitials
        {
            get
            {
                var first = string.IsNullOrWhiteSpace(Profile.FirstName) ? string.Empty : Profile.FirstName.Trim()[0].ToString().ToUpperInvariant();
                var last = string.IsNullOrWhiteSpace(Profile.LastName) ? string.Empty : Profile.LastName.Trim()[0].ToString().ToUpperInvariant();
                return string.IsNullOrWhiteSpace(first + last) ? "U" : first + last;
            }
        }
    }
}