namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Шаг "Выполнить актуализацию" — только для цикла, начатого напрямую главным редактором
/// (StartAsync); фиксирует финальные ShiftNextPeriod/PlannedNoChanges после того, как документ уже
/// перешёл в статус "На актуализации". Для пути "по заявке" этот шаг совмещён со стартом —
/// см. ConfirmActualizationStartRequest.</summary>
public class PerformActualizationRequest
{
    /// <summary>Сдвигать ли DueActualizationDate после публикации текущего цикла</summary>
    public required bool ShiftNextPeriod { get; set; }

    /// <summary>Планируется ли актуализация без изменений документа</summary>
    public required bool PlannedNoChanges { get; set; }
}
