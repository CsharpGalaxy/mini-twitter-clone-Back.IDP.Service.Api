using Business.DTOs;
using keycloak_sample.Services.DTOs.Roles;

namespace keycloak_sample.Services.Abstract;

public interface IRoleService
{
    Task<List<RoleDto>> GetAll(CancellationToken cancellationToken);
    
    Task<RoleDto> GetByName(string name,CancellationToken cancellationToken);
    
    Task<RoleDto> DeleteByName(string name,CancellationToken cancellationToken);


    Task<string> Create(CreateRoleDto request,CancellationToken cancellationToken);


    
}