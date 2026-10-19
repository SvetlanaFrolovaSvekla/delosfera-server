using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Substitutions.DTO;
using delosfera_server.Modules.Substitutions.Models;
using delosfera_server.Modules.Substitutions.Services;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Workflow.Services;
using Microsoft.EntityFrameworkCore;

namespace Delosfera.Tests;

/// <summary>
/// Маршрут заявки на замещение (КСЗ-В9) глазами согласующего и доступ к карточке.
///
/// Раньше согласующему не приходило ничего — ни задачи, ни уведомления, — а реестр
/// без права на полный список показывал только свои заявки: директор филиала не
/// находил заявку, пришедшую к нему. Карточку же с паспортом и ИНН замещающего,
/// наоборот, открывал любой вошедший по одному {id}.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SubstitutionApprovalFlowTests
{
    private readonly PostgresFixture _postgres;

    public SubstitutionApprovalFlowTests(PostgresFixture postgres) => _postgres = postgres;

    [Fact]
    public async Task Отправка_уведомляет_первого_согласующего_и_ставит_ему_задачу()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, ops, hr) = await SeedRoutingAsync(db);
        var notifications = new RecordingNotifications();
        var service = Service(db, notifications);

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        await service.SubmitAsync(id, initiator);

        var sent = Assert.Single(notifications.Sent);
        Assert.Equal(new[] {ops}, sent.UserIds);
        Assert.Equal($"/substitutions/{id}", sent.Url);

        // В общем реестре задач — у первого согласующего, но не у следующего.
        var opsInbox = await Inbox(db).GetAsync(ops);
        var task = Assert.Single(opsInbox.Tasks, t => t.DocumentType == TaskInboxService.SubstitutionDocumentType);
        Assert.Equal(id, task.EntityId);
        Assert.NotNull(task.DueAt);
        Assert.False(task.IsOverdue);

        Assert.DoesNotContain((await Inbox(db).GetAsync(hr)).Tasks,
            t => t.DocumentType == TaskInboxService.SubstitutionDocumentType);
    }

    [Fact]
    public async Task Согласование_передаёт_задачу_следующему_а_в_конце_извещает_инициатора()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, ops, hr) = await SeedRoutingAsync(db);
        var notifications = new RecordingNotifications();
        var service = Service(db, notifications);

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        await service.SubmitAsync(id, initiator);
        await service.ApproveAsync(id, ops, "Согласен");

        Assert.Equal(new[] {hr}, notifications.Sent[^1].UserIds);
        Assert.DoesNotContain((await Inbox(db).GetAsync(ops)).Tasks,
            t => t.DocumentType == TaskInboxService.SubstitutionDocumentType);
        Assert.Contains((await Inbox(db).GetAsync(hr)).Tasks,
            t => t.DocumentType == TaskInboxService.SubstitutionDocumentType && t.EntityId == id);

        var done = await service.ApproveAsync(id, hr, null);

        Assert.Equal(nameof(SubstitutionStatus.OnExecution), done.Status);
        Assert.Equal(new[] {initiator}, notifications.Sent[^1].UserIds);
    }

    [Fact]
    public async Task Отклонение_извещает_инициатора_с_комментарием()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, ops, _) = await SeedRoutingAsync(db);
        var notifications = new RecordingNotifications();
        var service = Service(db, notifications);

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        await service.SubmitAsync(id, initiator);
        await service.RejectAsync(id, ops, "Не указан член комиссии");

        var last = notifications.Sent[^1];
        Assert.Equal(new[] {initiator}, last.UserIds);
        Assert.Contains("Не указан член комиссии", last.BodyRu);
    }

    [Fact]
    public async Task Карточку_видят_инициатор_согласующие_и_УЧР_но_не_посторонний()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, ops, hr) = await SeedRoutingAsync(db);
        var stranger = await UserAsync(db, "Посторонний");
        var service = Service(db, new RecordingNotifications());

        var id = (await service.CreateAsync(Draft(), initiator)).Id;

        // Черновик — маршрута ещё нет: видит только инициатор.
        Assert.True(await service.CanViewAsync(id, initiator, isPrivileged: false));
        Assert.False(await service.CanViewAsync(id, ops, isPrivileged: false));

        await service.SubmitAsync(id, initiator);

        Assert.True(await service.CanViewAsync(id, ops, isPrivileged: false));
        // Согласующий следующего этапа видит заявку заранее — она уже в его маршруте.
        Assert.True(await service.CanViewAsync(id, hr, isPrivileged: false));
        Assert.False(await service.CanViewAsync(id, stranger, isPrivileged: false));
        Assert.True(await service.CanViewAsync(id, stranger, isPrivileged: true));
    }

    [Fact]
    public async Task Реестр_согласующего_содержит_заявку_а_у_постороннего_её_нет()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, ops, _) = await SeedRoutingAsync(db);
        var stranger = await UserAsync(db, "Посторонний");
        var service = Service(db, new RecordingNotifications());

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        await service.SubmitAsync(id, initiator);

        var forOps = await service.SearchAsync(null, null, mineOnly: false, ops, 1, 50, participantOnly: true);
        Assert.Contains(forOps.Items, x => x.Id == id);

        // «Только мои» — по-прежнему только где пользователь инициатор.
        var opsMine = await service.SearchAsync(null, null, mineOnly: true, ops, 1, 50, participantOnly: true);
        Assert.DoesNotContain(opsMine.Items, x => x.Id == id);

        var forStranger = await service.SearchAsync(null, null, mineOnly: false, stranger, 1, 50, participantOnly: true);
        Assert.DoesNotContain(forStranger.Items, x => x.Id == id);
    }

    [Fact]
    public async Task Отклонение_без_причины_не_проходит()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, ops, _) = await SeedRoutingAsync(db);
        var service = Service(db, new RecordingNotifications());

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        await service.SubmitAsync(id, initiator);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(id, ops, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RejectAsync(id, ops, "   "));

        var still = (await service.GetAsync(id))!;
        Assert.Equal(nameof(SubstitutionStatus.OnApproval), still.Status);
    }

    [Fact]
    public async Task Повторная_отправка_сохраняет_историю_прошлого_круга()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, ops, hr) = await SeedRoutingAsync(db);
        var service = Service(db, new RecordingNotifications());

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        await service.SubmitAsync(id, initiator);
        await service.RejectAsync(id, ops, "Не указан член комиссии");
        var resent = await service.SubmitAsync(id, initiator);

        // Первый круг на месте: отклонение с причиной и этап, до которого очередь не дошла.
        var round1 = resent.Approvals.Where(a => a.Round == 1).ToList();
        Assert.Contains(round1, a => a.UserId == ops && a.State == nameof(SubstitutionApprovalState.Rejected)
                                     && a.Comment == "Не указан член комиссии");
        Assert.Contains(round1, a => a.UserId == hr && a.State == nameof(SubstitutionApprovalState.Skipped));

        // Второй круг — заново с первого этапа.
        var round2 = resent.Approvals.Where(a => a.Round == 2).OrderBy(a => a.Order).ToList();
        Assert.Equal(2, round2.Count);
        Assert.Equal(nameof(SubstitutionApprovalState.Active), round2[0].State);
        Assert.Equal(nameof(SubstitutionApprovalState.Pending), round2[1].State);

        // Согласование второго круга двигает именно его, а не этапы прошлого.
        var afterOps = await service.ApproveAsync(id, ops, null);
        Assert.Equal(nameof(SubstitutionApprovalState.Active),
            afterOps.Approvals.Single(a => a.Round == 2 && a.UserId == hr).State);
        Assert.Equal(nameof(SubstitutionApprovalState.Skipped),
            afterOps.Approvals.Single(a => a.Round == 1 && a.UserId == hr).State);
    }

    [Fact]
    public async Task Замещающий_согласующего_видит_и_согласует_заявку()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, ops, hr) = await SeedRoutingAsync(db);
        var deputy = await UserAsync(db, "Замещающий Операционного управления");
        await AddSubstitutionAsync(db, ops, deputy);
        var notifications = new RecordingNotifications();
        var service = Service(db, notifications);

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        await service.SubmitAsync(id, initiator);

        // Уведомление ушло и согласующему, и его замещающему.
        Assert.Contains(deputy, notifications.Sent[^1].UserIds);
        Assert.True(await service.CanViewAsync(id, deputy, isPrivileged: false));

        var inbox = await Inbox(db, deputy);
        var task = Assert.Single(inbox.Tasks, t => t.DocumentType == TaskInboxService.SubstitutionDocumentType);
        Assert.NotNull(task.OnBehalfOf);

        var approved = await service.ApproveAsync(id, deputy, "Согласовано за отсутствующего");
        var opsStep = approved.Approvals.Single(a => a.UserId == ops);
        Assert.Equal(nameof(SubstitutionApprovalState.Approved), opsStep.State);
        Assert.Equal(deputy, opsStep.DecidedByUserId);
        Assert.Equal(nameof(SubstitutionApprovalState.Active), approved.Approvals.Single(a => a.UserId == hr).State);
    }

    [Fact]
    public async Task Посторонний_этап_не_согласует()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, _, hr) = await SeedRoutingAsync(db);
        var service = Service(db, new RecordingNotifications());

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        await service.SubmitAsync(id, initiator);

        // Кадровик УЧР — следующий этап, но сейчас не его очередь.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(id, hr, null));
    }

    [Fact]
    public async Task Заблокированный_согласующий_останавливает_отправку_с_понятной_причиной()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, _, hr) = await SeedRoutingAsync(db);
        var blocked = await db.Users.SingleAsync(u => u.Id == hr);
        blocked.BlockedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var service = Service(db, new RecordingNotifications());

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(id, initiator));

        Assert.Contains("УЧР", error.Message);
        Assert.Equal(nameof(SubstitutionStatus.Draft), (await service.GetAsync(id))!.Status);
    }

    [Fact]
    public async Task Этап_переназначается_действующему_сотруднику()
    {
        await using var db = await _postgres.NewIsolatedDbAsync();
        var (initiator, ops, hr) = await SeedRoutingAsync(db);
        var replacement = await UserAsync(db, "Новый согласующий");
        var blockedUser = await UserAsync(db, "Заблокированный");
        var blocked = await db.Users.SingleAsync(u => u.Id == blockedUser);
        blocked.IsActive = false;
        await db.SaveChangesAsync();
        var notifications = new RecordingNotifications();
        var service = Service(db, notifications);

        var id = (await service.CreateAsync(Draft(), initiator)).Id;
        var submitted = await service.SubmitAsync(id, initiator);
        var opsStep = submitted.Approvals.Single(a => a.UserId == ops);

        // Без причины, на заблокированного, на того же — нельзя.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReassignStepAsync(id, opsStep.Id, replacement, " ", initiator));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReassignStepAsync(id, opsStep.Id, blockedUser, "Уволен", initiator));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReassignStepAsync(id, opsStep.Id, ops, "Уволен", initiator));

        var reassigned = await service.ReassignStepAsync(id, opsStep.Id, replacement, "Согласующий уволен", initiator);

        var step = reassigned.Approvals.Single(a => a.Id == opsStep.Id);
        Assert.Equal(replacement, step.UserId);
        Assert.Equal(nameof(SubstitutionApprovalState.Active), step.State);
        Assert.Contains(replacement, notifications.Sent[^1].UserIds);

        // Прежний согласующий решить уже не может, новый — может.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(id, ops, null));
        var approved = await service.ApproveAsync(id, replacement, null);
        Assert.Equal(nameof(SubstitutionApprovalState.Active), approved.Approvals.Single(a => a.UserId == hr).State);

        // Пройденный этап не переназначается.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReassignStepAsync(id, opsStep.Id, ops, "Вернуть", initiator));
    }

    // ── стенд ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Маршрут ГО: Операционное управление → УЧР (у отсутствующего нет подразделения,
    /// значит и этапа директора филиала). Согласующие берутся из настроек, а не из
    /// зашитых в код id.
    /// </summary>
    private static async Task<(int Initiator, int Ops, int Hr)> SeedRoutingAsync(DelosferaDbContext db)
    {
        var initiator = await UserAsync(db, "Инициатор заявки");
        var ops = await UserAsync(db, "Операционное управление");
        var hr = await UserAsync(db, "Кадровик УЧР ГО");

        var settings = await db.HrRoutingSettings.FirstOrDefaultAsync();
        if (settings is null)
        {
            settings = new HrRoutingSettings {CreatedAt = DateTime.UtcNow};
            db.HrRoutingSettings.Add(settings);
        }
        settings.OperationsApproverUserId = ops;
        settings.OperationsFallbackUserId = null;
        settings.HeadOfficeHrUserId = hr;
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (initiator, ops, hr);
    }

    private static SubstitutionService Service(DelosferaDbContext db, RecordingNotifications notifications) =>
        new(db, new AuditService(db), new NumeratorService(db), new BankClock(), new NoopPrint(), notifications);

    private static TaskInboxService Inbox(DelosferaDbContext db) =>
        new(db, new NoSubstitutions(), new BankClock());

    /// <summary>Реестр задач с настоящими замещениями из базы (GEN-14).</summary>
    private static Task<delosfera_server.Modules.Workflow.DTO.TaskInboxDto> Inbox(DelosferaDbContext db, int userId) =>
        new TaskInboxService(db, new BaseActingFor(db), new BankClock()).GetAsync(userId);

    private static async Task AddSubstitutionAsync(DelosferaDbContext db, int absentUserId, int substituteUserId)
    {
        db.Substitutions.Add(new Substitution
        {
            UserId = absentUserId,
            SubstituteUserId = substituteUserId,
            StartsOn = WorkingCalendar.Today.AddDays(-1),
            EndsOn = WorkingCalendar.Today.AddDays(10),
            Reason = "Отпуск",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Замещения из базы для реестра задач — как в приложении (Users.SubstitutionService).</summary>
    private sealed class BaseActingFor(DelosferaDbContext db) : delosfera_server.Modules.Users.Services.ISubstitutionService
    {
        public Task<List<delosfera_server.Modules.Users.DTO.SubstitutionDto>> ListAsync(int? userId) =>
            throw new NotSupportedException();

        public Task<delosfera_server.Modules.Users.DTO.SubstitutionDto> CreateAsync(
            delosfera_server.Modules.Users.DTO.SubstitutionCreateRequest request, int actorUserId) =>
            throw new NotSupportedException();

        public Task<delosfera_server.Modules.Users.DTO.SubstitutionDto> CancelAsync(int id, int actorUserId) =>
            throw new NotSupportedException();

        public Task<List<int>> GetActingForUserIdsAsync(int substituteUserId) =>
            delosfera_server.Modules.Users.Services.ActingFor.AbsentUserIdsAsync(db, substituteUserId, WorkingCalendar.Today);
    }

    private static SubstitutionSaveRequest Draft() => new()
    {
        Subject = "Замещение на время отпуска",
        Reason = SubstitutionReason.Vacation,
        AbsentName = "Отсутствующий сотрудник",
        SubstituteName = "Замещающий сотрудник",
        StartsOn = WorkingCalendar.Today.AddDays(7),
        EndsOn = WorkingCalendar.Today.AddDays(21),
    };

    private static async Task<int> UserAsync(DelosferaDbContext db, string fullName)
    {
        var user = new User
        {
            FullName = fullName,
            Email = $"sub-flow-{Guid.NewGuid():N}@keremetbank.kg",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Запоминает уведомления вместо рассылки.</summary>
    private sealed class RecordingNotifications : NoopNotificationService
    {
        public List<CreateNotificationRequest> Sent { get; } = [];

        public override Task<int> CreateAsync(CreateNotificationRequest request, int? currentUserId)
        {
            Sent.Add(request);
            return Task.FromResult(0);
        }
    }

    private sealed class NoopPrint : ISubstitutionPrintService
    {
        public byte[] Order(SubstitutionRequest r) => [];
        public byte[] Liability(SubstitutionRequest r) => [];
        public byte[] Card(SubstitutionRequest r) => [];
    }
}
