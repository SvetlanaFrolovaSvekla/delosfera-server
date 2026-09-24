namespace delosfera_server.Modules.Documents.VND.DTO;

/// <summary>Нормативы сроков согласования редакции ВНД по умолчанию (в минутах).</summary>
public class VndApprovalNormSettingsResponse
{
    /// <summary>"Первичное согласование".</summary>
    public int PrimaryDeadlineMinutes { get; set; }

    /// <summary>"Согласование после внесённых изменений".</summary>
    public int RepeatDeadlineMinutes { get; set; }

    /// <summary>"Финальная выдержка".</summary>
    public int FinalHoldDeadlineMinutes { get; set; }
}

public class UpdateVndApprovalNormSettingsRequest
{
    public int PrimaryDeadlineMinutes { get; set; }
    public int RepeatDeadlineMinutes { get; set; }
    public int FinalHoldDeadlineMinutes { get; set; }
}
