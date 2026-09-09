using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Meetings.DTO;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Meetings.Services;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Повестка: порядок вопросов и правка поручений.
///
/// Порядок только проставлялся при добавлении — внесённый последним вопрос
/// последним и обсуждался, переставить его было нечем. Поручение правилось лишь
/// удалением и заведением заново, а вместе с ним стирался отчёт исполнителя.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AgendaOrderAndAssignmentsTests
{
    private readonly PostgresFixture _postgres;

    public AgendaOrderAndAssignmentsTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Вопросы_переставляются()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var повестка = Повестка(db, стенд.Secretary);

        var было = await ПорядокАsync(db, стенд.MeetingId);
        var стало = await повестка.ReorderAsync(стенд.MeetingId, [было[2], было[0], было[1]]);

        Assert.Equal([1, 2, 3], стало.Select(i => i.Order));
        Assert.Equal(было[2], стало[0].Id);
    }

    [Fact]
    public async Task Перестановка_требует_всех_вопросов()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var было = await ПорядокАsync(db, стенд.MeetingId);

        // Пропущенный вопрос получил бы чужой номер, а два вопроса — один и тот же.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Повестка(db, стенд.Secretary).ReorderAsync(стенд.MeetingId, [было[0], было[1]]));
    }

    [Fact]
    public async Task Поручение_правится_без_потери_отчёта()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var повестка = Повестка(db, стенд.Secretary);
        var вопрос = (await ПорядокАsync(db, стенд.MeetingId))[0];

        await повестка.AddAssignmentAsync(вопрос, new AgendaAssignmentRequest
        {
            UserId = стенд.Executor,
            Text = "Подготовить расчёт",
            DueDate = new DateOnly(2026, 10, 1),
        });

        var поручение = await db.AgendaAssignments.AsNoTracking()
            .FirstAsync(a => a.AgendaItemId == вопрос);

        await повестка.ReportAsync(поручение.Id,
            new AgendaReportRequest {Report = "Расчёт готов", Status = ExecutionStatus.DoneOnTime},
            стенд.Executor);

        await повестка.UpdateAssignmentAsync(поручение.Id, new AgendaAssignmentRequest
        {
            UserId = стенд.Executor,
            Text = "Подготовить расчёт и согласовать с бюджетом",
            DueDate = new DateOnly(2026, 10, 15),
        });

        var после = await db.AgendaAssignments.AsNoTracking().FirstAsync(a => a.Id == поручение.Id);

        Assert.Equal("Подготовить расчёт и согласовать с бюджетом", после.Text);
        Assert.Equal(new DateOnly(2026, 10, 15), после.DueDate);

        // Отчёт пишет исполнитель, и правка секретаря его не трогает.
        Assert.Equal("Расчёт готов", после.Report);
    }

    [Fact]
    public async Task Смена_исполнителя_переносит_и_подразделение()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var повестка = Повестка(db, стенд.Secretary);
        var вопрос = (await ПорядокАsync(db, стенд.MeetingId))[0];

        await повестка.AddAssignmentAsync(вопрос,
            new AgendaAssignmentRequest {UserId = стенд.Executor, Text = "Первому"});

        var поручение = await db.AgendaAssignments.AsNoTracking()
            .FirstAsync(a => a.AgendaItemId == вопрос);

        await повестка.UpdateAssignmentAsync(поручение.Id,
            new AgendaAssignmentRequest {UserId = стенд.SecondExecutor});

        var после = await db.AgendaAssignments.AsNoTracking().FirstAsync(a => a.Id == поручение.Id);

        Assert.Equal(стенд.SecondExecutor, после.UserId);

        // Иначе поручение осталось бы числиться за прежним отделом.
        Assert.Equal(стенд.SecondUnit, после.OrgUnitId);
    }

    [Fact]
    public async Task Проект_постановления_хранится_отдельно_от_решения()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var повестка = Повестка(db, стенд.Secretary);
        var вопрос = (await ПорядокАsync(db, стенд.MeetingId))[0];

        await повестка.UpdateItemAsync(вопрос, new AgendaItemRequest
        {
            Topic = "Вопрос первый",
            DraftResolution = "Проект: утвердить закупку в пределах сметы",
        });

        var после = await db.AgendaItems.AsNoTracking().FirstAsync(i => i.Id == вопрос);

        // Проект готовится к заседанию и может быть не принят; решение появляется
        // после и пишется отдельно.
        Assert.Equal("Проект: утвердить закупку в пределах сметы", после.DraftResolution);
        Assert.Null(после.Decision);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(
        int MeetingId, int Secretary, int Executor, int SecondExecutor, int SecondUnit);

    private static IAgendaService Повестка(DelosferaDbContext db, int userId)
    {
        var currentUser = new FakeCurrentUser(userId, PermissionCode.ManageBoardMeetings);

        return new AgendaService(
            db, new MeetingAccessService(db, currentUser), currentUser, new BankClock(), new delosfera_server.Modules.Documents.Services.AuditService(db));
    }

    private static async Task<List<int>> ПорядокАsync(DelosferaDbContext db, int meetingId) =>
        await db.AgendaItems.AsNoTracking()
            .Where(i => i.MeetingId == meetingId)
            .OrderBy(i => i.Order)
            .Select(i => i.Id)
            .ToListAsync();

    private static async Task<Стенд> SeedAsync(DelosferaDbContext db)
    {
        var секретарь = await ПользовательАsync(db, "Секретарь Правления", null);

        var отдел = new delosfera_server.Modules.Dictionaries.Models.OrganizationUnit
        {
            TitleRu = $"Отдел исполнителя {Guid.NewGuid():N}"[..28],
        };
        db.OrganizationUnits.Add(отдел);
        await db.SaveChangesAsync();

        var исполнитель = await ПользовательАsync(db, "Первый исполнитель", null);
        var второй = await ПользовательАsync(db, "Второй исполнитель", отдел.Id);

        var meeting = new Meeting
        {
            Body = MeetingBody.Board,
            Form = MeetingForm.InPerson,
            Date = new DateOnly(2026, 9, 20),
            Time = new TimeOnly(10, 0),
            SecretaryUserId = секретарь,
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 3; i++)
            db.AgendaItems.Add(new AgendaItem
            {
                MeetingId = meeting.Id,
                Order = i,
                Topic = $"Вопрос {i}",
            });

        await db.SaveChangesAsync();

        return new Стенд(meeting.Id, секретарь, исполнитель, второй, отдел.Id);
    }

    private static async Task<int> ПользовательАsync(DelosferaDbContext db, string fullName, int? unitId)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"agenda-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
            OrgUnitId = unitId,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }
}
