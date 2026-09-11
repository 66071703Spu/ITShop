using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class role_permission
{
    public int role_permission_id { get; set; }

    public int? role_id { get; set; }

    public int? permission_id { get; set; }

    public virtual permission? permission { get; set; }

    public virtual role? role { get; set; }
}
