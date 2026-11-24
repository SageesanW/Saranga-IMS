namespace Saranga.Application.DTOs.Roles;

public record RoleDto(
    Guid Id,
    string Name,
    string Description,
    bool IsSystemRole,
    List<string> Permissions,
    DateTimeOffset CreatedAt
);

public record CreateRoleRequest(
    string Name,
    string Description,
    List<Guid> PermissionIds
);

public record UpdateRoleRequest(
    string Description,
    List<Guid> PermissionIds
);

public record AssignRolePermissionsRequest(
    List<Guid> PermissionIds
);

public record PermissionDto(
    Guid Id,
    string Name,
    string Description,
    string Module
);
