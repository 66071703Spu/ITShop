using System;
using System.Collections.Generic;

namespace ITShop.Models;

public partial class Permission
{
    public int PermissionId { get; set; }

    public string? PermissionName { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
