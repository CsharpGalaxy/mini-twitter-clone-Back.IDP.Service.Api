using Business.DTOs.Roles;
using keycloak_sample.Services.Abstract;
using keycloak_sample.Services.DTOs.Roles;
using keycloak_sample.Services.ServiceOptions;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace keycloak_sample.Services.Concrete;

public class UserRolesManager: IUserRolesService
{
    private readonly IOptions<KeycloakConfiguration> _options;
    private readonly HttpClient _httpClient;
    private readonly IKeycloakService _keycloakService;
    
    public UserRolesManager(IOptions<KeycloakConfiguration> options, IKeycloakService keycloakService)
    {
        _options = options;
        _httpClient = new HttpClient();
        _keycloakService = keycloakService;
    }


    public async Task<bool> AssignRealmRoleToUserAsync(string userId, string roleId, string roleName,CancellationToken ct)
    {
        var url = $"{_options.Value.HostName}/admin/realms/{_options.Value.Realm}/users/{userId}/role-mappings/realm";

        var rolePayload = new[]
        {
            new
            {
                id = roleId,
                name = roleName,
                composite = true,
                clientRole = false,
                containerId = _options.Value.Realm
            }
        };

        var json = JsonSerializer.Serialize(rolePayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",await _keycloakService.GetAdminTokenAsync(ct));

        var response = await _httpClient.PostAsync(url, content);
        return response.IsSuccessStatusCode;
    }



    public async Task<bool> UnAssignmentRolesByUserId(string userId, List<RoleDto> rolesToRemove, CancellationToken ct)
    {
        var url = $"{_options.Value.HostName}/admin/realms/{_options.Value.Realm}/users/{userId}/role-mappings/realm";

        var payload = rolesToRemove.Select(role => new
        {
            id = role.Id,
            name = role.Name,
            composite = true,
            clientRole = false,
            containerId =  _options.Value.Realm
    });

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _keycloakService.GetAdminTokenAsync(ct));

        var response = await _httpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Delete,
            RequestUri = new Uri(_httpClient.BaseAddress + url),
            Content = content
        });

        return response.IsSuccessStatusCode;
    }

    public async Task<List<RoleItemDto>> GetAllUsersRolesByUserId(string userId, CancellationToken ct)
    {
        var url = $"{_options.Value.HostName}/admin/realms/{_options.Value.Realm}/users/{userId}/role-mappings/realm";

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await _keycloakService.GetAdminTokenAsync(ct));

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return new List<RoleItemDto>();

        var json = await response.Content.ReadAsStringAsync();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var roles = JsonSerializer.Deserialize<List<RealmRoleModel>>(json, options);

        return roles?
            .Select(r => new RoleItemDto { Name = r.Name, Id = r.Id })
            .ToList() ?? new List<RoleItemDto>();
    }

  
}
