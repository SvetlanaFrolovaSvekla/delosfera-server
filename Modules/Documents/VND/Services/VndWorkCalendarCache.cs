using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// In-memory кэш производственного календаря ВНД (singleton). Справочник крошечный — десяток-
/// другой строк в год, меняется раз в несколько месяцев, а нужен на каждом старте фазы
/// согласования. Поэтому читаем его из БД не чаще раза в <see cref="Ttl"/>; при правке
/// справочника на этой реплике кэш сбрасывается сразу (<see cref="Invalidate"/>), остальные
/// реплики подхватят изменения не позже чем через TTL.
/// </summary>
public interface IVndWorkCalendarCache
{
    Task<VndWorkingCalendar.Rules> GetRulesAsync(CancellationToken ct = default);
    void Invalidate();
}

public class VndWorkCalendarCache : IVndWorkCalendarCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VndWorkCalendarCache> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private volatile VndWorkingCalendar.Rules? _rules;
    private DateTime _loadedAtUtc;

    public VndWorkCalendarCache(IServiceScopeFactory scopeFactory, ILogger<VndWorkCalendarCache> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<VndWorkingCalendar.Rules> GetRulesAsync(CancellationToken ct = default)
    {
        var cached = _rules;
        if (cached is not null && DateTime.UtcNow - _loadedAtUtc < Ttl) return cached;

        await _lock.WaitAsync(ct);
        try
        {
            cached = _rules;
            if (cached is not null && DateTime.UtcNow - _loadedAtUtc < Ttl) return cached;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DelosferaDbContext>();
            var holidays = await db.Set<VndWorkCalendarDay>()
                .AsNoTracking()
                .Select(d => d.Date)
                .ToListAsync(ct);
            var hours = await db.Set<VndWorkHoursSettings>()
                .AsNoTracking()
                .Select(h => new { h.WorkStartMinutes, h.WorkEndMinutes })
                .FirstOrDefaultAsync(ct);

            _rules = new VndWorkingCalendar.Rules(
                holidays.ToHashSet(),
                hours?.WorkStartMinutes ?? VndWorkingCalendar.DefaultWorkStartMinutes,
                hours?.WorkEndMinutes ?? VndWorkingCalendar.DefaultWorkEndMinutes);
            _loadedAtUtc = DateTime.UtcNow;
            return _rules;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && _rules is not null)
        {
            // БД моргнула — работаем по последнему известному календарю, а не падаем.
            _logger.LogWarning(ex, "Не удалось обновить производственный календарь ВНД, используется кэш");
            return _rules;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate() => _rules = null;
}

/// <summary>Расчёт сроков фаз процесса согласования ВНД (хранятся в БД, см. VndApprovalProcess).</summary>
public static class VndApprovalDeadlines
{
    /// <summary>Срок фазы: рабочее время для новых процессов, календарное — для старых
    /// (запущенных до перехода на рабочий календарь, UsesWorkingTime == false).</summary>
    public static DateTime Compute(DateTime startedAtUtc, int minutes, bool usesWorkingTime,
        VndWorkingCalendar.Rules rules) =>
        usesWorkingTime
            ? VndWorkingCalendar.AddWorkingMinutes(startedAtUtc, minutes, rules)
            : startedAtUtc.AddMinutes(minutes);

    /// <summary>Пересчитать сохранённые сроки всех начавшихся фаз процесса.</summary>
    public static void Apply(VndApprovalProcess p, VndWorkingCalendar.Rules rules)
    {
        p.PrimaryDeadlineAt = Compute(p.PrimaryStartedAt, p.PrimaryDeadlineMinutes, p.UsesWorkingTime, rules);
        p.RepeatDeadlineAt = p.RepeatStartedAt is { } r
            ? Compute(r, p.RepeatDeadlineMinutes, p.UsesWorkingTime, rules)
            : null;
        p.FinalHoldDeadlineAt = p.FinalHoldStartedAt is { } f
            ? Compute(f, p.FinalHoldDeadlineMinutes, p.UsesWorkingTime, rules)
            : null;
    }
}
