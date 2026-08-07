namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Компактный ответ по состоянию актуализации конкретного ВНД —
/// для start/confirm-start/publish. За полными реквизитами — обычный GetById.</summary>
public class VndActualizationStateResponse
{
    public int VndId { get; set; }
    public required string Status { get; set; } // "onact" | "consol" | "active" и т.д.

    public int? ActualizationResponsibleUserId { get; set; }
    public string? ActualizationResponsibleUserName { get; set; }
    public bool ActualizationRequiresApproval { get; set; }
    public bool ActualizationShiftNextPeriod { get; set; }

    public DateOnly? DueActualizationDate { get; set; }
    public DateOnly? LastActualizationDate { get; set; }
}