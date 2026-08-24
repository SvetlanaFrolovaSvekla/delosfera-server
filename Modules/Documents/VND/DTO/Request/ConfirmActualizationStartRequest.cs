namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Подтверждение старта актуализации после одобренной заявки на доступ — этот же вызов
/// совмещает в себе и старт цикла, и шаг "Выполнить актуализацию": ShiftNextPeriod сюда уже не
/// передаём, он взят из одобренной заявки (см. VndActualizationRequest.ShiftNextPeriod, которую
/// мог скорректировать главный редактор при одобрении) — здесь решается только "без изменений".</summary>
public class ConfirmActualizationStartRequest
{
    /// <summary>Планируется ли актуализация без изменений документа — новая редакция в рамках
    /// цикла не создаётся, пока это так (см. VndDocument.ActualizationPlannedNoChanges).</summary>
    public required bool PlannedNoChanges { get; set; }
}
