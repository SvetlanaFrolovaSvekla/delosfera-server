namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>PUT /api/vnd/{vndId}/secrecy — настройки секретности документа.</summary>
public class UpdateVndSecrecyRequest
{
    /// <summary>Сделать документ секретным (false — снять секретность; списки при этом сохраняются).</summary>
    public bool IsSecret { get; set; }

    /// <summary>Группы пользователей из справочника, которым открыт документ.</summary>
    public List<int> GroupIds { get; set; } = [];

    /// <summary>Дополнительные отдельные пользователи, которым открыт документ.</summary>
    public List<int> UserIds { get; set; } = [];
}
