using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class PackageItem
{
    public int Id { get; set; }

    public int? PackageId { get; set; }

    public int? ProductId { get; set; }

    public int? Quantity { get; set; }

    public virtual ProductPackage? Package { get; set; }

    public virtual Product? Product { get; set; }
}
