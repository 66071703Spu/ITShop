using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class user_role
{
    public int user_role_id { get; set; }

    public int user_id { get; set; }

    public int role_id { get; set; }

    public virtual role role { get; set; } = null!;

    public virtual user user { get; set; } = null!;
}
