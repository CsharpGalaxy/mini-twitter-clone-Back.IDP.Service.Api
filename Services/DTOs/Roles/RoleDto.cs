using System.Text.Json.Serialization;

namespace keycloak_sample.Services.DTOs.Roles;

public sealed class RoleDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = default!;



}
