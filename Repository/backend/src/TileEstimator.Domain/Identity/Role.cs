using TileEstimator.Domain.Common;

namespace TileEstimator.Domain.Identity;

/// <summary>SPEC 6 role. Roles are system-defined: Owner, Admin, Estimator, Sales, Installer, Viewer.</summary>
public class Role : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public ICollection<RolePermission> Permissions { get; private set; } = new List<RolePermission>();

    private Role() { }

    public static Role Create(Guid id, string name, string description, int sortOrder) => new()
    {
        Id = id,
        Name = name,
        Description = description,
        SortOrder = sortOrder
    };
}

/// <summary>SPEC 6 granular permission, for example estimate.update.</summary>
public class Permission : Entity
{
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Group { get; private set; } = string.Empty;

    private Permission() { }

    public static Permission Create(Guid id, string code, string group, string description) => new()
    {
        Id = id,
        Code = code,
        Group = group,
        Description = description
    };
}

/// <summary>Join row that makes up the role-to-permission matrix in docs/authorization.md.</summary>
public class RolePermission : Entity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Role? Role { get; private set; }
    public Permission? Permission { get; private set; }

    private RolePermission() { }

    public static RolePermission Create(Guid roleId, Guid permissionId) => new()
    {
        Id = Guid.NewGuid(),
        RoleId = roleId,
        PermissionId = permissionId
    };
}
