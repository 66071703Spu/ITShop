using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class User
{
    public int UserId { get; set; }

    public string? InviteCode { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual ICollection<CouponRedemption> CouponRedemptions { get; set; } = new List<CouponRedemption>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<UserInvite> UserInviteInvitedUsers { get; set; } = new List<UserInvite>();

    public virtual ICollection<UserInvite> UserInviteInviterUsers { get; set; } = new List<UserInvite>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
