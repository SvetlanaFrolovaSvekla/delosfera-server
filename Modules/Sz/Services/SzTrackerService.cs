using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;

namespace delosfera_server.Modules.Sz.Services;

public interface ISzTrackerService
{
    /// <summary>Доска записок по стадиям (РС-4).</summary>
    Task<List<SzTrackerColumnDto>> GetBoardAsync();
}

/// <summary>
/// Доска записок (РС-4): активные записки по стадиям жизненного цикла. Реестр отвечает
/// «какие записки есть», доска — «на какой стадии каждая и где затор». Завершённые и
/// отклонённые не показываются — доска про работу.
/// </summary>
public class SzTrackerService : ISzTrackerService
{
    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;

    public SzTrackerService(DelosferaDbContext db, IBankClock clock)
    {
        _db = db;
        _clock = clock;
    }

    private static readonly (string Code, string Title)[] Stages =
    [
        (SzStatus.Draft, "Черновик"),
        (SzStatus.PendingRegistration, "Ждёт регистрации"),
        (SzStatus.OnApproval, "На согласовании"),
        (SzStatus.OnSigning, "На подписании"),
        (SzStatus.OnRevision, "На доработке"),
        (SzStatus.OnAddresseeDecision, "Решение адресата"),
        (SzStatus.OnExecution, "На исполнении"),
    ];

    public async Task<List<SzTrackerColumnDto>> GetBoardAsync()
    {
        var codes = Stages.Select(s => s.Code).ToArray();

        var rows = await _db.SzDocuments
            .Where(s => codes.Contains(s.Document!.StatusCode))
            .Select(s => new
            {
                s.Id,
                s.Document!.RegNumber,
                Status = s.Document.StatusCode,
                s.Document.Title,
                s.Document.CreatedAt,
                s.Document.AuthorId,
                s.KindId,
                s.AddresseeUserId,
            })
            .ToListAsync();

        var kinds = await _db.SzKinds.ToDictionaryAsync(k => k.Id, k => k.TitleRu);

        var userIds = rows.Select(r => r.AuthorId)
            .Concat(rows.Where(r => r.AddresseeUserId != null).Select(r => r.AddresseeUserId!.Value))
            .Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var today = _clock.Today;

        var enriched = rows.Select(r =>
        {
            var age = Math.Max(0, today.DayNumber - DateOnly.FromDateTime(r.CreatedAt).DayNumber);
            return new
            {
                r.Status,
                Dto = new SzTrackerItemDto
                {
                    Id = r.Id,
                    RegNumber = r.RegNumber,
                    Title = r.Title,
                    Kind = kinds.GetValueOrDefault(r.KindId, "—"),
                    AuthorName = users.GetValueOrDefault(r.AuthorId),
                    AddresseeName = r.AddresseeUserId is { } a ? users.GetValueOrDefault(a) : null,
                    AgeDays = age,
                    // Черновик может лежать сколько угодно; записка в ходу дольше двух
                    // недель — повод разобраться.
                    IsStale = r.Status != SzStatus.Draft && age > 14,
                },
            };
        }).ToList();

        return Stages.Select(s =>
        {
            var col = enriched.Where(i => i.Status == s.Code)
                .Select(i => i.Dto)
                .OrderByDescending(i => i.AgeDays)
                .ToList();
            return new SzTrackerColumnDto
            {
                Code = s.Code,
                Title = s.Title,
                Count = col.Count,
                Items = col,
            };
        }).ToList();
    }
}
