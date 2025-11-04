namespace Business.DTOs.Roles;

public class RealmRoleModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool Composite { get; set; }
    public bool ClientRole { get; set; }
    public string ContainerId { get; set; }
}
