namespace ITShop.ViewModels;

public class SuperAdminDashboardViewModel
{
    public int TotalCustomers { get; set; }
    public int TotalAdmins { get; set; }
    public int TotalSuperAdmins { get; set; }
    public int TotalPermissions { get; set; }
    public List<AdminViewModel> RecentAdmins { get; set; } = new();
}

public class RolePermissionMatrixViewModel
{
    public List<RolePermissionRowViewModel> Roles { get; set; } = new();
    public List<PermissionColumnViewModel> Permissions { get; set; } = new();
}

public class RolePermissionBulkUpdateViewModel
{
    public List<RolePermissionUpdateItemViewModel> Roles { get; set; } = new();
}

public class RolePermissionUpdateItemViewModel
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public List<int> SelectedPermissionIds { get; set; } = new();
}

public class RolePermissionRowViewModel
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public List<int> SelectedPermissionIds { get; set; } = new();
    public List<PermissionColumnViewModel> AvailablePermissions { get; set; } = new();
    public string EmptyStateMessage { get; set; } = string.Empty;
}

public class PermissionColumnViewModel
{
    public int PermissionId { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}