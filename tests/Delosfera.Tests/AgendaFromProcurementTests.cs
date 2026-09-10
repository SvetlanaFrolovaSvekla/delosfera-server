using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Meetings.Models;
using delosfera_server.Modules.Meetings.Services;
using delosfera_server.Modules.Procurement.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Models;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Заявка на закупку в очереди вопросов к коллегиальному органу.
///
/// Матрица полномочий определяет орган утверждения по сумме, и заявка встаёт на
/// этап ожидания его решения. Этап этот ни с чем не был связан: секретарь органа
/// о заявке не узнавал, а маршрут ждал решения, которое никто не собирался
/// выносить в повестку.
/// </summary>
[Collection(PostgresCollection.Name)]
public class AgendaFromProcurementTests
{
    private readonly PostgresFixture _postgres;

    public AgendaFromProcurementTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Заявка_на_этапе_Правления_попадает_к_секретарю()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var очередь = await new AgendaCandidateService(db, new DocumentService(db, new AuditService(db), new NumeratorService(db))).ListAsync(MeetingBody.Board);

        var заявка = Assert.Single(очередь.Where(c => c.Kind == AgendaCandidateKind.Procurement));
        Assert.Equal(стенд.RequestId, заявка.ProcurementRequestId);
        Assert.Equal(5_000_000m, заявка.Amount);
    }

    [Fact]
    public async Task Пока_маршрут_не_дошёл_до_органа_заявки_в_очереди_нет()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, этапАктивен: false);

        var очередь = await new AgendaCandidateService(db, new DocumentService(db, new AuditService(db), new NumeratorService(db))).ListAsync(MeetingBody.Board);

        // Заявка ещё ходит по подразделениям: выносить нечего, пока согласование
        // не дошло до этапа, где решение принимает орган.
        Assert.DoesNotContain(очередь, c => c.ProcurementRequestId == стенд.RequestId);
    }

    [Fact]
    public async Task Заявку_куратора_орган_не_рассматривает()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db, орган: ApprovalAuthority.Curator);

        var очередь = await new AgendaCandidateService(db, new DocumentService(db, new AuditService(db), new NumeratorService(db))).ListAsync(MeetingBody.Board);

        Assert.DoesNotContain(очередь, c => c.ProcurementRequestId == стенд.RequestId);
    }

    [Fact]
    public async Task В_очереди_другого_органа_заявок_на_закупку_нет()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        await SeedAsync(db);

        // Заседания Совета директоров и собрания акционеров система не ведёт, а
        // кредитный комитет закупки не утверждает: чужую очередь засорять нечем.
        var кредитный = await new AgendaCandidateService(db, new DocumentService(db, new AuditService(db), new NumeratorService(db))).ListAsync(MeetingBody.CreditCommittee);

        Assert.DoesNotContain(кредитный, c => c.Kind == AgendaCandidateKind.Procurement);
    }

    [Fact]
    public async Task Секретарь_включает_заявку_в_повестку()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var сервис = new AgendaCandidateService(db, new DocumentService(db, new AuditService(db), new NumeratorService(db)));

        var item = await сервис.TakeProcurementIntoAgendaAsync(
            стенд.MeetingId, стенд.RequestId, "О приобретении серверов", null, стенд.Actor);

        Assert.Equal("О приобретении серверов", item.Topic);
        Assert.Equal(стенд.RequestId, item.SourceProcurementRequestId);

        // Взятая в повестку уходит из очереди: работа секретаря по ней сделана.
        var очередь = await сервис.ListAsync(MeetingBody.Board);
        Assert.DoesNotContain(очередь, c => c.ProcurementRequestId == стенд.RequestId);
    }

    [Fact]
    public async Task Дважды_одну_заявку_в_повестку_не_включить()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);
        var сервис = new AgendaCandidateService(db, new DocumentService(db, new AuditService(db), new NumeratorService(db)));

        await сервис.TakeProcurementIntoAgendaAsync(стенд.MeetingId, стенд.RequestId, null, null, стенд.Actor);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => сервис.TakeProcurementIntoAgendaAsync(стенд.MeetingId, стенд.RequestId, null, null, стенд.Actor));
    }

    [Fact]
    public async Task На_заседание_другого_органа_заявку_не_включить()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var стенд = await SeedAsync(db);

        var кредитный = new Meeting
        {
            Body = MeetingBody.CreditCommittee,
            Date = new DateOnly(2026, 10, 1),
            Form = MeetingForm.InPerson,
            SecretaryUserId = стенд.Actor,
        };
        db.Meetings.Add(кредитный);
        await db.SaveChangesAsync();

        var ошибка = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new AgendaCandidateService(db, new DocumentService(db, new AuditService(db), new NumeratorService(db)))
                .TakeProcurementIntoAgendaAsync(кредитный.Id, стенд.RequestId, null, null, стенд.Actor));

        Assert.Contains("другого органа", ошибка.Message);
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    private sealed record Стенд(int RequestId, int MeetingId, int Actor);

    private static async Task<Стенд> SeedAsync(
        DelosferaDbContext db,
        bool этапАктивен = true,
        ApprovalAuthority орган = ApprovalAuthority.Board)
    {
        var автор = new User
        {
            FullName = "Инициатор закупки",
            Email = $"agenda-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(автор);
        await db.SaveChangesAsync();

        var method = await db.ProcurementMethods.AsNoTracking().FirstAsync();

        var doc = new Document
        {
            Type = DocumentType.Procurement,
            Title = "Заявка на закупку",
            StatusCode = "OnApproval",
            RegNumber = "ЗК-2026-9001",
            AuthorId = автор.Id,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        var request = new ProcurementRequest
        {
            DocumentId = doc.Id,
            Subject = "Серверное оборудование",
            SubjectKind = ProcurementSubjectKind.Goods,
            Amount = 5_000_000m,
            MethodId = method.Id,
            ApprovalAuthority = орган,
        };
        db.ProcurementRequests.Add(request);
        await db.SaveChangesAsync();

        var instance = new RouteInstance
        {
            DocumentId = doc.Id,
            Status = RouteInstanceStatus.Running,
            Steps =
            [
                new RouteStep
                {
                    Order = 1,
                    Kind = StepKind.Board,
                    Mode = StepMode.Sequential,
                    Participants =
                    [
                        new RouteParticipant
                        {
                            UserId = автор.Id,
                            Required = true,
                            State = этапАктивен ? ParticipantState.Active : ParticipantState.Pending,
                        },
                    ],
                },
            ],
        };
        db.RouteInstances.Add(instance);

        var meeting = new Meeting
        {
            Body = MeetingBody.Board,
            Date = new DateOnly(2026, 10, 1),
            Form = MeetingForm.InPerson,
            SecretaryUserId = автор.Id,
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        return new Стенд(request.Id, meeting.Id, автор.Id);
    }
}
