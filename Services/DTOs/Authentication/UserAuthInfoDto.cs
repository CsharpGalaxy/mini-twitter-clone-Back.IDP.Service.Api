using keycloak_sample.Services.DTOs.Roles;
using keycloak_sample.Services.DTOs.Users;

namespace keycloak_sample.Services.DTOs.Authentication;

public class UserAuthInfoDto
{
    public UserDto User { get; set; } = default!;
    public List<RoleDto> Roles { get; set; } = default!;
    public string AccessToken { get; set; } = default!;
}