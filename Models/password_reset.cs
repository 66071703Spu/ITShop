using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class password_reset
{
    public int reset_id { get; set; }

    public string email { get; set; } = null!;

    public string token_hash { get; set; } = null!;

    public DateTime expires_at { get; set; }

    public DateTime? used_at { get; set; }

    public DateTime created_at { get; set; }
}
