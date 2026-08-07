namespace delosfera_server.Modules.Documents.VND.DTO.Response;

public class VndActualizationRecordResponse
{
    public int Id { get; set; }

    public int ResponsibleUserId { get; set; }
    public required string ResponsibleUserName { get; set; }

    public bool RequiresApproval { get; set; }
    public bool ShiftNextPeriod { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>null, пока цикл ещё не завершён (см. IsCompleted)</summary>
    public DateTime? PublishedAt { get; set; }
    public bool? HadChanges { get; set; }
    public DateOnly? DueActualizationDateBefore { get; set; }
    public DateOnly? DueActualizationDateAfter { get; set; }

    /// <summary>false — цикл ещё в процессе (документ сейчас в OnActualization/Consolidation)</summary>
    public bool IsCompleted { get; set; }
}