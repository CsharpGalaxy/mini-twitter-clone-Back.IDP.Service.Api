using Business.DTOs.Authentication;
using keycloak_sample.Services.Abstract;
using keycloak_sample.Services.DTOs.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace keycloak_sample.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class AuthController:ControllerBase
{
    private readonly IKeycloakService _keycloakService;

    public AuthController(IKeycloakService keycloakService)
    {
        _keycloakService = keycloakService;
    }

    [HttpGet("admintoken")]
    public async Task<IActionResult> GetTokenAdmin(CancellationToken cancellationToken)
    {
        try
        {
            string token = await _keycloakService.GetAdminTokenAsync(cancellationToken);
            return Ok(new { AccessToken = token });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { ex.Message });
        }
    }

    [HttpGet("token")]
  public async Task<IActionResult> GetToken(CancellationToken cancellationToken)
  {
      try
      {
       string token = await _keycloakService.GetAccessToken(cancellationToken);
       return Ok(new { AccessToken = token });
      }
      catch (ArgumentException ex)
      {
         return BadRequest(new { ex.Message });
      }
  }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto request, CancellationToken cancellationToken)
    {
        var (isSuccess, message) = await _keycloakService.RegisterAsync(request, cancellationToken);

        if (!isSuccess)
            return BadRequest(new { Message = message });
        
        return Ok(new { Message = message });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto requst, CancellationToken cancellationToken)
    {
        var (isSuccess, message) = await _keycloakService.LoginAsync(requst, cancellationToken);

        if (!isSuccess)
            return BadRequest(new { Message = message });

        return Ok(new { AccessToken = message});
    }
    
    [HttpGet("User")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        try
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return Unauthorized(new { message = "No token provided" });
            }

            var token = authHeader["Bearer ".Length..].Trim();
        
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { message = "Invalid token format" });
            }

            try
            {
                var userInfo = await _keycloakService.GetCurrentUserInfoAsync(token, cancellationToken);
                return Ok(new { 
                    user = userInfo.User,
                    roles = userInfo.Roles,
                    accessToken = token 
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Token is invalid or expired" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Internal server error", error = ex.Message });
        }
    }
}