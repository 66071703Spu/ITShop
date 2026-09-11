using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class Register
{
    public string RegisUsername { get; set; } = null!;

    public string RegisFullName { get; set; } = null!;

    public string RegisEmail { get; set; } = null!;

    public string? RegisPhone { get; set; }

    public string? RegisAddress { get; set; }
}
