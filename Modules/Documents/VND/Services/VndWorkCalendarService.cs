using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IVndWorkCalendarService
{
    Task<List<VndWorkCalendarDayResponse>> GetYearAsync(int year);
    Task<VndWorkCalendarRulesResponse> GetRulesAsync(DateOnly from, DateOnly to);
    Task<VndWorkCalendarDayResponse> CreateAsync(SaveVndWorkCalendarDayRequest request);
    Task<VndWorkCalendarDayResponse> UpdateAsync(int id, SaveVndWorkCalendarDayRequest request);
    Task DeleteAsync(int id);
    Task<CopyVndWorkCalendarYearResponse> CopyYearAsync(CopyVndWorkCalendarYearRequest request);
    Task<VndWorkHoursResponse> GetHoursAsync();
    Task<VndWorkHoursResponse> UpdateHoursAsync(UpdateVndWorkHoursRequest request);
}

/// <summary>
/// Справочник "Производственный календарь" раздела ВНД — рабочее время банка и праздники, которые
/// главный редактор проставляет на год. После любой правки:
/// 1) сбрасывается кэш календаря (IVndWorkCalendarCache);
/// 2) пересчитываются сроки ТЕКУЩИХ фаз уже идущих согласований на рабочем времени —
///    добавили праздник на следующей неделе → у идущих согласований срок сдвинется на день.
/// </summary>
public class VndWorkCalendarService : IVndWorkCalendarService
{
    private const int MinYear = 2000;
    private const int MaxYear = 2100;
    private const int MaxTitleLength = 200;

    private readonly DelosferaDbContext _db;
    private readonly IVndWorkCalendarCache _cache;
    private readonly ILogger<VndWorkCalendarService> _logger;

    public VndWorkCalendarService(DelosferaDbContext db, IVndWorkCalendarCache cache,
        ILogger<VndWorkCalendarService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<VndWorkCalendarDayResponse>> GetYearAsync(int year)
    {
        ValidateYear(year);
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);

        return await _db.Set<VndWorkCalendarDay>()
            .AsNoTracking()
            .Where(d => d.Date >= from && d.Date <= to)
            .OrderBy(d => d.Date)
            .Select(d => new VndWorkCalendarDayResponse { Id = d.Id, Date = d.Date, Title = d.Title })
            .ToListAsync();
    }

    public async Task<VndWorkCalendarRulesResponse> GetRulesAsync(DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        // Клиенту нужен период вокруг текущих сроков; больше пары лет — ошибка вызова.
        if (to.DayNumber - from.DayNumber > 3 * 366)
            throw new InvalidOperationException("Период календаря не может превышать 3 года");

        var holidays = await _db.Set<VndWorkCalendarDay>()
            .AsNoTracking()
            .Where(d => d.Date >= from && d.Date <= to)
            .OrderBy(d => d.Date)
            .Select(d => d.Date)
            .ToListAsync();
        var rules = await _cache.GetRulesAsync();

        return new VndWorkCalendarRulesResponse
        {
            TimeZone = "Asia/Bishkek",
            UtcOffsetMinutes = (int)VndWorkingCalendar.Zone.BaseUtcOffset.TotalMinutes,
            WorkStartMinutes = rules.WorkStartMinutes,
            WorkEndMinutes = rules.WorkEndMinutes,
            WorkDayMinutes = rules.WorkDayMinutes,
            From = from,
            To = to,
            Holidays = holidays,
        };
    }

    public async Task<VndWorkCalendarDayResponse> CreateAsync(SaveVndWorkCalendarDayRequest request)
    {
        var title = Validate(request);
        await EnsureDateFreeAsync(request.Date, exceptId: null);

        var entity = new VndWorkCalendarDay { Date = request.Date, Title = title };
        _db.Set<VndWorkCalendarDay>().Add(entity);
        await _db.SaveChangesAsync();

        await AfterChangeAsync();
        return ToResponse(entity);
    }

    public async Task<VndWorkCalendarDayResponse> UpdateAsync(int id, SaveVndWorkCalendarDayRequest request)
    {
        var title = Validate(request);
        var entity = await _db.Set<VndWorkCalendarDay>().FindAsync(id)
                     ?? throw new KeyNotFoundException("Запись календаря не найдена");
        await EnsureDateFreeAsync(request.Date, exceptId: id);

        entity.Date = request.Date;
        entity.Title = title;
        await _db.SaveChangesAsync();

        await AfterChangeAsync();
        return ToResponse(entity);
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Set<VndWorkCalendarDay>().FindAsync(id)
                     ?? throw new KeyNotFoundException("Запись календаря не найдена");
        _db.Set<VndWorkCalendarDay>().Remove(entity);
        await _db.SaveChangesAsync();

        await AfterChangeAsync();
    }

    /// <summary>
    /// Перенести праздники прошлого года на новый год по тем же числам — основа для заполнения,
    /// которую редактор потом правит. Даты, которые в новом году уже заняты, пропускаются;
    /// 29 февраля в невисокосный год — тоже. Праздники по лунному календарю (Орозо айт,
    /// Курман айт) скопируются на прошлогоднюю дату — редактору нужно их поправить.
    /// </summary>
    public async Task<CopyVndWorkCalendarYearResponse> CopyYearAsync(CopyVndWorkCalendarYearRequest request)
    {
        ValidateYear(request.FromYear);
        ValidateYear(request.ToYear);
        if (request.FromYear == request.ToYear)
            throw new InvalidOperationException("Выберите другой год");

        var (fromStart, fromEnd) = (new DateOnly(request.FromYear, 1, 1), new DateOnly(request.FromYear, 12, 31));
        var (toStart, toEnd) = (new DateOnly(request.ToYear, 1, 1), new DateOnly(request.ToYear, 12, 31));

        var source = await _db.Set<VndWorkCalendarDay>()
            .AsNoTracking()
            .Where(d => d.Date >= fromStart && d.Date <= fromEnd)
            .ToListAsync();

        var targetTaken = (await _db.Set<VndWorkCalendarDay>()
                .AsNoTracking()
                .Where(d => d.Date >= toStart && d.Date <= toEnd)
                .Select(d => d.Date)
                .ToListAsync())
            .ToHashSet();

        var added = 0;
        var skipped = 0;
        foreach (var day in source)
        {
            if (day.Date.Month == 2 && day.Date.Day == 29 && !DateTime.IsLeapYear(request.ToYear))
            {
                skipped++;
                continue;
            }

            var date = new DateOnly(request.ToYear, day.Date.Month, day.Date.Day);
            if (!targetTaken.Add(date))
            {
                skipped++;
                continue;
            }

            _db.Set<VndWorkCalendarDay>().Add(new VndWorkCalendarDay
            {
                Date = date,
                Title = day.Title,
            });
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync();
            await AfterChangeAsync();
        }

        return new CopyVndWorkCalendarYearResponse { Added = added, Skipped = skipped };
    }

    // ------------------------------------------------------------------

    private static string Validate(SaveVndWorkCalendarDayRequest request)
    {
        ValidateYear(request.Date.Year);

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0) title = "Праздничный день";
        if (title.Length > MaxTitleLength)
            throw new InvalidOperationException($"Название не должно превышать {MaxTitleLength} символов");

        return title;
    }

    private static void ValidateYear(int year)
    {
        if (year is < MinYear or > MaxYear)
            throw new InvalidOperationException($"Год должен быть в диапазоне {MinYear}–{MaxYear}");
    }

    private async Task EnsureDateFreeAsync(DateOnly date, int? exceptId)
    {
        var taken = await _db.Set<VndWorkCalendarDay>()
            .AnyAsync(d => d.Date == date && (exceptId == null || d.Id != exceptId));
        if (taken)
            throw new InvalidOperationException($"На {date:dd.MM.yyyy} в календаре уже есть запись — измените её");
    }

    private async Task AfterChangeAsync()
    {
        _cache.Invalidate();
        try
        {
            await RecalculateActiveDeadlinesAsync();
        }
        catch (Exception ex)
        {
            // Справочник уже сохранён; сроки пересчитает следующая правка или можно повторить.
            _logger.LogError(ex, "Не удалось пересчитать сроки идущих согласований ВНД после правки календаря");
        }
    }

    /// <summary>
    /// Пересчёт срока ТЕКУЩЕЙ фазы у идущих согласований на рабочем времени. Точечный UPDATE по
    /// каждому изменившемуся процессу (с проверкой, что фаза не сменилась за это время) — без
    /// загрузки этапов и без конфликтов с параллельными решениями согласующих. Идущих
    /// согласований единицы-десятки, календарь правят раз в несколько месяцев.
    /// </summary>
    private async Task RecalculateActiveDeadlinesAsync()
    {
        var rules = await _cache.GetRulesAsync();

        var active = await _db.VndApprovalProcesses
            .AsNoTracking()
            .Where(p => p.UsesWorkingTime && (p.Status == ApprovalProcessStatus.Primary
                                              || p.Status == ApprovalProcessStatus.Repeated
                                              || p.Status == ApprovalProcessStatus.FinalHold))
            .Select(p => new
            {
                p.Id, p.Status,
                p.PrimaryStartedAt, p.PrimaryDeadlineMinutes, p.PrimaryDeadlineAt,
                p.RepeatStartedAt, p.RepeatDeadlineMinutes, p.RepeatDeadlineAt,
                p.FinalHoldStartedAt, p.FinalHoldDeadlineMinutes, p.FinalHoldDeadlineAt,
            })
            .ToListAsync();

        var changed = 0;
        foreach (var p in active)
        {
            switch (p.Status)
            {
                case ApprovalProcessStatus.Primary:
                {
                    var due = VndWorkingCalendar.AddWorkingMinutes(p.PrimaryStartedAt, p.PrimaryDeadlineMinutes, rules);
                    if (due == p.PrimaryDeadlineAt) break;
                    changed += await _db.VndApprovalProcesses
                        .Where(x => x.Id == p.Id && x.Status == ApprovalProcessStatus.Primary)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.PrimaryDeadlineAt, due));
                    break;
                }
                case ApprovalProcessStatus.Repeated when p.RepeatStartedAt is { } started:
                {
                    var due = VndWorkingCalendar.AddWorkingMinutes(started, p.RepeatDeadlineMinutes, rules);
                    if (due == p.RepeatDeadlineAt) break;
                    changed += await _db.VndApprovalProcesses
                        .Where(x => x.Id == p.Id && x.Status == ApprovalProcessStatus.Repeated
                                    && x.RepeatStartedAt == started)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.RepeatDeadlineAt, due));
                    break;
                }
                case ApprovalProcessStatus.FinalHold when p.FinalHoldStartedAt is { } started:
                {
                    var due = VndWorkingCalendar.AddWorkingMinutes(started, p.FinalHoldDeadlineMinutes, rules);
                    if (due == p.FinalHoldDeadlineAt) break;
                    changed += await _db.VndApprovalProcesses
                        .Where(x => x.Id == p.Id && x.Status == ApprovalProcessStatus.FinalHold
                                    && x.FinalHoldStartedAt == started)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.FinalHoldDeadlineAt, due));
                    break;
                }
            }
        }

        if (changed > 0)
            _logger.LogInformation("Производственный календарь ВНД изменён: пересчитаны сроки {Count} согласований", changed);
    }

    private static VndWorkCalendarDayResponse ToResponse(VndWorkCalendarDay d) =>
        new() { Id = d.Id, Date = d.Date, Title = d.Title };

    // ------------------------------------------------------------------ рабочее время банка

    private const int MinWorkDayMinutes = 60;

    public async Task<VndWorkHoursResponse> GetHoursAsync()
    {
        var h = await LoadHoursAsync();
        return new VndWorkHoursResponse { WorkStartMinutes = h.WorkStartMinutes, WorkEndMinutes = h.WorkEndMinutes };
    }

    /// <summary>
    /// Сменить рабочее время банка. Норматив «1 д.» = длина рабочего дня, поэтому нормативы,
    /// заданные в днях, сохраняют смысл: нормативы по умолчанию и нормативы ещё не завершённых
    /// согласований пересчитываются пропорционально (7 д. остаются 7 д.), а сроки текущих фаз
    /// пересчитываются по новым часам.
    /// </summary>
    public async Task<VndWorkHoursResponse> UpdateHoursAsync(UpdateVndWorkHoursRequest request)
    {
        if (request.WorkStartMinutes is < 0 or >= 24 * 60 || request.WorkEndMinutes is <= 0 or > 24 * 60)
            throw new InvalidOperationException("Время должно быть в пределах суток");
        if (request.WorkEndMinutes - request.WorkStartMinutes < MinWorkDayMinutes)
            throw new InvalidOperationException("Рабочий день должен длиться не меньше часа, а конец — быть позже начала");

        var h = await LoadHoursAsync();
        var oldDay = h.WorkEndMinutes - h.WorkStartMinutes;
        var newDay = request.WorkEndMinutes - request.WorkStartMinutes;

        h.WorkStartMinutes = request.WorkStartMinutes;
        h.WorkEndMinutes = request.WorkEndMinutes;

        if (oldDay != newDay && oldDay > 0)
        {
            int Scale(int minutes) => Math.Max(1, (int)Math.Round(minutes * (double)newDay / oldDay));

            var norms = await _db.Set<VndApprovalNormSettings>().FirstOrDefaultAsync();
            if (norms is not null)
            {
                norms.PrimaryDeadlineMinutes = Scale(norms.PrimaryDeadlineMinutes);
                norms.RepeatDeadlineMinutes = Scale(norms.RepeatDeadlineMinutes);
                norms.FinalHoldDeadlineMinutes = Scale(norms.FinalHoldDeadlineMinutes);
            }

            var running = await _db.VndApprovalProcesses
                .Where(p => p.UsesWorkingTime && (p.Status == ApprovalProcessStatus.Primary
                                                  || p.Status == ApprovalProcessStatus.RevisionNeeded
                                                  || p.Status == ApprovalProcessStatus.Repeated
                                                  || p.Status == ApprovalProcessStatus.FinalHold))
                .ToListAsync();
            foreach (var p in running)
            {
                p.PrimaryDeadlineMinutes = Scale(p.PrimaryDeadlineMinutes);
                p.RepeatDeadlineMinutes = Scale(p.RepeatDeadlineMinutes);
                p.FinalHoldDeadlineMinutes = Scale(p.FinalHoldDeadlineMinutes);
            }
        }

        await _db.SaveChangesAsync();
        await AfterChangeAsync();

        return new VndWorkHoursResponse { WorkStartMinutes = h.WorkStartMinutes, WorkEndMinutes = h.WorkEndMinutes };
    }

    private async Task<VndWorkHoursSettings> LoadHoursAsync()
    {
        var h = await _db.Set<VndWorkHoursSettings>().FirstOrDefaultAsync();
        if (h is not null) return h;

        // Значения по умолчанию засеяны миграцией; страховка на случай пустой таблицы.
        h = new VndWorkHoursSettings();
        _db.Set<VndWorkHoursSettings>().Add(h);
        await _db.SaveChangesAsync();
        return h;
    }
}
