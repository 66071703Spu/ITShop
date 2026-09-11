using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class user_invite
{
    public int invite_id { get; set; }

    public int inviter_user_id { get; set; }

    public string invite_code { get; set; } = null!;

    public string invited_email { get; set; } = null!;

    public int? invited_user_id { get; set; }

    public string status { get; set; } = null!;

    public bool reward_given { get; set; }

    public DateTime created_at { get; set; }

    public virtual user? invited_user { get; set; }

    public virtual user inviter_user { get; set; } = null!;
}
