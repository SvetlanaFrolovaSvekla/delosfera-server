namespace delosfera_server.Modules.Documents.VND.DTO.Response;

/// <summary>Сводка для KPI-карточек на главной странице — персональные показатели текущего пользователя</summary>
public class VndHomeSummaryResponse
{
    /// <summary>Открытых (ещё не опубликованных) циклов актуализации, где пользователь — ответственный</summary>
    public int MyResponsibleActualizations { get; set; }

    /// <summary>Решений, зачтённых пользователю по тайм-ауту (просрочка согласования) в текущем месяце</summary>
    public int MyTimeoutApprovalsThisMonth { get; set; }

    /// <summary>ВНД, где пользователь — инициатор согласования, и процесс ещё не завершён</summary>
    public int MyVndAwaitingApproval { get; set; }

    /// <summary>Этапов согласования, ожидающих решения именно этого пользователя прямо сейчас
    /// (дублирует /tasks/coordination.Count — для удобства единым запросом)</summary>
    public int PendingMyApproval { get; set; }
}