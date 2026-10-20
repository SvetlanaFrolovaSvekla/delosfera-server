using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Substitutions.Models;
using delosfera_server.Modules.Sz.Models;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Modules.Users.Services;
using delosfera_server.Modules.Workflow.DTO;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.Services;

public interface ITaskInboxService
{
    /// <summary>
    /// Открытые задачи пользователя по всем контурам (GEN-11).
    ///
    /// includeOwn — добавить в список записки, где пользователь инициатор и которые
    /// сейчас ждут других (регистрации, согласования, подписи...). Это не задачи, а
    /// отслеживание, поэтому по умолчанию выключено: дайджест, рабочий стол и
    /// статистика считают только реальную работу.
    /// </summary>
    Task<TaskInboxDto> GetAsync(int userId, string? documentType = null, bool includeOwn = false);

    /// <summary>
    /// Выполненные задачи пользователя по служебным запискам (вкладка «Выполненные»):
    /// закрытые задачи движка и записки, которые он зарегистрировал. Свежие первыми.
    /// </summary>
    Task<TaskInboxDto> GetDoneSzAsync(int userId, int limit = 200);

    /// <summary>Статистика по открытым задачам пользователя (ЗД-1).</summary>
    Task<TaskStatsDto> StatsAsync(int userId);
}

/// <summary>
/// Сводный реестр задач. Задачи движка не знают о контурах, поэтому документ
/// достаётся через цепочку участник → этап → маршрут → документ: без неё в списке
/// были бы безымянные строки «Approval», по которым не понять, что открывать.
///
/// Включает задачи тех, кого пользователь замещает (GEN-14), с пометкой, за кого
/// именно они выполняются.
/// </summary>
public class TaskInboxService : ITaskInboxService
{
    private readonly DelosferaDbContext _db;
    private readonly ISubstitutionService _substitutions;
    private readonly IBankClock _clock;

    public TaskInboxService(DelosferaDbContext db, ISubstitutionService substitutions, IBankClock clock)
    {
        _db = db;
        _substitutions = substitutions;
        _clock = clock;
    }

    public async Task<TaskInboxDto> GetAsync(int userId, string? documentType = null, bool includeOwn = false)
    {
        var actingFor = await _substitutions.GetActingForUserIdsAsync(userId);
        var assignees = actingFor.Append(userId).Distinct().ToList();

        var names = await _db.Users
            .Where(u => actingFor.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        // Задачи согласования: документ достаётся через цепочку участник → этап → маршрут.
        var routeRows = await (
            from t in _db.WorkflowTasks
            join p in _db.RouteParticipants on t.RouteParticipantId equals p.Id
            join st in _db.RouteSteps on p.RouteStepId equals st.Id
            join ri in _db.RouteInstances on st.RouteInstanceId equals ri.Id
            join d in _db.Documents on ri.DocumentId equals d.Id
            where assignees.Contains(t.AssigneeUserId) && t.State == WorkflowTaskState.Open
            select new Row
            {
                Id = t.Id,
                ParticipantId = p.Id,
                AssigneeUserId = t.AssigneeUserId,
                TaskType = t.Type,
                DueAt = t.DueAt,
                CreatedAt = t.CreatedAt,
                Order = st.Order,
                Kind = (StepKind?)st.Kind,
                DocumentId = d.Id,
                RegNumber = d.RegNumber,
                Title = d.Title,
                DocType = d.Type,
                DocStatus = d.StatusCode,
                DelegatedByUserId = t.DelegatedByUserId,
            }).ToListAsync();

        // Задачи контура — решение адресата, поручение: маршрута за ними нет, документ
        // указан напрямую. Без них список задач показывал бы только согласования, а
        // работа, выданная резолюцией, была бы видна лишь внутри своего раздела.
        var directRows = await (
            from t in _db.WorkflowTasks
            join d in _db.Documents on t.DocumentId equals d.Id
            where assignees.Contains(t.AssigneeUserId)
               && t.State == WorkflowTaskState.Open
               && t.RouteParticipantId == null
            select new Row
            {
                Id = t.Id,
                ParticipantId = null,
                AssigneeUserId = t.AssigneeUserId,
                TaskType = t.Type,
                DueAt = t.DueAt,
                CreatedAt = t.CreatedAt,
                Order = null,
                Kind = null,
                DocumentId = d.Id,
                RegNumber = d.RegNumber,
                Title = d.Title,
                DocType = d.Type,
                DocStatus = d.StatusCode,
                DelegatedByUserId = t.DelegatedByUserId,
            }).ToListAsync();

        // Ознакомление: лист ознакомления живёт вне движка задач — своя таблица без
        // маршрута. Без него сотрудник видит поручения и согласования, но не листы, с
        // которыми обязан расписаться, и «я этого не видел» ловится только вручную.
        var ackRows = await (
            from e in _db.AcknowledgementEntries
            join s in _db.AcknowledgementSheets on e.SheetId equals s.Id
            where assignees.Contains(e.UserId)
               && e.State == AcknowledgementState.Pending
               && s.ClosedAt == null
            select new
            {
                e.Id,
                e.UserId,
                e.CreatedAt,
                Instruction = s.Instruction,
                s.DueDate,
                s.DocumentId,
                DocTitle = s.Document != null ? s.Document.Title : null,
            }).ToListAsync();

        // ВНД-согласование: у нормативки свой контур согласования (VndApprovalProcess/
        // Stage), не движок задач. В единый список берём только этапы, где решение сейчас
        // за пользователем — все три фазы: первичная, повторная, финальная выдержка.
        // Актуализация и консолидация остаются в своей вкладке: там логика цикла, а не
        // одна задача согласования (см. VND TasksService).
        var vndStages = await _db.VndApprovalStages
            .Include(s => s.ApprovalProcess).ThenInclude(p => p!.Vnd)
            .Where(s => assignees.Contains(s.ApproverUserId))
            .Where(s =>
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.Primary
                 && s.PrimaryDecision == ApprovalStageDecision.Pending)
                ||
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.Repeated
                 && s.ParticipatesInRepeat
                 && (s.RepeatDecision == null || s.RepeatDecision == ApprovalStageDecision.Pending))
                ||
                (s.ApprovalProcess!.Status == ApprovalProcessStatus.FinalHold
                 && (s.FinalHoldDecision == null || s.FinalHoldDecision == ApprovalStageDecision.Pending)))
            .ToListAsync();

        // Согласование заявок на замещение (КСЗ-В9): у них свой маршрут этапов, не движок
        // задач, и в реестр они не попадали вовсе — согласующий узнавал о заявке только из
        // напоминания о просрочке. Этапы тех, кого пользователь замещает (GEN-14), — тоже
        // его: согласовать за отсутствующего он вправе.
        var substitutionRows = await _db.SubstitutionApprovals.AsNoTracking()
            .Where(a => assignees.Contains(a.UserId)
                        && a.State == SubstitutionApprovalState.Active
                        && a.Request!.Status == SubstitutionStatus.OnApproval)
            .Select(a => new
            {
                a.Id,
                a.RequestId,
                a.RoleLabel,
                a.UserId,
                a.ActivatedAt,
                a.Request!.RegNumber,
                a.Request.Subject,
            })
            .ToListAsync();

        // Исполнение заявок на замещение в УЧР (приказ на время замещения). После последнего
        // этапа маршрута заявка уходит «На исполнение», и задачи у кадровика не оставалось —
        // заявку можно было найти только в реестре. Исполняет кадровик этапа «УЧР» действующего
        // круга (сам или его замещающий); если этапа УЧР в маршруте не было — кадровики из
        // настроек маршрута УЧР (ГО и филиалы).
        var hrSettings = await _db.HrRoutingSettings.AsNoTracking()
            .Select(x => new { x.HeadOfficeHrUserId, x.BranchHrUserId })
            .FirstOrDefaultAsync();
        var isRoutingHr = hrSettings is not null
                          && ((hrSettings.HeadOfficeHrUserId is { } hoHr && assignees.Contains(hoHr))
                              || (hrSettings.BranchHrUserId is { } brHr && assignees.Contains(brHr)));
        var substitutionExecutionRows = await _db.SubstitutionRequests.AsNoTracking()
            .Where(r => r.Status == SubstitutionStatus.OnExecution
                        && (r.Approvals.Any(a => a.RoleLabel == HrStepRole
                                                 && a.Round == r.Approvals.Max(x => x.Round)
                                                 && assignees.Contains(a.UserId))
                            || (isRoutingHr && !r.Approvals.Any(a => a.RoleLabel == HrStepRole
                                                                     && a.Round == r.Approvals.Max(x => x.Round)))))
            .Select(r => new
            {
                r.Id,
                r.RegNumber,
                r.Subject,
                r.UpdatedAt,
                HrUserId = r.Approvals
                    .Where(a => a.RoleLabel == HrStepRole && a.Round == r.Approvals.Max(x => x.Round))
                    .Select(a => (int?)a.UserId)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        // Норматив этапа (ЗМ-SLA) — тот же, по которому шлются напоминания.
        var substitutionSlaDays = substitutionRows.Count == 0
            ? 0
            : await _db.SubstitutionSlaSettings.AsNoTracking()
                  .Select(x => (int?)x.ApprovalStepSlaDays)
                  .FirstOrDefaultAsync() ?? DefaultSubstitutionSlaDays;

        // Регистрация СЗ: очередь Сектора делопроизводства, а не задача движка — записка
        // ждёт любого, у кого есть право регистрировать, а не конкретного человека. Без
        // этого записка «на регистрации» была видна только в реестре, и делопроизводство
        // узнавало о ней, лишь открыв нужную вкладку.
        var registrationRows = await HasPermissionAsync(userId, PermissionCode.RegisterSz)
            ? await _db.SzDocuments
                .Where(x => x.Document!.StatusCode == SzStatus.PendingRegistration)
                .Select(x => new RegistrationRow(x.Id, x.DocumentId, x.Document!.Title, x.Document.UpdatedAt))
                .ToListAsync()
            : new List<RegistrationRow>();

        // Задачи контура СЗ, оставшиеся от прежнего статуса записки: «Устранение
        // замечаний» после отзыва, «Решение по записке» после ручного перевода статуса.
        // Их закрывают не все пути смены статуса, поэтому сверяемся с текущим: задача
        // показывается, только пока записка в том состоянии, ради которого её выдали.
        directRows = directRows
            .Where(r => r.DocType != DocumentType.Sz
                        || r.TaskType switch
                        {
                            "RemarksResolution" => r.DocStatus == SzStatus.OnRevision,
                            "AddresseeDecision" => r.DocStatus == SzStatus.OnAddresseeDecision,
                            // Задача автора «Моя СЗ на согласовании» — только пока идёт согласование.
                            "SzApprovalTracking" => r.DocStatus == SzStatus.OnApproval
                                                    || r.DocStatus == SzStatus.Registered,
                            _ => true,
                        })
            .ToList();

        var rows = routeRows.Concat(directRows).ToList();

        // Резолюция по подписанной записке: после подписи записка сразу «на исполнении»,
        // и следующий шаг — резолюция адресата (он же подписант), отписка исполнителям.
        // Отдельной задачи на это движок не создаёт, и без этой строки подписанная
        // записка пропадала из «Моих задач» ровно тогда, когда от адресата ждут работы.
        // Если адресат уже вынес решение по существу (путь без подписания) — резолюция
        // не обязательна, строку не показываем.
        var resolutionRows = await _db.SzDocuments
            .Where(x => x.Document!.StatusCode == SzStatus.OnExecution
                        && x.ExecutionResolution == null
                        && x.AddresseeDecision == null
                        && x.AddresseeUserId != null
                        && assignees.Contains(x.AddresseeUserId.Value))
            .Select(x => new
            {
                SzId = x.Id,
                x.DocumentId,
                x.Document!.Title,
                x.Document.RegNumber,
                AddresseeUserId = x.AddresseeUserId!.Value,
                x.DueDate,
                x.Document.UpdatedAt,
            })
            .ToListAsync();

        // Имена тех, кто делегировал задачи (СК-3): в реестре у делегата стоит «от кого».
        var delegatedByIds = rows
            .Where(r => r.DelegatedByUserId is not null)
            .Select(r => r.DelegatedByUserId!.Value)
            .Distinct()
            .ToList();

        var delegatedByNames = delegatedByIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Users
                .Where(u => delegatedByIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName);

        // Карточки контуров открываются по своему идентификатору, а не по документу:
        // /sz/{szId}, /prc/{requestId}. Без этой подстановки задача уводила на чужую
        // карточку с тем же числом.
        var documentIds = rows.Select(r => r.DocumentId).Distinct().ToList();

        var entityIds = (await _db.SzDocuments
                .Where(x => documentIds.Contains(x.DocumentId))
                .Select(x => new {x.DocumentId, EntityId = x.Id})
                .ToListAsync())
            .Concat(await _db.ProcurementRequests
                .Where(x => documentIds.Contains(x.DocumentId))
                .Select(x => new {x.DocumentId, EntityId = x.Id})
                .ToListAsync())
            .ToDictionary(x => x.DocumentId, x => x.EntityId);

        var now = DateTime.UtcNow;

        // Срок листа задан датой без времени: считаем просроченным после конца дня,
        // а не с полуночи — иначе «до 14-го» гасло бы утром 14-го.
        static DateTime? AckDue(DateOnly? d) => d?.ToDateTime(new TimeOnly(23, 59, 59));

        var ackTasks = ackRows.Select(a => new InboxTaskDto
        {
            TaskId = a.Id,
            ParticipantId = null,
            // Лист открывается на общей странице ознакомления, не по своей карточке:
            // отдельного экрана листа нет, ссылка ведёт в раздел (см. taskLink).
            DocumentId = a.DocumentId ?? 0,
            EntityId = null,
            RegNumber = null,
            DocumentTitle = a.DocTitle ?? a.Instruction ?? "Ознакомление с документом",
            DocumentType = "Acknowledgement",
            DocumentTypeTitle = "Ознакомление",
            TaskType = "Ознакомление",
            StepOrder = null,
            StepKind = null,
            DueAt = AckDue(a.DueDate),
            IsOverdue = AckDue(a.DueDate) is { } due && due < now,
            OnBehalfOf = a.UserId != userId && names.TryGetValue(a.UserId, out var an) ? an : null,
            CreatedAt = a.CreatedAt,
        });

        var vndTasks = vndStages.Select(s =>
        {
            var p = s.ApprovalProcess!;
            // Срок берём по текущей фазе: у повторной и финальной он свой, и показать
            // срок первичной на финальной выдержке — сбить с толку.
            DateTime? due = p.Status switch
            {
                ApprovalProcessStatus.Primary => p.PrimaryDeadlineAt,
                ApprovalProcessStatus.Repeated => p.RepeatDeadlineAt,
                ApprovalProcessStatus.FinalHold => p.FinalHoldDeadlineAt,
                _ => null,
            };
            return new InboxTaskDto
            {
                TaskId = s.Id,
                ParticipantId = null,
                // Карточка ВНД открывается по своему id: /base-vnd/{vndId} (см. taskLink).
                DocumentId = 0,
                EntityId = p.VndId,
                RegNumber = p.Vnd!.Code,
                DocumentTitle = p.Vnd!.TitleRu,
                DocumentType = "Vnd",
                DocumentTypeTitle = "ВНД",
                TaskType = "Согласование",
                StepOrder = null,
                StepKind = null,
                DueAt = due,
                IsOverdue = due is { } d && d < now,
                OnBehalfOf = s.ApproverUserId != userId && names.TryGetValue(s.ApproverUserId, out var vn) ? vn : null,
                CreatedAt = p.PrimaryStartedAt,
            };
        });

        var substitutionTasks = substitutionRows.Select(a =>
        {
            var due = a.ActivatedAt is { } activated
                ? SubstitutionStepDueUtc(activated, substitutionSlaDays)
                : (DateTime?)null;
            return new InboxTaskDto
            {
                TaskId = a.Id,
                ParticipantId = null,
                // Карточка заявки открывается по её id: /substitutions/{id} (см. taskLink).
                DocumentId = 0,
                EntityId = a.RequestId,
                RegNumber = a.RegNumber,
                DocumentTitle = a.Subject,
                DocumentType = SubstitutionDocumentType,
                DocumentTypeTitle = "Заявка на замещение",
                TaskType = $"Согласование: {a.RoleLabel}",
                StepOrder = null,
                StepKind = null,
                DueAt = due,
                IsOverdue = due is { } d && d < now,
                OnBehalfOf = a.UserId != userId && names.TryGetValue(a.UserId, out var absentName) ? absentName : null,
                CreatedAt = a.ActivatedAt ?? now,
            };
        });

        var substitutionExecutionTasks = substitutionExecutionRows.Select(r => new InboxTaskDto
        {
            TaskId = r.Id,
            ParticipantId = null,
            DocumentId = 0,
            EntityId = r.Id,
            RegNumber = r.RegNumber,
            DocumentTitle = r.Subject,
            DocumentType = SubstitutionDocumentType,
            DocumentTypeTitle = "Заявка на замещение",
            TaskType = "Исполнение: приказ о замещении",
            StepOrder = null,
            StepKind = null,
            // Норматива на исполнение нет — срок не ставим.
            DueAt = null,
            IsOverdue = false,
            OnBehalfOf = r.HrUserId is { } hr && hr != userId && names.TryGetValue(hr, out var hrName) ? hrName : null,
            // С момента, когда заявка ушла на исполнение, — последнее её изменение.
            CreatedAt = r.UpdatedAt,
        });

        var registrationTasks = registrationRows.Select(r => new InboxTaskDto
        {
            TaskId = r.SzId,
            ParticipantId = null,
            DocumentId = r.DocumentId,
            EntityId = r.SzId,
            RegNumber = null,
            DocumentTitle = r.Title,
            DocumentType = DocumentType.Sz.ToString(),
            DocumentTypeTitle = DocumentTypeTitle(DocumentType.Sz),
            TaskType = "Регистрация",
            StepOrder = null,
            StepKind = null,
            Stage = SzStage.Registration,
            // Норматив регистрации не задан: срок исполнения записке назначается
            // только при регистрации, до неё сроку взяться неоткуда.
            DueAt = null,
            IsOverdue = false,
            OnBehalfOf = null,
            // Когда записку отправили на регистрацию — последнее изменение документа.
            CreatedAt = r.SentAt,
        });

        // Свои записки инициатора, которые сейчас ждут других. Без них автор видел
        // свою записку только в реестре СЗ и не понимал, на каком она шаге — ждёт ли
        // регистрации, кто её согласует, дошла ли до подписи.
        var ownTasks = includeOwn && (documentType is null || documentType == DocumentType.Sz.ToString())
            ? await OwnSzTasksAsync(userId, rows, registrationRows,
                resolutionRows.Where(r => r.AddresseeUserId == userId).Select(r => r.DocumentId))
            : new List<InboxTaskDto>();

        var resolutionTasks = resolutionRows.Select(r => new InboxTaskDto
        {
            TaskId = r.SzId,
            ParticipantId = null,
            DocumentId = r.DocumentId,
            EntityId = r.SzId,
            RegNumber = r.RegNumber,
            DocumentTitle = r.Title,
            DocumentType = DocumentType.Sz.ToString(),
            DocumentTypeTitle = DocumentTypeTitle(DocumentType.Sz),
            TaskType = "Резолюция по записке",
            StepOrder = null,
            StepKind = null,
            Stage = SzStage.Decision,
            DueAt = AckDue(r.DueDate),
            IsOverdue = AckDue(r.DueDate) is { } rd && rd < now,
            OnBehalfOf = r.AddresseeUserId != userId && names.TryGetValue(r.AddresseeUserId, out var rn) ? rn : null,
            // С момента, когда записка ушла на исполнение, — последнее изменение документа.
            CreatedAt = r.UpdatedAt,
        });

        var tasks = rows
            .Select(r => new InboxTaskDto
            {
                TaskId = r.Id,
                ParticipantId = r.ParticipantId,
                DocumentId = r.DocumentId,
                EntityId = entityIds.TryGetValue(r.DocumentId, out var entityId) ? entityId : null,
                RegNumber = r.RegNumber,
                DocumentTitle = r.Title,
                DocumentType = r.DocType.ToString(),
                DocumentTypeTitle = DocumentTypeTitle(r.DocType),
                TaskType = TaskTypeTitle(r.TaskType, r.Kind),
                StepOrder = r.Order,
                StepKind = r.Kind?.ToString(),
                Stage = r.DocType == DocumentType.Sz ? SzStageOf(r.TaskType, r.Kind) : null,
                // «Моя СЗ на согласовании» — задача автора на отслеживание: действия от
                // него не ждут, поэтому она идёт как своя записка (без делегирования,
                // в «Всего задач» не считается), но приходит всегда, а не только с includeOwn.
                IsOwn = r.TaskType == "SzApprovalTracking",
                DueAt = r.DueAt,
                IsOverdue = r.DueAt is { } due && due < now,
                OnBehalfOf = r.AssigneeUserId != userId && names.TryGetValue(r.AssigneeUserId, out var name)
                    ? name
                    : null,
                DelegatedBy = r.DelegatedByUserId is { } delegId
                              && delegatedByNames.TryGetValue(delegId, out var delegName)
                    ? delegName
                    : null,
                CreatedAt = r.CreatedAt,
            })
            .Concat(ackTasks)
            .Concat(vndTasks)
            .Concat(substitutionTasks)
            .Concat(substitutionExecutionTasks)
            .Concat(registrationTasks)
            .Concat(resolutionTasks)
            .Concat(ownTasks)
            .Where(t => documentType is null || t.DocumentType == documentType)
            // Просроченные наверх, затем по сроку: реестр должен начинаться с того,
            // что горит, а не с того, что пришло первым.
            .OrderByDescending(t => t.IsOverdue)
            .ThenBy(t => t.DueAt ?? DateTime.MaxValue)
            .ToList();

        // Счётчики — по реальной работе: свои записки на отслеживании считаются
        // отдельно, иначе «Всего задач» росло бы от того, что пользователь сам написал.
        var work = tasks.Where(t => !t.IsOwn).ToList();

        return new TaskInboxDto
        {
            Tasks = tasks,
            Total = work.Count,
            Overdue = work.Count(t => t.IsOverdue),
            Delegated = work.Count(t => t.OnBehalfOf is not null),
            Own = tasks.Count - work.Count,
        };
    }

    /// <summary>Контур заявок на замещение в реестре задач — по нему клиент строит ссылку.</summary>
    public const string SubstitutionDocumentType = "Substitution";

    /// <summary>Роль этапа кадровика в маршруте заявки на замещение (см. SubstitutionService.BuildApprovalRouteAsync).</summary>
    private const string HrStepRole = "УЧР";

    /// <summary>Норматив этапа заявки на замещение, пока администратор его не задал (как в SubstitutionService).</summary>
    private const int DefaultSubstitutionSlaDays = 3;

    /// <summary>
    /// Срок этапа заявки на замещение: конец N-го рабочего дня (пн–пт) после открытия
    /// этапа по времени банка — с того же дня напоминание считает этап просроченным
    /// (SubstitutionReminderNotifier). Праздники не учитываются (COND-1).
    /// </summary>
    private DateTime SubstitutionStepDueUtc(DateTime activatedUtc, int slaDays)
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(activatedUtc, _clock.Zone));
        for (var left = slaDays; left > 0;)
        {
            day = day.AddDays(1);
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) left--;
        }

        var endOfDayLocal = DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(23, 59, 59)), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(endOfDayLocal, _clock.Zone);
    }

    /// <summary>
    /// Статусы записки, в которых она ждёт кого-то, кроме автора, — и как это
    /// подписать инициатору. Черновик и завершённые статусы сюда не входят: черновик
    /// ждёт самого автора, а исполненная или отозванная записка уже ничего не ждёт.
    /// </summary>
    private static readonly Dictionary<string, (string Stage, string Title)> OwnSzStatuses = new()
    {
        [SzStatus.PendingRegistration] = (SzStage.Registration, "Ожидает регистрации"),
        [SzStatus.OnApproval] = (SzStage.Approval, "На согласовании"),
        // Прежний «Зарегистрирована» означал то же самое: номер есть, идёт согласование.
        [SzStatus.Registered] = (SzStage.Approval, "На согласовании"),
        [SzStatus.OnSigning] = (SzStage.Signing, "На подписании"),
        [SzStatus.OnRevision] = (SzStage.Revision, "Возвращена на доработку"),
        [SzStatus.OnAddresseeDecision] = (SzStage.Decision, "Ожидает решения адресата"),
        [SzStatus.OnBoardReview] = (SzStage.Board, "На рассмотрении коллегиального органа"),
        [SzStatus.OnExecution] = (SzStage.Execution, "На исполнении"),
    };

    /// <summary>
    /// Записки, где пользователь — инициатор, со статусом «ждёт других». Если по той
    /// же записке у него уже есть настоящая задача (он сам согласующий, регистратор
    /// или устраняет замечания), строка отслеживания не добавляется — иначе одна
    /// записка висела бы в списке дважды.
    /// </summary>
    private async Task<List<InboxTaskDto>> OwnSzTasksAsync(
        int userId, List<Row> rows, List<RegistrationRow> registrationRows, IEnumerable<int> otherBusyDocumentIds)
    {
        var statuses = OwnSzStatuses.Keys.ToList();

        var own = await _db.SzDocuments
            .Where(x => x.Document!.AuthorId == userId && statuses.Contains(x.Document.StatusCode))
            .Select(x => new
            {
                SzId = x.Id,
                x.DocumentId,
                x.Document!.Title,
                x.Document.RegNumber,
                x.Document.StatusCode,
                x.Document.UpdatedAt,
            })
            .ToListAsync();

        var busy = rows.Where(r => r.AssigneeUserId == userId).Select(r => r.DocumentId)
            .Concat(registrationRows.Select(r => r.DocumentId))
            .Concat(otherBusyDocumentIds)
            .ToHashSet();

        return own
            .Where(x => !busy.Contains(x.DocumentId))
            .Select(x =>
            {
                var (stage, title) = OwnSzStatuses[x.StatusCode];
                return new InboxTaskDto
                {
                    // Своей задачи нет — строка идёт под id записки, как у регистрации;
                    // ключ на клиенте различает их по типу задачи (см. taskKey).
                    TaskId = x.SzId,
                    ParticipantId = null,
                    DocumentId = x.DocumentId,
                    EntityId = x.SzId,
                    RegNumber = x.RegNumber,
                    DocumentTitle = x.Title,
                    DocumentType = DocumentType.Sz.ToString(),
                    DocumentTypeTitle = DocumentTypeTitle(DocumentType.Sz),
                    TaskType = title,
                    StepOrder = null,
                    StepKind = null,
                    Stage = stage,
                    IsOwn = true,
                    // Срок здесь — не срок автора: ему нечего просрочить, пока записка
                    // у других. Поэтому без срока и без «просрочено».
                    DueAt = null,
                    IsOverdue = false,
                    OnBehalfOf = null,
                    // Когда записка перешла в текущий статус — последнее изменение документа.
                    CreatedAt = x.UpdatedAt,
                };
            })
            .ToList();
    }

    /// <summary>Этап записки, к которому относится задача движка, — для вкладок «Мои задачи».</summary>
    private static string SzStageOf(string taskType, StepKind? kind) => taskType switch
    {
        "RemarksResolution" => SzStage.Revision,
        "AddresseeDecision" => SzStage.Decision,
        "SzApprovalTracking" => SzStage.Approval,
        "Assignment" => SzStage.Execution,
        _ => kind switch
        {
            StepKind.Signing => SzStage.Signing,
            StepKind.Board => SzStage.Board,
            _ => SzStage.Approval,
        },
    };

    public async Task<TaskInboxDto> GetDoneSzAsync(int userId, int limit = 200)
    {
        limit = Math.Clamp(limit, 1, 500);

        // Закрытые задачи самого пользователя (не замещаемых): выполненное по замещению
        // числится за тем, кого замещали. Задача «Моя СЗ на согласовании» — отслеживание,
        // а не работа, поэтому в выполненные не попадает.
        var routeRows = await (
            from t in _db.WorkflowTasks
            join p in _db.RouteParticipants on t.RouteParticipantId equals p.Id
            join st in _db.RouteSteps on p.RouteStepId equals st.Id
            join ri in _db.RouteInstances on st.RouteInstanceId equals ri.Id
            join d in _db.Documents on ri.DocumentId equals d.Id
            where t.AssigneeUserId == userId && t.State == WorkflowTaskState.Done
                  && d.Type == DocumentType.Sz && t.Type != "SzApprovalTracking"
            orderby (t.CompletedAt ?? t.CreatedAt) descending
            select new Row
            {
                Id = t.Id,
                ParticipantId = p.Id,
                AssigneeUserId = t.AssigneeUserId,
                TaskType = t.Type,
                DueAt = t.DueAt,
                CreatedAt = t.CreatedAt,
                Order = st.Order,
                Kind = (StepKind?)st.Kind,
                DocumentId = d.Id,
                RegNumber = d.RegNumber,
                Title = d.Title,
                DocType = d.Type,
                DocStatus = d.StatusCode,
                DelegatedByUserId = t.DelegatedByUserId,
                CompletedAt = t.CompletedAt,
            }).Take(limit).ToListAsync();

        var directRows = await (
            from t in _db.WorkflowTasks
            join d in _db.Documents on t.DocumentId equals d.Id
            where t.AssigneeUserId == userId && t.State == WorkflowTaskState.Done
                  && t.RouteParticipantId == null
                  && d.Type == DocumentType.Sz && t.Type != "SzApprovalTracking"
            orderby (t.CompletedAt ?? t.CreatedAt) descending
            select new Row
            {
                Id = t.Id,
                ParticipantId = null,
                AssigneeUserId = t.AssigneeUserId,
                TaskType = t.Type,
                DueAt = t.DueAt,
                CreatedAt = t.CreatedAt,
                Order = null,
                Kind = null,
                DocumentId = d.Id,
                RegNumber = d.RegNumber,
                Title = d.Title,
                DocType = d.Type,
                DocStatus = d.StatusCode,
                DelegatedByUserId = t.DelegatedByUserId,
                CompletedAt = t.CompletedAt,
            }).Take(limit).ToListAsync();

        var rows = routeRows.Concat(directRows).ToList();
        var documentIds = rows.Select(r => r.DocumentId).Distinct().ToList();
        var entityIds = await _db.SzDocuments
            .Where(x => documentIds.Contains(x.DocumentId))
            .ToDictionaryAsync(x => x.DocumentId, x => x.Id);

        var tasks = rows.Select(r => new InboxTaskDto
        {
            TaskId = r.Id,
            ParticipantId = r.ParticipantId,
            DocumentId = r.DocumentId,
            EntityId = entityIds.TryGetValue(r.DocumentId, out var entityId) ? entityId : null,
            RegNumber = r.RegNumber,
            DocumentTitle = r.Title,
            DocumentType = r.DocType.ToString(),
            DocumentTypeTitle = DocumentTypeTitle(r.DocType),
            TaskType = TaskTypeTitle(r.TaskType, r.Kind),
            StepOrder = r.Order,
            StepKind = r.Kind?.ToString(),
            Stage = SzStageOf(r.TaskType, r.Kind),
            DueAt = r.DueAt,
            IsOverdue = false,
            CreatedAt = r.CreatedAt,
            // Закрыта до появления поля — честного времени нет, показываем создание.
            CompletedAt = r.CompletedAt ?? r.CreatedAt,
        }).ToList();

        // Регистрация — очередь Сектора делопроизводства, а не задача движка: выполненной
        // она считается по записке, которую пользователь зарегистрировал.
        var registered = await _db.SzDocuments
            .Where(x => x.RegisteredByUserId == userId && x.RegisteredOn != null)
            .OrderByDescending(x => x.RegisteredOn)
            .Take(limit)
            .Select(x => new {x.Id, x.DocumentId, x.Document!.Title, x.Document.RegNumber, x.RegisteredOn})
            .ToListAsync();

        // Время регистрации — из журнала аудита (в записке хранится только дата).
        var registeredIds = registered.Select(r => r.Id).ToList();
        var registeredAt = (await _db.AuditEntries
                .Where(a => a.EntityType == "Sz" && a.Action == "Registered" && registeredIds.Contains(a.EntityId))
                .GroupBy(a => a.EntityId)
                .Select(g => new {SzId = g.Key, At = g.Max(a => a.At)})
                .ToListAsync())
            .ToDictionary(x => x.SzId, x => x.At);

        tasks.AddRange(registered.Select(r => new InboxTaskDto
        {
            TaskId = r.Id,
            DocumentId = r.DocumentId,
            EntityId = r.Id,
            RegNumber = r.RegNumber,
            DocumentTitle = r.Title,
            DocumentType = DocumentType.Sz.ToString(),
            DocumentTypeTitle = DocumentTypeTitle(DocumentType.Sz),
            TaskType = "Регистрация",
            Stage = SzStage.Registration,
            IsOverdue = false,
            CreatedAt = r.RegisteredOn!.Value.ToDateTime(TimeOnly.MinValue),
            CompletedAt = registeredAt.TryGetValue(r.Id, out var at)
                ? at
                : r.RegisteredOn!.Value.ToDateTime(TimeOnly.MinValue),
        }));

        tasks = tasks.OrderByDescending(t => t.CompletedAt ?? t.CreatedAt).Take(limit).ToList();

        return new TaskInboxDto {Tasks = tasks, Total = tasks.Count};
    }

    public async Task<TaskStatsDto> StatsAsync(int userId)
    {
        var inbox = await GetAsync(userId);
        var tasks = inbox.Tasks;

        var now = DateTime.UtcNow;
        var endOfToday = now.Date.AddDays(1);
        var endOfWeek = now.Date.AddDays(7);

        // Группы собираем по ключу контура/типа; заголовок берём из первой задачи,
        // раз он у всех в группе одинаковый.
        List<TaskStatGroupDto> Group(Func<InboxTaskDto, string> key, Func<InboxTaskDto, string> title) =>
            tasks
                .GroupBy(key)
                .Select(g => new TaskStatGroupDto
                {
                    Key = g.Key,
                    Title = title(g.First()),
                    Count = g.Count(),
                    Overdue = g.Count(t => t.IsOverdue),
                })
                .OrderByDescending(g => g.Count)
                .ToList();

        return new TaskStatsDto
        {
            Total = tasks.Count,
            Overdue = tasks.Count(t => t.IsOverdue),
            Delegated = tasks.Count(t => t.OnBehalfOf is not null),
            DueToday = tasks.Count(t => !t.IsOverdue && t.DueAt is { } due && due < endOfToday),
            DueThisWeek = tasks.Count(t => !t.IsOverdue && t.DueAt is { } due && due >= endOfToday && due < endOfWeek),
            NoDue = tasks.Count(t => t.DueAt is null),
            ByContour = Group(t => t.DocumentType, t => t.DocumentTypeTitle),
            ByType = Group(t => t.TaskType, t => TaskTypeTitle(t.TaskType)),
        };
    }

    /// <summary>
    /// Есть ли право у пользователя — по его ролям в базе, а не по токену текущего
    /// запроса: реестр собирается и для дайджеста (DigestService), где запрос чужой.
    /// </summary>
    private async Task<bool> HasPermissionAsync(int userId, PermissionCode code)
    {
        var roleCodes = await _db.Users
            .Where(u => u.Id == userId)
            .SelectMany(u => u.Roles)
            .Select(r => r.PermissionCodes)
            .ToListAsync();

        return roleCodes.Any(codes => codes.Contains((int)code));
    }

    /// <summary>Записка, ждущая регистрации, — строка очереди делопроизводства.</summary>
    private sealed record RegistrationRow(int SzId, int DocumentId, string Title, DateTime SentAt);

    private static string DocumentTypeTitle(DocumentType type) => type switch
    {
        DocumentType.Sz => "Служебная записка",
        DocumentType.Vnd => "ВНД",
        DocumentType.Tid => "ТИД",
        DocumentType.Procurement => "Закупка",
        DocumentType.Contract => "Договор",
        _ => type.ToString(),
    };

    /// <summary>
    /// Название задачи маршрута с учётом этапа: движок всем участникам выдаёт задачу
    /// «Approval», и без этапа подписант видел у себя «Согласование» вместо подписи.
    /// </summary>
    private static string TaskTypeTitle(string type, StepKind? kind) => (type, kind) switch
    {
        ("Approval", StepKind.Signing) => "Подписание",
        ("Approval", StepKind.FinalControl) => "Финальный контроль",
        ("Approval", StepKind.Board) => "Рассмотрение на коллегиальном органе",
        _ => TaskTypeTitle(type),
    };

    private static string TaskTypeTitle(string type) => type switch
    {
        "Approval" => "Согласование",
        "RemarksResolution" => "Устранение замечаний",
        "AddresseeDecision" => "Решение по записке",
        "SzApprovalTracking" => "Моя СЗ на согласовании",
        "Assignment" => "Поручение",
        _ => type,
    };

    /// <summary>
    /// Строка задачи до сборки ответа. Общий тип нужен, чтобы задачи маршрута и
    /// задачи контура собирались в один список.
    /// </summary>
    private sealed class Row
    {
        public int Id { get; init; }
        public int? ParticipantId { get; init; }
        public int AssigneeUserId { get; init; }
        public required string TaskType { get; init; }
        public DateTime? DueAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public int? Order { get; init; }
        public StepKind? Kind { get; init; }
        public int DocumentId { get; init; }
        public string? RegNumber { get; init; }
        public required string Title { get; init; }
        public DocumentType DocType { get; init; }
        public string? DocStatus { get; init; }
        public int? DelegatedByUserId { get; init; }
        public DateTime? CompletedAt { get; init; }
    }
}
