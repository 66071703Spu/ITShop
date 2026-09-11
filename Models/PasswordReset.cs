using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class PasswordReset
{
    public int ResetId { get; set; }

    public string Email { get; set; } = null!;

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
