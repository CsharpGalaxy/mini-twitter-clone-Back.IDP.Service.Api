using Business.DTOs;
using keycloak_sample.Services.Abstract;
using keycloak_sample.Services.DTOs.Roles;
using keycloak_sample.Services.ServiceOptions;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;

namespace keycloak_sample.Services.Concrete;

public class RoleManager: IRoleService
{
    private readonly IOptions<KeycloakConfiguration> _options;
    private readonly HttpClient _httpClient;
    private readonly IKeycloakService _keycloakService;
    
    public RoleManager(IOptions<KeycloakConfiguration> options, IKeycloakService keycloakService)
    {
        _options = options;
        _httpClient = new HttpClient();
        _keycloakService = keycloakService;
    }




    public async Task<List<RoleDto>> GetAll(CancellationToken cancellationToken)
    {
        var accessToken = await _keycloakService.GetAdminTokenAsync(cancellationToken);

        var url = $"{_options.Value.HostName}/admin/realms/{_options.Value.Realm}/roles";


        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var roles = JsonSerializer.Deserialize<List<RoleDto>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return roles?.Any() == true ? roles : throw new Exception("لیست رول‌ها خالی است");
    }

    public async Task<RoleDto> GetByName(string name, CancellationToken cancellationToken)
    {
        string endpoint = $"{_options.Value.HostName}/admin/realms/{_options.Value.Realm}/roles/{name}";;
        
        var response = await _keycloakService.GetAsync<RoleDto>(endpoint , true , cancellationToken);

        if (response.Data == null)
            throw new Exception("Kullanıcı rolü bulunamadı");
        
        return response.Data;
        
    }

    public async Task<RoleDto> DeleteByName(string name, CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.Value.HostName}/admin/realms/{_options.Value.Realm}/roles/{name}";
        
        var response = await _keycloakService.DeleteAsync<RoleDto>(endpoint , true , cancellationToken);
        
        
        if (!response.IsSuccessful)
        {
            throw new Exception(response.ErrorMessages?.FirstOrDefault() ?? "Kullanıcı rolü silinemedi.");
        }
        
        return response.Data;
        
    }

    public async Task<string> Create(CreateRoleDto request, CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.Value.HostName}/admin/realms/{_options.Value.Realm}/roles";

        
        var response = await _keycloakService.PostAsync<string>(endpoint , request , true , cancellationToken );

        if (!response.IsSuccessful)
        {
            throw new Exception(response.ErrorMessages?.FirstOrDefault() ?? "Kullanıcı rolü eklenemedi.");
        }
        
        return response.Data;
        
    }
}