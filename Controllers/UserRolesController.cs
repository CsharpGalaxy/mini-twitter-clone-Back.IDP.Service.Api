using keycloak_sample.Services.Abstract;
using keycloak_sample.Services.DTOs.Roles;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserRolesController:ControllerBase
{
    private readonly IUserRolesService _userRolesService;
    
    public UserRolesController(IUserRolesService userRolesService)
    {
        _userRolesService = userRolesService;
    }
    
    [HttpPost("{id}/roles")]
    public async Task<IActionResult> AssignmentRolesByUserId(Guid id, RoleDto request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _userRolesService.AssignRealmRoleToUserAsync(id.ToString(), request.Id.ToString(),request.Name, cancellationToken);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { ModelState = ex.Message });
        }
    }
    
    [HttpDelete("{id}/roles")]
    public async Task<IActionResult> UnAssignmentRolesByUserId(Guid id, List<RoleDto> request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _userRolesService.UnAssignmentRolesByUserId(id.ToString(), request, cancellationToken);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { ModelState = ex.Message });
        }
    }
    
    [HttpGet("{id}/roles")]
    public async Task<IActionResult> GetAllUsersRolesByUserId(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _userRolesService.GetAllUsersRolesByUserId(id.ToString(), cancellationToken);

            if (response == null)
            {
                return NotFound(new  { Message = "notfound."});
            }
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { ModelState = ex.Message });
        }
    }
    
}