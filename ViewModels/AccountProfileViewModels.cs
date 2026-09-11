using System.ComponentModel.DataAnnotations;

namespace ITShop.ViewModels;

public class ProfileDashboardViewModel
{
    public SignupViewModel Profile { get; set; } = new();
    public string DefaultAddressPreview { get; set; } = "-";
    public string DefaultAddressFull { get; set; } = "-";
    public int AddressCount { get; set; }
    public int TotalOrders { get; set; }
    public int ActiveOrders { get; set; }
    public decimal LifetimeSpent { get; set; }
    public List<ProfileRecentOrderViewModel> RecentOrders { get; set; } = new();
    public List<CouponDisplayViewModel> AvailableCoupons { get; set; } = new();
    public List<CouponHistoryItemViewModel> CouponHistory { get; set; } = new();
    public ProfileInviteOverviewViewModel InviteOverview { get; set; } = new();
    public List<ProfileInviteLinkViewModel> ReferralHistory { get; set; } = new();

    public string AvatarInitials
    {
        get
        {
            var first = string.IsNullOrWhiteSpace(Profile.FirstName) ? string.Empty : Profile.FirstName.Trim()[0].ToString().ToUpperInvariant();
            var last = string.IsNullOrWhiteSpace(Profile.LastName) || Profile.LastName == "-"
                ? string.Empty
                : Profile.LastName.Trim()[0].ToString().ToUpperInvariant();

            return string.IsNullOrWhiteSpace(first + last) ? "U" : first + last;
        }
    }
}

public class ProfileRecentOrderViewModel
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string Status { get; set; } = "pending";
    public decimal FinalAmount { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ItemSummary { get; set; } = string.Empty;
    public int ItemCount { get; set; }
}

public class EditProfileViewModel
{
    public int UserId { get; set; }

    [Required]
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
}

public class ManageAddressesViewModel
{
    public List<AddressItemViewModel> Addresses { get; set; } = new();
    public AddressInputViewModel NewAddress { get; set; } = new();
}

public class AddressItemViewModel
{
    public int AddressId { get; set; }
    public string AddressLine { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public bool IsDefault { get; set; }

    public string FullAddress
    {
        get => string.IsNullOrWhiteSpace(AddressLine) ? "-" : AddressLine.Trim();
    }
}

public class AddressInputViewModel
{
    [Required]
    public string AddressLine { get; set; } = string.Empty;

    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public bool IsDefault { get; set; }
}

public class ChangePasswordViewModel
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileInvitePromotionViewModel
{
    public int PromotionId { get; set; }
    public string PromotionName { get; set; } = string.Empty;
    public string RewardLabel { get; set; } = string.Empty;
    public string ApplicableProductSummary { get; set; } = string.Empty;
    public decimal? MinimumOrderSubtotal { get; set; }
}

public class ProfileInviteOverviewViewModel
{
    public string InviteCode { get; set; } = string.Empty;
    public string InviteLink { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public string RewardLabel { get; set; } = string.Empty;
    public string ApplicableProductSummary { get; set; } = string.Empty;
    public decimal? MinimumOrderSubtotal { get; set; }
    public bool IsRewardActive { get; set; }
}

public class ProfileInviteLinkViewModel
{
    public string InviteCode { get; set; } = string.Empty;
    public string InviteLink { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? InvitedEmail { get; set; }
    public string? RewardCouponCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RewardedAt { get; set; }
}