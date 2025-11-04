using System.Text.Json.Serialization;

namespace keycloak_sample.Services.DTOs.Roles;

public sealed class CreateRoleDto
{
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = default!;
}