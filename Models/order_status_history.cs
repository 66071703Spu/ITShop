using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class order_status_history
{
    public int status_history_id { get; set; }

    public int? order_id { get; set; }

    public string? status { get; set; }

    public DateTime? changed_at { get; set; }

    public virtual order? order { get; set; }
}
