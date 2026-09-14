using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Procurement.DTO;
using delosfera_server.Modules.Procurement.Models;

namespace delosfera_server.Modules.Procurement.Services;

public interface IProcurementTrackerService
{
    /// <summary>Доска закупок по стадиям (ЗК-11).</summary>
    Task<List<ProcurementTrackerColumnDto>> GetBoardAsync();
}

/// <summary>
/// Доска закупок (ЗК-11): активные заявки, разложенные по стадиям жизненного цикла.
/// Реестр отвечает «какие заявки есть», доска — «на какой стадии каждая и где затор».
/// Отклонённые и отменённые сюда не попадают: доска про то, что в работе.
/// </summary>
public class ProcurementTrackerService : IProcurementTrackerService
{
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public ProcurementTrackerService(DelosferaDbContext db, IBankClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // Порядок стадий = порядок колонок слева направо. Завершённые показываем последней
    // колонкой, отклонённые/отменённые не показываем — доска про активную работу.
    private static readonly (string Code, string Title)[] Stages =
    [
        (ProcurementStatus.Draft, "Черновик"),
        (ProcurementStatus.OnApproval, "На согласовании"),
        (ProcurementStatus.OnRevision, "На доработке"),
        (ProcurementStatus.Approved, "Согласована"),
        (ProcurementStatus.InProcurement, "В процедуре"),
        (ProcurementStatus.Completed, "Завершена"),
    ];

    public async Task<List<ProcurementTrackerColumnDto>> GetBoardAsync()
    {
        var codes = Stages.Select(s => s.Code).ToArray();

        var rows = await _db.ProcurementRequests
            .Where(r => codes.Contains(r.Document!.StatusCode))
            .Select(r => new
            {
                r.Id,
                r.Document!.RegNumber,
                Status = r.Document.StatusCode,
                r.Subject,
                r.Amount,
                r.CreatedAt,
                r.InitiatorUnitId,
                r.CuratorUserId,
            })
            .ToListAsync();

        var unitIds = rows.Where(r => r.InitiatorUnitId != null)
            .Select(r => r.InitiatorUnitId!.Value).Distinct().ToList();
        var units = await _db.OrganizationUnits
            .Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.TitleRu);

        var curatorIds = rows.Where(r => r.CuratorUserId != null)
            .Select(r => r.CuratorUserId!.Value).Distinct().ToList();
        var curators = await _db.Users
            .Where(u => curatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var today = _clock.Today;

        var enriched = rows.Select(r =>
        {
            var age = Math.Max(0, today.DayNumber - DateOnly.FromDateTime(r.CreatedAt).DayNumber);
            return new
            {
                r.Status,
                Dto = new ProcurementTrackerItemDto
                {
                    Id = r.Id,
                    RegNumber = r.RegNumber,
                    Subject = r.Subject,
                    Amount = r.Amount,
                    InitiatorUnit = r.InitiatorUnitId is { } u && units.TryGetValue(u, out var ut) ? ut : null,
                    CuratorName = r.CuratorUserId is { } c && curators.TryGetValue(c, out var cn) ? cn : null,
                    AgeDays = age,
                    // Завершённой висеть не зазорно; активной больше двух недель — повод разобраться.
                    IsStale = r.Status != ProcurementStatus.Completed && age > 14,
                },
            };
        }).ToList();

        return Stages.Select(s =>
        {
            var col = enriched.Where(i => i.Status == s.Code)
                .Select(i => i.Dto)
                .OrderByDescending(i => i.AgeDays)
                .ToList();
            return new ProcurementTrackerColumnDto
            {
                Code = s.Code,
                Title = s.Title,
                Count = col.Count,
                TotalAmount = col.Sum(i => i.Amount),
                Items = col,
            };
        }).ToList();
    }
}
