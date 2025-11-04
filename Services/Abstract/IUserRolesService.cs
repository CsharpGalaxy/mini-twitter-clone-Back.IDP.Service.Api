using keycloak_sample.Services.DTOs.Roles;
using static keycloak_sample.Services.Concrete.UserRolesManager;

namespace keycloak_sample.Services.Abstract;

public interface IUserRolesService
{
    Task<bool> AssignRealmRoleToUserAsync(string userId, string roleId, string roleName, CancellationToken ct);


    Task<bool> UnAssignmentRolesByUserId(string userId, List<RoleDto> rolesToRemove, CancellationToken ct);
    Task<List<RoleItemDto>> GetAllUsersRolesByUserId(string userId, CancellationToken ct);


}