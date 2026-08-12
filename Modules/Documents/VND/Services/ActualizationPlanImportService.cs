using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Export;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IActualizationPlanImportService
{
    /// <summary>Импорт плана из Excel-шаблона (PLN-01).</summary>
    Task<PlanImportResultDto> ImportAsync(int year, Stream file, int userId);

    /// <summary>Пустой шаблон с шапкой и примером строки.</summary>
    byte[] Template();

    /// <summary>Отчёт по исполнительской дисциплине (PLN-07).</summary>
    Task<byte[]> DisciplineReportAsync(int year);
}

/// <summary>
/// Импорт годового плана и отчётность по нему (PLN-01, PLN-07).
///
/// Ожидаемые колонки шаблона: №, наименование ВНД, ответственное СП, дата
/// актуализации, орган утверждения, комментарий. Порядок фиксирован — так проще
/// объяснить сотруднику, чем требовать заполнения служебных идентификаторов.
///
/// Повторный импорт того же года обновляет позиции, а не плодит дубли: план правят
/// в таблице и загружают заново, и «второй экземпляр каждой строки» — не то, чего ждут.
/// </summary>
public class ActualizationPlanImportService : IActualizationPlanImportService
{
    private static readonly string[] Header =
    [
        "№", "Наименование ВНД", "Ответственное подразделение",
        "Дата актуализации", "Орган утверждения", "Комментарий",
    ];

    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public ActualizationPlanImportService(DelosferaDbContext db, IBankClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public byte[] Template() => XlsxWorkbook.Build(new XlsxSheet
    {
        Name = "План актуализации",
        Header = Header,
        Widths = [8, 60, 40, 18, 28, 40],
        Rows =
        [
            [
                "1",
                "Положение о внутреннем контроле",
                "Управление рисками",
                "31.03.2026",
                "Правление",
                "Плановая актуализация",
            ],
        ],
    });

    public async Task<PlanImportResultDto> ImportAsync(int year, Stream file, int userId)
    {
        var rows = XlsxReader.ReadFirstSheet(file);

        if (rows.Count == 0)
            throw new InvalidOperationException("Файл пуст: нет ни одной строки");

        var plan = await _db.ActualizationPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Year == year);

        if (plan is null)
        {
            plan = new ActualizationPlan {Year = year};
            _db.ActualizationPlans.Add(plan);
            await _db.SaveChangesAsync();
        }

        var units = await _db.OrganizationUnits.ToListAsync();
        var bodies = await _db.ApprovalBodies.ToListAsync();
        var vnds = await _db.VndDocuments
            .Select(v => new {v.Id, v.Code, v.TitleRu})
            .ToListAsync();

        var result = new PlanImportResultDto {PlanId = plan.Id, Year = year};
        var order = plan.Items.Count == 0 ? 0 : plan.Items.Max(i => i.Order);

        // Записи журнала складываем по ходу разбора, а пишем после сохранения:
        // у новых позиций идентификатор появляется только там (PLN-07).
        var imported = new List<(ActualizationPlanItem Item, string Description)>();

        // Первая строка — шапка: отличаем её по тому, что во второй колонке заголовок,
        // а не наименование документа.
        var start = LooksLikeHeader(rows[0]) ? 1 : 0;

        for (var r = start; r < rows.Count; r++)
        {
            var row = rows[r];
            var line = r + 1;
            result.RowsRead++;

            var title = Cell(row, 1);

            if (string.IsNullOrWhiteSpace(title))
            {
                // Пустые строки в конце таблицы — обычное дело, о них не сообщаем;
                // сообщаем только о строках, где что-то есть, но не главное.
                if (row.Any(c => !string.IsNullOrWhiteSpace(c)))
                    result.Skipped.Add($"строка {line}: не заполнено наименование ВНД");

                continue;
            }

            var dueDate = XlsxReader.ParseDate(Cell(row, 3));

            if (dueDate is null)
            {
                result.Skipped.Add($"строка {line}: не разобрана дата актуализации «{Cell(row, 3)}»");
                continue;
            }

            var unitTitle = Cell(row, 2);
            var unit = units.FirstOrDefault(u =>
                u.TitleRu.Equals(unitTitle, StringComparison.OrdinalIgnoreCase));

            if (unit is null && !string.IsNullOrWhiteSpace(unitTitle))
                result.Skipped.Add($"строка {line}: подразделение «{unitTitle}» не найдено в справочнике");

            var bodyTitle = Cell(row, 4);
            var body = bodies.FirstOrDefault(x =>
                x.TitleRu.Equals(bodyTitle, StringComparison.OrdinalIgnoreCase));

            // Сопоставление с базой: сначала по коду, затем по точному наименованию.
            var vnd = vnds.FirstOrDefault(v => v.Code.Equals(title, StringComparison.OrdinalIgnoreCase))
                      ?? vnds.FirstOrDefault(v => v.TitleRu.Equals(title, StringComparison.OrdinalIgnoreCase));

            if (vnd is null) result.Unmatched.Add($"строка {line}: «{title}»");
            else result.Matched++;

            var existing = plan.Items.FirstOrDefault(i =>
                i.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                var created = new ActualizationPlanItem
                {
                    PlanId = plan.Id,
                    Order = ++order,
                    Title = title,
                    VndDocumentId = vnd?.Id,
                    ResponsibleUnitId = unit?.Id,
                    ApprovalBodyId = body?.Id,
                    DueDate = dueDate.Value,
                    Comment = Cell(row, 5),
                };

                plan.Items.Add(created);
                imported.Add((created, $"Импортирована из файла, срок {dueDate:dd.MM.yyyy}"));

                result.Created++;
                continue;
            }

            // Завершённую позицию импорт не переписывает: работа по ней уже учтена,
            // и повторная загрузка файла не должна отматывать её назад.
            if (existing.Status is PlanItemStatus.Actual or PlanItemStatus.Excluded)
            {
                result.Skipped.Add($"строка {line}: «{title}» — позиция закрыта, не обновляется");
                continue;
            }

            var previousDue = existing.DueDate;

            existing.VndDocumentId ??= vnd?.Id;
            existing.ResponsibleUnitId = unit?.Id ?? existing.ResponsibleUnitId;
            existing.ApprovalBodyId = body?.Id ?? existing.ApprovalBodyId;
            existing.DueDate = dueDate.Value;
            existing.Comment = Cell(row, 5) ?? existing.Comment;

            imported.Add((existing, previousDue == dueDate.Value
                ? "Обновлена импортом файла"
                : $"Обновлена импортом, срок {previousDue:dd.MM.yyyy} → {dueDate:dd.MM.yyyy}"));

            result.Updated++;
        }

        await _db.SaveChangesAsync();

        foreach (var (item, description) in imported)
        {
            _db.PlanItemEvents.Add(new PlanItemEvent
            {
                PlanItemId = item.Id,
                Kind = "Imported",
                Description = description,
                UserId = userId,
                At = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync();

        return result;
    }

    public async Task<byte[]> DisciplineReportAsync(int year)
    {
        var plan = await _db.ActualizationPlans
            .Include(p => p.Items).ThenInclude(i => i.VndDocument)
            .Include(p => p.Items).ThenInclude(i => i.ResponsibleUnit)
            .Include(p => p.Items).ThenInclude(i => i.Curator)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Year == year)
            ?? throw new KeyNotFoundException($"План актуализации на {year} год не заведён");

        var today = _clock.Today;
        var settings = await _db.ActualizationSettings.FirstOrDefaultAsync() ?? new ActualizationSettings();

        var items = plan.Items.OrderBy(i => i.Order).ToList();

        var byUnit = items
            .GroupBy(i => i.ResponsibleUnit?.TitleRu ?? "Без подразделения")
            .OrderBy(g => g.Key)
            .Select(g => new[]
            {
                g.Key,
                g.Count().ToString(),
                g.Count(i => i.Status == PlanItemStatus.Actual).ToString(),
                g.Count(i => i.Status == PlanItemStatus.OnActualization).ToString(),
                g.Count(i => IsOverdue(i, today)).ToString(),
                Percent(g.Count(i => i.Status == PlanItemStatus.Actual), g.Count()),
            })
            .ToList();

        var details = items.Select(i => new[]
        {
            i.Order.ToString(),
            i.VndDocument?.Code ?? "—",
            i.Title,
            i.ResponsibleUnit?.TitleRu ?? "—",
            i.Curator?.FullName ?? "—",
            i.DueDate.ToString("dd.MM.yyyy"),
            i.StartedOn?.ToString("dd.MM.yyyy") ?? "—",
            i.CompletedOn?.ToString("dd.MM.yyyy") ?? "—",
            ActualizationPlanService.ItemStatusTitle(i.Status),
            IsOverdue(i, today) ? $"просрочено на {today.DayNumber - i.DueDate.DayNumber} дн." : "нет",
            i.NextDueDate?.ToString("dd.MM.yyyy") ?? "—",
            i.Comment ?? string.Empty,
        }).ToList();

        return XlsxWorkbook.Build(
            new XlsxSheet
            {
                Name = "Дисциплина по СП",
                Header =
                [
                    "Подразделение", "Позиций в плане", "Актуализировано",
                    "В работе", "Просрочено", "Исполнено, %",
                ],
                Widths = [46, 16, 16, 12, 14, 14],
                Rows = byUnit,
            },
            new XlsxSheet
            {
                Name = "Позиции плана",
                Header =
                [
                    "№", "Код ВНД", "Наименование", "Ответственное СП", "Куратор",
                    "План", "Начато", "Завершено", "Статус", "Просрочка",
                    "Следующая актуализация", "Комментарий",
                ],
                Widths = [6, 16, 55, 40, 30, 13, 13, 13, 20, 22, 22, 40],
                Rows = details,
            },
            new XlsxSheet
            {
                Name = "Параметры",
                Header = ["Показатель", "Значение"],
                Widths = [44, 22],
                Rows =
                [
                    ["Год плана", plan.Year.ToString()],
                    ["Статус плана", ActualizationPlanService.StatusTitle(plan.Status)],
                    ["Чем утверждён", plan.ApprovalNote ?? "—"],
                    ["Дата утверждения", plan.ApprovedOn?.ToString("dd.MM.yyyy") ?? "—"],
                    ["Дата отчёта", today.ToString("dd.MM.yyyy")],
                    ["Порог «жёлтый», дней", settings.GreenThresholdDays.ToString()],
                    ["Порог «красный», дней", settings.RedThresholdDays.ToString()],
                ],
            });
    }

    // ── внутреннее ───────────────────────────────────────────────────────────

    private static bool IsOverdue(ActualizationPlanItem item, DateOnly today) =>
        item.Status is not (PlanItemStatus.Actual or PlanItemStatus.Excluded) && item.DueDate < today;

    private static string Percent(int part, int total) =>
        total == 0 ? "0" : Math.Round(part * 100.0 / total, 1).ToString("0.#").Replace('.', ',');

    private static string? Cell(string[] row, int index) =>
        index < row.Length ? row[index]?.Trim() : null;

    private static bool LooksLikeHeader(string[] row)
    {
        var second = Cell(row, 1) ?? string.Empty;
        return second.Contains("наименование", StringComparison.OrdinalIgnoreCase)
               || second.Contains("ВНД", StringComparison.OrdinalIgnoreCase);
    }
}
