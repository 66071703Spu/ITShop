using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class ProductPackage
{
    public int PackageId { get; set; }

    public string? Name { get; set; }

    public decimal? Price { get; set; }

    public virtual ICollection<PackageItem> PackageItems { get; set; } = new List<PackageItem>();
}
