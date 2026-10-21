using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Sz.DTO;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Sz.Services;

public interface ISzTrackerService
{
    /// <summary>Доска записок по стадиям (РС-4) — только записки, видимые пользователю.</summary>
    Task<SzTrackerBoardDto> GetBoardAsync(int currentUserId);
}

/// <summary>
/// Доска записок (РС-4): активные записки по стадиям жизненного цикла. Реестр отвечает
/// «какие записки есть», доска — «на какой стадии каждая и где затор». Завершённые и
/// отклонённые не показываются — доска про работу.
///
/// Видимость — та же, что у реестра (<see cref="SzVisibility"/>): каждый видит на доске
/// свои записки и те, к которым причастен; право «видеть все записки» открывает всю доску.
/// Черновики на доске — только свои, у всех, включая администратора: чужой черновик ещё
/// не в работе, затора в нём нет, а колонка из черновиков всего банка — шум. Разобрать
/// чужие черновики администратор может в реестре (вкладка «Черновики»).
/// </summary>
public class SzTrackerService : ISzTrackerService
{
    /// <summary>Записка в ходу дольше недели на одной стадии — повод разобраться.</summary>
    public const int StaleAfterDays = 7;

    /// <summary>
    /// На исполнении без срока — дольше месяца. Исполнение длиннее согласования, а
    /// записку со сроком оценивает сам срок: она либо просрочена, либо нет.
    /// </summary>
    public const int StaleExecutionAfterDays = 30;

    private readonly DelosferaDbContext _db;
    private readonly IBankClock _clock;
    private readonly ICurrentUserService _currentUser;

    public SzTrackerService(DelosferaDbContext db, IBankClock clock, ICurrentUserService currentUser)
    {
        _db = db;
        _clock = clock;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Колонки доски. У колонки может быть несколько статусов: прежний «Зарегистрирована»
    /// означал «номер присвоен, идёт согласование» — такие записки лежат в «На согласовании»,
    /// а не пропадают с доски.
    /// </summary>
    private static readonly (string Code, string Title, string[] Statuses)[] Stages =
    [
        (SzStatus.Draft, "Черновик", new[] {SzStatus.Draft}),
        (SzStatus.PendingRegistration, "Ждёт регистрации", new[] {SzStatus.PendingRegistration}),
        (SzStatus.OnApproval, "На согласовании", new[] {SzStatus.OnApproval, SzStatus.Registered}),
        (SzStatus.OnSigning, "На подписании", new[] {SzStatus.OnSigning}),
        (SzStatus.OnRevision, "На доработке", new[] {SzStatus.OnRevision}),
        (SzStatus.OnAddresseeDecision, "Решение адресата", new[] {SzStatus.OnAddresseeDecision}),
        (SzStatus.OnBoardReview, "На коллегиальном органе", new[] {SzStatus.OnBoardReview}),
        (SzStatus.OnExecution, "На исполнении", new[] {SzStatus.OnExecution}),
    ];

    public async Task<SzTrackerBoardDto> GetBoardAsync(int currentUserId)
    {
        var codes = Stages.SelectMany(s => s.Statuses).ToArray();
        var canSeeOthersDrafts = _currentUser.HasPermission(PermissionCode.ManageSystemSettings);

        var visible = await SzVisibility.ApplyAsync(
            _db.SzDocuments.AsNoTracking(), _db, currentUserId,
            canSeeAll: _currentUser.HasPermission(PermissionCode.ViewAllSz),
            canSeeOthersDrafts: canSeeOthersDrafts);

        var rows = await visible
            .Where(s => codes.Contains(s.Document!.StatusCode))
            // На доске — только свои черновики, какими бы правами ни обладал пользователь.
            .Where(s => s.Document!.StatusCode != SzStatus.Draft || s.Document.AuthorId == currentUserId)
            .Select(s => new
            {
                s.Id,
                s.Document!.RegNumber,
                Status = s.Document.StatusCode,
                s.Document.Title,
                s.Document.CreatedAt,
                s.Document.StatusChangedAt,
                s.Document.AuthorId,
                s.AuthorUnitId,
                s.KindId,
                s.AddresseeUserId,
                s.DueDate,
            })
            .ToListAsync();

        var kinds = await _db.SzKinds.ToDictionaryAsync(k => k.Id, k => k.TitleRu);

        var userIds = rows.Select(r => r.AuthorId)
            .Concat(rows.Where(r => r.AddresseeUserId != null).Select(r => r.AddresseeUserId!.Value))
            .Distinct().ToList();
        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        // «Моё подразделение»: своё подразделение и те, которыми руковожу (с вложенными) —
        // руководитель управления отвечает и за его отделы.
        var ownUnitId = await _db.Users
            .Where(u => u.Id == currentUserId)
            .Select(u => u.OrgUnitId)
            .FirstOrDefaultAsync();
        var myUnits = (await SzVisibility.VisibleUnitIdsAsync(_db, currentUserId)).ToHashSet();
        if (ownUnitId is int own) myUnits.Add(own);

        var today = _clock.Today;

        var enriched = rows.Select(r =>
        {
            var age = DaysSince(r.CreatedAt, today);
            var stageDays = DaysSince(r.StatusChangedAt, today);
            var daysLeft = r.DueDate is DateOnly due ? due.DayNumber - today.DayNumber : (int?)null;

            return new
            {
                r.Status,
                Dto = new SzTrackerItemDto
                {
                    Id = r.Id,
                    RegNumber = r.RegNumber,
                    Title = r.Title,
                    KindId = r.KindId,
                    Kind = kinds.GetValueOrDefault(r.KindId, "—"),
                    AuthorName = users.GetValueOrDefault(r.AuthorId),
                    AddresseeName = r.AddresseeUserId is { } a ? users.GetValueOrDefault(a) : null,
                    AgeDays = age,
                    StageSince = r.StatusChangedAt,
                    StageDays = stageDays,
                    IsStale = IsStale(r.Status, stageDays, r.DueDate),
                    DueDate = r.DueDate,
                    DaysLeft = daysLeft,
                    IsOverdue = daysLeft < 0,
                    IsMine = r.AuthorId == currentUserId,
                    IsMyUnit = r.AuthorUnitId is int unit && myUnits.Contains(unit),
                },
            };
        }).ToList();

        var columns = Stages.Select(s =>
        {
            // Сначала то, что требует внимания: просроченные, затем дольше всех стоящие на стадии.
            var col = enriched.Where(i => s.Statuses.Contains(i.Status))
                .Select(i => i.Dto)
                .OrderByDescending(i => i.IsOverdue)
                .ThenByDescending(i => i.StageDays)
                .ThenByDescending(i => i.Id)
                .ToList();
            return new SzTrackerColumnDto
            {
                Code = s.Code,
                Title = s.Title,
                Count = col.Count,
                Items = col,
            };
        }).ToList();

        return new SzTrackerBoardDto
        {
            Columns = columns,
            StaleAfterDays = StaleAfterDays,
            StaleExecutionAfterDays = StaleExecutionAfterDays,
            HasUnit = myUnits.Count > 0,
            CanSeeOthersDrafts = canSeeOthersDrafts,
        };
    }

    /// <summary>
    /// Зависла ли записка. Черновик не зависает: автор доделает его, когда сочтёт нужным.
    /// На исполнении со сроком решает срок (просрочена или нет), без срока — свой порог.
    /// </summary>
    public static bool IsStale(string status, int stageDays, DateOnly? dueDate) => status switch
    {
        SzStatus.Draft => false,
        SzStatus.OnExecution => dueDate is null && stageDays > StaleExecutionAfterDays,
        _ => stageDays > StaleAfterDays,
    };

    /// <summary>Полных календарных дней от момента (UTC) до сегодня — по дате банка, не по UTC.</summary>
    private int DaysSince(DateTime utc, DateOnly today)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _clock.Zone);
        return Math.Max(0, today.DayNumber - DateOnly.FromDateTime(local).DayNumber);
    }
}
