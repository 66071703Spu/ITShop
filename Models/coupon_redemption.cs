using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class coupon_redemption
{
    public int redemption_id { get; set; }

    public int coupon_id { get; set; }

    public int user_id { get; set; }

    public int? order_id { get; set; }

    public DateTime? used_at { get; set; }

    public virtual coupon coupon { get; set; } = null!;

    public virtual user user { get; set; } = null!;
}
