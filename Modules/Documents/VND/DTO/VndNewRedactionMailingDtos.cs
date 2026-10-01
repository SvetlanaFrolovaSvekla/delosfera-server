namespace delosfera_server.Modules.Documents.VND.DTO;

/// <summary>Получатель рассылки «Новые редакции ВНД» — для списка на странице настроек.</summary>
public class VndNewRedactionMailingRecipientResponse
{
    public int UserId { get; set; }
    public required string FullName { get; set; }
    public string? Email { get; set; }
    public string? OrgUnitName { get; set; }
    public string? PositionName { get; set; }

    /// <summary>Письмо такому получателю не уйдёт (заблокирован или отключён в каталоге) —
    /// показываем на странице, чтобы администратор видел, кто фактически выпадает из рассылки.</summary>
    public bool IsInactive { get; set; }
}

public class VndNewRedactionMailingResponse
{
    public bool Enabled { get; set; }
    public bool NotifyInApp { get; set; }
    public bool NotifyEmail { get; set; }
    public List<VndNewRedactionMailingRecipientResponse> Recipients { get; set; } = [];
}

/// <summary>Сохранение раздела целиком: флаги и полный состав получателей.</summary>
public class UpdateVndNewRedactionMailingRequest
{
    public bool Enabled { get; set; }
    public bool NotifyInApp { get; set; }
    public bool NotifyEmail { get; set; } = true;
    public List<int> UserIds { get; set; } = [];
}
