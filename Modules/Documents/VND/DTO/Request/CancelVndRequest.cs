namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Данные для архивации (отмены) ВНД — кнопка "Архивировать" (см. VndService.CancelAsync
/// и PermissionCode.CancelVnd). № и дата отмены обязательны — отмена оформляется служебной
/// запиской и не согласуется; причина — по желанию.</summary>
public class CancelVndRequest
{
    /// <summary>№ отмены (реквизит служебной записки, которой оформлена отмена)</summary>
    public required string CancelCode { get; set; }

    /// <summary>Дата отмены</summary>
    public required DateOnly CancelDate { get; set; }

    /// <summary>Причина отмены — необязательна</summary>
    public string? CancelReason { get; set; }
}
