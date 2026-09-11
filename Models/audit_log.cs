using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class audit_log
{
    public int log_id { get; set; }

    public int? user_id { get; set; }

    public string? action { get; set; }

    public string? entity_name { get; set; }

    public int? entity_id { get; set; }

    public DateTime? created_at { get; set; }

    public virtual user? user { get; set; }
}
