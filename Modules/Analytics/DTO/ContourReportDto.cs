using delosfera_server.Modules.Analytics.DTO.Response;

namespace delosfera_server.Modules.Analytics.DTO;

/// <summary>
/// Отчёт по контуру (АН-1..4): набор KPI-плашек и несколько распределений. Единый
/// формат для аналитики закупок, заседаний, кадрового ДО и канцелярии — контуры
/// разные, а форма подачи одна: сверху цифры, ниже пара диаграмм.
/// </summary>
public class ContourReportDto
{
    public List<ReportKpiDto> Kpis { get; set; } = [];
    public List<ReportChartDto> Charts { get; set; } = [];
}

public class ReportKpiDto
{
    public required string Label { get; set; }
    public required string Value { get; set; }
    public string? Note { get; set; }

    /// <summary>normal | warning | danger — для подсветки тревожных цифр.</summary>
    public string Tone { get; set; } = "normal";
}

public class ReportChartDto
{
    public required string Title { get; set; }
    public List<ChartCategoryPoint> Points { get; set; } = [];
}
