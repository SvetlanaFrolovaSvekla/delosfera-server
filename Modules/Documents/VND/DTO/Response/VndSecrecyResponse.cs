namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndSecrecyResponse
{
    public bool IsSecret { get; set; }
    public List<int> GroupIds { get; set; } = [];
    public List<VndSecrecyUserResponse> Users { get; set; } = [];
}

public class VndSecrecyUserResponse
{
    public int UserId { get; set; }
    public required string FullName { get; set; }
    public string? Email { get; set; }
    public string? OrgUnitName { get; set; }
    public string? PositionName { get; set; }

    /// <summary>Заблокирован или отключён — доступ формально есть, но войти он не сможет.</summary>
    public bool IsInactive { get; set; }
}
