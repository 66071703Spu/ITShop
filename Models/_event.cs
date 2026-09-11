using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class _event
{
    public int event_id { get; set; }

    public string name { get; set; } = null!;

    public string? description { get; set; }

    public string? banner_url { get; set; }

    public DateTime? start_date { get; set; }

    public DateTime? end_date { get; set; }

    public string status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public DateTime updated_at { get; set; }
}
