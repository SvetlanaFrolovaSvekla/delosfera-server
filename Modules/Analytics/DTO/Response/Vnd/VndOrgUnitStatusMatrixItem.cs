namespace delosfera_server.Modules.Analytics.DTO.Response.Vnd;

/// <summary>Ячейка матрицы "подразделение-разработчик × статус ВНД" — данные для тепловой карты
/// или сгруппированной столбчатой диаграммы</summary>
public class VndOrgUnitStatusMatrixItem
{
    /// <summary>Id подразделения-разработчика</summary>
    public int OrgUnitId { get; set; }

    /// <summary>Название подразделения</summary>
    public required string OrgUnitLabel { get; set; }

    /// <summary>Статус ВНД (строковый код, как во фронтовых фильтрах: active/onact/review/consol/arch/draft)</summary>
    public required string Status { get; set; }

    /// <summary>Количество документов подразделения в этом статусе</summary>
    public int Count { get; set; }
}
