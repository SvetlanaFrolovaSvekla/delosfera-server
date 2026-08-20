using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Export;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Dictionaries.Models;
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

        // Шапку ищем, а не считаем первой строкой: в плане банка над ней стоят
        // название документа и ссылка на протокол Правления, которым он утверждён.
        var (headerRow, columns) = FindHeader(rows);
        var start = headerRow + 1;

        // Подразделение в плане задаётся не колонкой, а строкой-разделом: «1.1
        // Управление риск-менеджмента», и относится ко всем позициям под ней.
        // Колонка «Ответственный исполнитель» содержит ФИО, а не подразделение.
        OrganizationUnit? sectionUnit = null;

        for (var r = start; r < rows.Count; r++)
        {
            var row = rows[r];
            var line = r + 1;
            result.RowsRead++;

            var title = At(row, columns.Title);

            if (string.IsNullOrWhiteSpace(title))
            {
                // Пустые строки в конце таблицы — обычное дело, о них не сообщаем;
                // сообщаем только о строках, где что-то есть, но не главное.
                if (row.Any(c => !string.IsNullOrWhiteSpace(c)))
                    result.Skipped.Add($"строка {line}: не заполнено наименование ВНД");

                continue;
            }

            // Раздел: заполнено только наименование, срока нет. Такая строка не
            // позиция плана, а заголовок группы — запоминаем подразделение и идём дальше.
            if (string.IsNullOrWhiteSpace(At(row, columns.Due)))
            {
                var found = MatchUnit(units, title);
                if (found is not null) sectionUnit = found;
                continue;
            }

            var dueDate = XlsxReader.ParseDate(At(row, columns.Due));

            if (dueDate is null)
            {
                result.Skipped.Add($"строка {line}: не разобран срок «{At(row, columns.Due)}»");
                continue;
            }

            // Подразделение берём из раздела; если в файле есть отдельная колонка
            // с подразделением — она главнее, потому что задана явно.
            var unitTitle = At(row, columns.Unit);
            var unit = MatchUnit(units, unitTitle) ?? sectionUnit;

            if (unit is null)
                result.Skipped.Add($"строка {line}: подразделение не определено — ни в разделе, ни в строке");

            var bodyTitle = At(row, columns.Body);
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
                    Comment = BuildComment(row, columns),
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
            existing.Comment = BuildComment(row, columns) ?? existing.Comment;

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

    /// <summary>Номера колонок, найденные по заголовкам. -1 — колонки нет.</summary>
    private record Columns(int Title, int Due, int Unit, int Body, int Executor, int Curator,
                           int Kind, int Purpose, int Comment);

    /// <summary>
    /// Найти шапку и разобрать, где какая колонка.
    ///
    /// Порядок колонок в плане банка не совпадает с шаблоном, а над шапкой стоят
    /// название документа и ссылка на протокол. Поэтому колонки ищутся по словам
    /// в заголовке, а не по номерам: план правят каждый год, и переставленный
    /// столбец не должен ломать загрузку.
    /// </summary>
    private static (int Row, Columns Columns) FindHeader(List<string[]> rows)
    {
        for (var r = 0; r < Math.Min(rows.Count, 20); r++)
        {
            var row = rows[r];
            var title = Find(row, "наименование");

            // Шапка — та строка, где есть и наименование, и срок: у названия
            // документа над таблицей второго признака нет.
            if (title < 0) continue;

            var due = Find(row, "срок", "дата актуализ", "дата");
            if (due < 0) continue;

            return (r, new Columns(
                Title: title,
                Due: due,
                Unit: Find(row, "ответственное подразделение", "подразделение"),
                Body: Find(row, "орган утверждения", "орган"),
                Executor: Find(row, "ответственный исполнитель", "исполнитель"),
                Curator: Find(row, "контролирующее лицо", "куратор"),
                Kind: Find(row, "вид разработки", "вид"),
                Purpose: Find(row, "цель"),
                Comment: Find(row, "комментарий", "примечание")));
        }

        throw new InvalidOperationException(
            "В файле не найдена шапка таблицы: нужны колонки с наименованием ВНД и сроком");
    }

    /// <summary>Номер колонки по первому подходящему слову заголовка.</summary>
    private static int Find(string[] row, params string[] words)
    {
        foreach (var word in words)
            for (var i = 0; i < row.Length; i++)
                if ((row[i] ?? string.Empty).Contains(word, StringComparison.OrdinalIgnoreCase))
                    return i;

        return -1;
    }

    private static string? At(string[] row, int index) =>
        index >= 0 && index < row.Length && !string.IsNullOrWhiteSpace(row[index])
            ? row[index].Trim()
            : null;

    /// <summary>
    /// Подразделение по названию раздела плана.
    ///
    /// Сверка идёт по нормализованному названию, а не по вхождению подстроки:
    /// «Управление внутреннего аудита» содержит в себе «Правление», и поиск по
    /// вхождению уводил позиции этого управления в подразделение Правления.
    /// Номер раздела вида «1.1» отбрасывается: в справочнике его нет.
    ///
    /// Не нашлось — позиция остаётся без подразделения, и это видно в итогах
    /// загрузки. Приписать её к похожему по названию хуже, чем оставить пустой:
    /// ошибку в приписке никто не заметит, а пустое поле бросается в глаза.
    /// </summary>
    private static OrganizationUnit? MatchUnit(List<OrganizationUnit> units, string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var clean = Normalize(title);
        if (clean.Length < 4) return null;

        return units.FirstOrDefault(u => Normalize(u.TitleRu) == clean);
    }

    /// <summary>
    /// Название к сравнимому виду: без номера раздела, лишних пробелов, кавычек
    /// и различий в регистре. «1.1  Управление  риск-менеджмента» и «Управление
    /// риск-менеджмента» — одно и то же подразделение.
    /// </summary>
    private static string Normalize(string value)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(value.Trim(), @"^[IVXLC\d]+[.\d]*\s+", "");
        text = text.Replace("«", "\"").Replace("»", "\"").Replace("ё", "е").Replace("Ё", "Е");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Комментарий к позиции. В плане есть колонки, которых нет в модели: вид
    /// разработки, цель актуализации, ответственный исполнитель, контролирующее
    /// лицо. Терять их нельзя — методологу они нужны, — поэтому сводим в текст.
    /// </summary>
    private static string? BuildComment(string[] row, Columns c)
    {
        var parts = new List<string>();

        void Add(string label, int index)
        {
            var value = At(row, index);
            if (!string.IsNullOrWhiteSpace(value)) parts.Add($"{label}: {value}");
        }

        Add("Вид разработки", c.Kind);
        Add("Цель", c.Purpose);
        Add("Исполнитель", c.Executor);
        Add("Контролирует", c.Curator);

        var own = At(row, c.Comment);
        if (!string.IsNullOrWhiteSpace(own)) parts.Insert(0, own);

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }
}
