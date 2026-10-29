using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Signing.Models;
using delosfera_server.Modules.Signing.Services;
using delosfera_server.Modules.Users.Services;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Workflow.Services;

/// <summary>
/// Реализация движка согласования (TID-01..14). Phase-1 core: последовательные и
/// параллельные этапы, резолюции, строгий режим замечаний, отклонение, вето,
/// финальный контроль ОМ, автоакцепт по нормативу.
/// </summary>
public class RouteEngine : IRouteEngine
{
    private const string Entity = "RouteInstance";

    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly IEnumerable<IRouteCompletionHandler> _completionHandlers;
    private readonly ISubstitutionService _substitutions;
    private readonly IWorkflowNotifier _notifier;
    private readonly ISignatureService _signatures;
    private readonly IRouteRoleResolver _roles;

    public RouteEngine(
        DelosferaDbContext db, IAuditService audit,
        IEnumerable<IRouteCompletionHandler> completionHandlers,
        ISubstitutionService substitutions,
        IWorkflowNotifier notifier,
        ISignatureService signatures,
        IRouteRoleResolver roles)
    {
        _roles = roles;
        _db = db;
        _audit = audit;
        _completionHandlers = completionHandlers;
        _substitutions = substitutions;
        _notifier = notifier;
        _signatures = signatures;
    }

    /// <summary>
    /// Что известно о документе для разрешения ролей: подразделение автора и
    /// подразделение, выбранное в самом документе.
    /// </summary>
    private async Task<RouteContext> BuildContextAsync(int documentId)
    {
        var authorUnit = await _db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => d.Author!.OrgUnitId)
            .FirstOrDefaultAsync();

        // Подразделение из карточки — на него ссылаются роли target-unit-head/curator.
        // Для кадровой СЗ (HrKindId задан) это ПОДРАЗДЕЛЕНИЕ СОТРУДНИКА (EmployeeUnitId):
        // его начальник — профильный согласующий, его куратор — курирующий зампред
        // (алгоритм кадровой СЗ). Для обычной записки — адресное подразделение,
        // для заявки на закупку — инициирующее.
        var sz = await _db.SzDocuments.AsNoTracking()
            .Where(s => s.DocumentId == documentId)
            .Select(s => new { s.HrKindId, s.CorrespondentUnitId, s.EmployeeUnitId, s.AuthorUnitId })
            .FirstOrDefaultAsync();

        int? targetUnit;
        if (sz is not null)
            targetUnit = sz.HrKindId is not null
                ? sz.EmployeeUnitId ?? sz.CorrespondentUnitId ?? sz.AuthorUnitId
                : sz.CorrespondentUnitId ?? sz.EmployeeUnitId ?? sz.AuthorUnitId;
        else
            targetUnit = await _db.ProcurementRequests.AsNoTracking()
                .Where(r => r.DocumentId == documentId)
                .Select(r => r.InitiatorUnitId)
                .FirstOrDefaultAsync();

        return new RouteContext(documentId, authorUnit, targetUnit ?? authorUnit);
    }

    private IQueryable<RouteInstance> InstanceQuery() =>
        _db.RouteInstances
            .Include(i => i.Steps.OrderBy(s => s.Order))
                .ThenInclude(s => s.Participants)
                    .ThenInclude(p => p.Resolution)
                        .ThenInclude(r => r!.Remarks);

    public async Task<RouteInstance> InstantiateFromTemplateAsync(
        int documentId, int templateId, IReadOnlySet<string>? satisfiedConditions = null)
    {
        var tpl = await _db.RouteTemplates
            .Include(t => t.Steps.OrderBy(s => s.Order)).ThenInclude(s => s.Participants)
            .FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new KeyNotFoundException($"Шаблон маршрута id={templateId} не найден");

        // Условные этапы: без условия — всегда, с условием — только если контур передал
        // это условие как выполненное (например household-goods, board-authority).
        var steps = tpl.Steps
            .Where(s => s.Condition is null
                        || (satisfiedConditions is not null && satisfiedConditions.Contains(s.Condition)))
            .OrderBy(s => s.Order)
            .ToList();

        // Роли шаблона превращаются в людей здесь, на запуске маршрута. Раньше
        // роль копировалась в участника как есть и человеком не становилась
        // никогда: задача не создавалась, участник числился активным, и маршрут
        // вставал навсегда — молча, потому что формально всё было в порядке.
        var context = await BuildContextAsync(documentId);
        var resolved = new Dictionary<int, int?>();
        var unresolved = new List<string>();

        foreach (var participant in steps.SelectMany(s => s.Participants))
        {
            if (participant.UserId is not null) continue;

            // Участник задан ролью или подразделением. Подразделение значит «пусть
            // согласует руководитель этого отдела» — иначе этап без конкретного
            // человека так же тихо повис бы, как повисала неразрешённая роль.
            var roleRef = !string.IsNullOrWhiteSpace(participant.RoleRef)
                ? participant.RoleRef
                : participant.UnitId is { } unitId
                    ? $"{RouteRoles.UnitHeadPrefix}{unitId}"
                    : null;

            if (roleRef is null) continue;

            var userId = await _roles.ResolveAsync(roleRef, context);
            resolved[participant.Id] = userId;

            if (userId is null) unresolved.Add(_roles.Explain(roleRef));
        }

        if (unresolved.Count > 0)
            throw new InvalidOperationException(
                "Не удалось определить согласующих: " + string.Join("; ", unresolved.Distinct()) +
                ". Заполните справочник оргструктуры или состав органа");

        // Автор не согласует сам себя: если роль этапа разрешилась в автора записки
        // (например «руководитель подразделения автора», когда директор филиала сам
        // создал кадровую СЗ), этап пропускаем — по алгоритму он уходит сразу дальше.
        var authorId = await _db.Documents.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => (int?) d.AuthorId)
            .FirstOrDefaultAsync();

        // Автор попал в этап ТОЛЬКО через роль (не назван поимённо) и это он сам.
        // Поимённо названного автора не трогаем — его указали намеренно.
        bool IsAuthorByRole(RouteTemplateParticipant tp) =>
            tp.UserId is null && authorId is not null
            && resolved.TryGetValue(tp.Id, out var byRole) && byRole == authorId.Value;

        var stepModels = new List<RouteStep>();
        var order = 1;
        foreach (var ts in steps)
        {
            // Из согласующих этапа убираем автора, пришедшего ролью (сам себе визу не ставит).
            var keep = ts.Participants.Where(tp => !IsAuthorByRole(tp)).ToList();

            // Этап согласования, где после этого не осталось ни одного согласующего,
            // пропускаем целиком. Этапы подписания/иные с явными участниками не режем.
            if (keep.Count == 0 && ts.Kind == StepKind.Approval)
                continue;

            stepModels.Add(new RouteStep
            {
                Order = order++,
                Mode = ts.Mode,
                Kind = ts.Kind,
                IsFinalMethodology = ts.IsFinalMethodology,
                TimeNormHours = ts.TimeNormHours,
                // Кворум имеет смысл только на развилке — на последовательном этапе его не переносим.
                Quorum = ts.Mode == StepMode.Parallel ? ts.Quorum : null,
                RequiredSignatureLevel = ts.RequiredSignatureLevel,
                Participants = (keep.Count > 0 ? keep : ts.Participants).Select(tp => new RouteParticipant
                {
                    UserId = tp.UserId ?? (resolved.TryGetValue(tp.Id, out var byRole) ? byRole : null),
                    UnitId = tp.UnitId,
                    RoleRef = tp.RoleRef,
                    Required = tp.Required,
                    State = ParticipantState.Pending
                }).ToList()
            });
        }

        if (stepModels.Count == 0)
            throw new InvalidOperationException(
                "После исключения автора в маршруте не осталось согласующих — назначьте другого согласующего");

        var instance = new RouteInstance
        {
            DocumentId = documentId,
            TemplateId = templateId,
            Status = RouteInstanceStatus.Draft,
            Steps = stepModels
        };

        _db.RouteInstances.Add(instance);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Entity, instance.Id, "Instantiated", null, new { documentId, templateId });
        return instance;
    }

    /// <summary>
    /// Дописать этап подписания в конец маршрута, собранного по шаблону. Шаблон
    /// задаёт согласование, а подписант приходит из карточки документа — если в
    /// шаблоне подписание уже предусмотрено, второй раз его не добавляем.
    /// </summary>
    public async Task AppendSigningStepAsync(int routeInstanceId, int signerUserId)
    {
        var instance = await InstanceQuery().FirstOrDefaultAsync(i => i.Id == routeInstanceId)
                       ?? throw new KeyNotFoundException($"Маршрут {routeInstanceId} не найден");

        if (instance.Steps.Any(s => s.Kind == StepKind.Signing)) return;

        _db.RouteSteps.Add(new RouteStep
        {
            RouteInstanceId = instance.Id,
            Order = instance.Steps.Count == 0 ? 1 : instance.Steps.Max(s => s.Order) + 1,
            Mode = StepMode.Sequential,
            Kind = StepKind.Signing,
            Participants = [new RouteParticipant {UserId = signerUserId, Required = true, State = ParticipantState.Pending}],
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync(Entity, instance.Id, "SigningStepAppended", null, new {signerUserId});
    }

    /// <summary>
    /// Маршрут из одного подписанта.
    ///
    /// Нужен там, где подписание отделено от согласования: в служебных записках
    /// сначала согласуют, потом регистрируют, и только потом подписывают. Общий
    /// метод для согласующих здесь не годится — он отказывается строить маршрут
    /// без единого согласующего, и правильно делает: маршрут согласования без
    /// согласующих бессмыслен. Здесь согласование уже прошло.
    /// </summary>
    public async Task<RouteInstance> InstantiateForSignerAsync(
        int documentId, int signerUserId, int? timeNormHours = null)
    {
        var instance = new RouteInstance
        {
            DocumentId = documentId,
            Status = RouteInstanceStatus.Draft,
            Steps =
            [
                new RouteStep
                {
                    Order = 1,
                    Mode = StepMode.Sequential,
                    Kind = StepKind.Signing,
                    TimeNormHours = timeNormHours,
                    Participants =
                    [
                        new RouteParticipant
                        {
                            UserId = signerUserId,
                            Required = true,
                            State = ParticipantState.Pending,
                        },
                    ],
                },
            ],
        };

        _db.RouteInstances.Add(instance);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(Entity, instance.Id, "InstantiatedForSigner", null,
            new {documentId, signer = signerUserId});

        return instance;
    }

    public async Task<RouteInstance> InstantiateForApproversAsync(
        int documentId, IReadOnlyList<int> approverUserIds, bool parallel,
        int? timeNormHours = null, int? signerUserId = null)
    {
        if (approverUserIds.Count == 0)
            throw new InvalidOperationException("Не выбран ни один согласующий");

        // Параллельное согласование — один этап со всеми участниками; последовательное —
        // по этапу на каждого: движок переходит к следующему только после решения предыдущего.
        var steps = parallel
            ? new List<RouteStep>
            {
                new()
                {
                    Order = 1,
                    Mode = StepMode.Parallel,
                    Kind = StepKind.Approval,
                    TimeNormHours = timeNormHours,
                    Participants = approverUserIds
                        .Select(id => new RouteParticipant {UserId = id, Required = true, State = ParticipantState.Pending})
                        .ToList(),
                },
            }
            : approverUserIds.Select((id, index) => new RouteStep
            {
                Order = index + 1,
                Mode = StepMode.Sequential,
                Kind = StepKind.Approval,
                TimeNormHours = timeNormHours,
                Participants = [new RouteParticipant {UserId = id, Required = true, State = ParticipantState.Pending}],
            }).ToList();

        // Подписант ставит подпись последним: сначала документ согласуют по существу,
        // и только потом руководитель подписывает то, с чем все согласились. Обратный
        // порядок означал бы подпись под текстом, который ещё могут изменить.
        if (signerUserId is { } signer)
        {
            steps.Add(new RouteStep
            {
                Order = steps.Count + 1,
                Mode = StepMode.Sequential,
                Kind = StepKind.Signing,
                TimeNormHours = timeNormHours,
                Participants = [new RouteParticipant {UserId = signer, Required = true, State = ParticipantState.Pending}],
            });
        }

        var instance = new RouteInstance
        {
            DocumentId = documentId,
            Status = RouteInstanceStatus.Draft,
            Steps = steps,
        };

        _db.RouteInstances.Add(instance);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(Entity, instance.Id, "InstantiatedForApprovers", null,
            new {documentId, approvers = approverUserIds.Count, parallel, signer = signerUserId});

        return instance;
    }

    public async Task StartAsync(int routeInstanceId, int actorUserId)
    {
        var inst = await InstanceQuery().FirstOrDefaultAsync(i => i.Id == routeInstanceId)
            ?? throw new KeyNotFoundException($"Маршрут id={routeInstanceId} не найден");

        if (inst.Status != RouteInstanceStatus.Draft)
            throw new InvalidOperationException("Маршрут уже запущен или завершён");

        var steps = inst.Steps.OrderBy(s => s.Order).ToList();
        if (steps.Count == 0)
            throw new InvalidOperationException("В маршруте нет этапов");

        // TID-04/05: финальный контроль ОМ, если задан, должен быть последним этапом.
        var finalIdx = steps.FindIndex(s => s.IsFinalMethodology);
        if (finalIdx >= 0 && finalIdx != steps.Count - 1)
            throw new InvalidOperationException("Этап финального контроля ОМ должен быть последним");

        // TID-05: обязательные согласующие должны присутствовать.
        if (steps.SelectMany(s => s.Participants).All(p => !p.Required) &&
            steps.Any(s => s.Participants.Count == 0))
            throw new InvalidOperationException("В маршруте есть пустые этапы");

        inst.Status = RouteInstanceStatus.Running;
        inst.StartedAt = DateTime.UtcNow;
        inst.CurrentStepOrder = steps[0].Order;

        var activated = ActivateStep(steps[0]);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Entity, inst.Id, "RouteStarted", actorUserId);
        await _notifier.TaskAssignedAsync(activated);
    }

    /// <summary>
    /// Активирует этап и возвращает участников, которым появилась задача — их
    /// оповещают после сохранения, когда задачи уже есть в базе.
    /// </summary>
    private List<int> ActivateStep(RouteStep step)
    {
        step.ActivatedAt = DateTime.UtcNow;
        // Срок этапа — в рабочих часах по календарю банка (COND-1), не астрономических.
        var due = step.TimeNormHours.HasValue
            ? WorkingCalendar.AddWorkingHours(DateTime.UtcNow, step.TimeNormHours.Value)
            : (DateTime?)null;

        var activated = new List<int>();
        var participants = step.Participants.OrderBy(p => p.Id).ToList();

        if (step.Mode == StepMode.Parallel)
        {
            foreach (var p in participants) activated.AddRange(ActivateParticipant(p, due));
        }
        else
        {
            var first = participants.FirstOrDefault(p => p.State == ParticipantState.Pending);
            if (first != null) activated.AddRange(ActivateParticipant(first, due));
        }

        return activated;
    }

    private List<int> ActivateParticipant(RouteParticipant p, DateTime? due)
    {
        p.State = ParticipantState.Active;
        p.ActivatedAt = DateTime.UtcNow;
        p.DueAt = due;
        if (p.UserId is int uid)
        {
            _db.WorkflowTasks.Add(new WorkflowTask
            {
                RouteParticipantId = p.Id,
                AssigneeUserId = uid,
                Type = "Approval",
                DueAt = due,
                State = WorkflowTaskState.Open,
                CreatedAt = DateTime.UtcNow
            });

            return [p.Id];
        }

        // Этап без конкретного исполнителя (роль/подразделение) оповещать некому.
        return [];
    }

    public async Task ResolveAsync(int participantId, ResolutionType type, string? comment, int actorUserId, int? signatureId = null)
    {
        var p = await _db.RouteParticipants
            .Include(x => x.RouteStep)!.ThenInclude(s => s!.Participants).ThenInclude(x => x.Resolution)
            .Include(x => x.Resolution)
            .FirstOrDefaultAsync(x => x.Id == participantId)
            ?? throw new KeyNotFoundException($"Участник id={participantId} не найден");

        if (p.State != ParticipantState.Active)
            throw new InvalidOperationException("Участник не находится в состоянии ожидания решения");

        // Резолюцию выносит сам согласующий либо тот, кто его сейчас замещает (GEN-14).
        // Без этой проверки решение мог принять любой аутентифицированный пользователь,
        // а подпись под резолюцией теряет смысл. Системные вызовы (автоакцепт) идут с id 0.
        if (actorUserId != 0 && p.UserId is { } assignee && assignee != actorUserId)
        {
            var actingFor = await _substitutions.GetActingForUserIdsAsync(actorUserId);
            if (!actingFor.Contains(assignee))
                throw new InvalidOperationException(
                    "Решение по этому этапу выносит назначенный согласующий или его замещающий");
        }

        if ((type is ResolutionType.ApprovedWithRemarks or ResolutionType.Rejected) && string.IsNullOrWhiteSpace(comment))
            throw new InvalidOperationException("Комментарий обязателен для замечаний и отклонения");

        var step = p.RouteStep!;

        await RequireSignatureAsync(step, type, actorUserId, signatureId);
        var inst = await InstanceQuery().FirstAsync(i => i.Id == step.RouteInstanceId);

        // Нажатие кнопки — это простая электронная подпись, и она должна оставлять
        // след: кто, когда и под чем расписался. Без записи подписи виза сводится к
        // смене статуса, а доказать авторство решения потом нечем. Личность
        // подтверждена входом, момент и отпечаток карточки фиксируются здесь.
        //
        // Автоакцепт по нормативу идёт от системы и подписи не имеет по определению.
        signatureId ??= actorUserId == 0
            ? null
            : (await _signatures.SignDocumentAsync(inst.DocumentId, SignatureLevel.Simple, actorUserId)).Id;

        var resolution = new Resolution
        {
            RouteParticipantId = p.Id,
            Type = type,
            Comment = comment,
            SignatureId = signatureId,
            At = DateTime.UtcNow
        };
        _db.Resolutions.Add(resolution);
        p.State = ParticipantState.Done;
        CloseTask(p.Id);

        await _audit.LogAsync("Resolution", p.Id, type.ToString(), actorUserId, new { comment });

        switch (type)
        {
            case ResolutionType.Rejected:
                // TID-11: прерывание. В параллельном этапе — аннулировать остальных.
                foreach (var other in step.Participants.Where(x => x.Id != p.Id && x.State is ParticipantState.Active or ParticipantState.Pending))
                {
                    other.State = ParticipantState.Cancelled;
                    CloseTask(other.Id, WorkflowTaskState.Cancelled);
                }
                inst.Status = RouteInstanceStatus.Rejected;
                inst.FinishedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await _audit.LogAsync(Entity, inst.Id, "Rejected", actorUserId);
                await NotifyStatusAsync(inst, actorUserId);
                await _notifier.RouteFinishedAsync(inst.Id, RouteInstanceStatus.Rejected, comment);
                return;

            case ResolutionType.Veto:
                inst.Status = RouteInstanceStatus.Arbitration;
                await _db.SaveChangesAsync();
                await _audit.LogAsync(Entity, inst.Id, "Arbitration", actorUserId);
                return;

            case ResolutionType.ApprovedWithRemarks:
                // Строгий режим: одно сплошное замечание (ВНД-уточнение #8).
                _db.Remarks.Add(new Remark { Resolution = resolution, Text = comment!, State = RemarkState.Open });
                break;
        }

        await _db.SaveChangesAsync();
        await AdvanceIfStepComplete(inst.Id, step.Id, actorUserId);
    }

    private async Task AdvanceIfStepComplete(int instanceId, int stepId, int actorUserId)
    {
        var inst = await InstanceQuery().FirstAsync(i => i.Id == instanceId);
        var step = inst.Steps.First(s => s.Id == stepId);

        // Sequential: активировать следующего участника, если ещё есть.
        if (step.Mode == StepMode.Sequential)
        {
            var next = step.Participants.OrderBy(x => x.Id).FirstOrDefault(x => x.State == ParticipantState.Pending);
            if (next != null)
            {
                var due = step.TimeNormHours.HasValue
                    ? WorkingCalendar.AddWorkingHours(DateTime.UtcNow, step.TimeNormHours.Value)
                    : (DateTime?)null;
                var activatedNext = ActivateParticipant(next, due);
                await _db.SaveChangesAsync();
                await _notifier.TaskAssignedAsync(activatedNext);
                return;
            }
        }

        // Кворум развилки: как только набралось нужное число чистых одобрений, этап
        // проходит, не дожидаясь остальных — оставшихся аннулируем. Считаем только
        // «Согласовано»: «с замечаниями» уводит маршрут на доработку и в кворум не идёт.
        if (step.Mode == StepMode.Parallel && step.Quorum is int quorum && quorum > 0)
        {
            var approved = step.Participants.Count(
                x => x.State == ParticipantState.Done && x.Resolution?.Type == ResolutionType.Approved);
            if (approved >= quorum)
            {
                foreach (var other in step.Participants
                             .Where(x => x.State is ParticipantState.Active or ParticipantState.Pending))
                {
                    other.State = ParticipantState.Cancelled;
                    CloseTask(other.Id, WorkflowTaskState.Cancelled);
                }
                await _db.SaveChangesAsync();
                await _audit.LogAsync(Entity, inst.Id, "QuorumReached", actorUserId,
                    new { step = step.Order, quorum, approved });
            }
        }

        var allDone = step.Participants.All(x => x.State is ParticipantState.Done or ParticipantState.Cancelled);
        if (!allDone) return; // параллельный этап ещё ждёт других

        // Есть ли открытые замечания на этом этапе → строгий режим (доработка инициатора).
        var openRemarks = step.Participants
            .Select(x => x.Resolution)
            .Where(r => r != null)
            .SelectMany(r => r!.Remarks)
            .Any(rm => rm.State == RemarkState.Open);

        if (openRemarks)
        {
            inst.Status = RouteInstanceStatus.OnRevision;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(Entity, inst.Id, "OnRevision", actorUserId);
            await NotifyStatusAsync(inst, actorUserId);
            await _notifier.RouteFinishedAsync(inst.Id, RouteInstanceStatus.OnRevision, null);
            return;
        }

        await AdvanceToNextStep(inst, step, actorUserId);
    }

    private async Task AdvanceToNextStep(RouteInstance inst, RouteStep current, int actorUserId)
    {
        var next = inst.Steps.Where(s => s.Order > current.Order).OrderBy(s => s.Order).FirstOrDefault();
        if (next == null)
        {
            inst.Status = RouteInstanceStatus.Approved;
            inst.FinishedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.LogAsync(Entity, inst.Id, "Approved", actorUserId);

            // Движок не знает про контуры: сообщаем о завершении, дальше реагирует
            // обработчик своего контура (для ВНД — перевод в консолидацию, TID-14/VND-05).
            foreach (var handler in _completionHandlers)
                await handler.OnRouteApprovedAsync(inst.Id, inst.DocumentId, actorUserId);

            await _notifier.RouteFinishedAsync(inst.Id, RouteInstanceStatus.Approved, null);
            return;
        }

        inst.CurrentStepOrder = next.Order;
        var activatedStep = ActivateStep(next);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Entity, inst.Id, "StepAdvanced", actorUserId, new { toStep = next.Order });
        await _notifier.TaskAssignedAsync(activatedStep);
    }

    public async Task ConfirmRemarkResolvedAsync(int remarkId, int actorUserId)
    {
        var remark = await _db.Remarks
            .Include(r => r.Resolution)!.ThenInclude(res => res!.RouteParticipant)!.ThenInclude(p => p!.RouteStep)
            .FirstOrDefaultAsync(r => r.Id == remarkId)
            ?? throw new KeyNotFoundException($"Замечание id={remarkId} не найдено");

        remark.State = RemarkState.Resolved;
        remark.ResolvedConfirmedById = actorUserId;
        remark.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Remark", remark.Id, "Resolved", actorUserId);

        var step = remark.Resolution!.RouteParticipant!.RouteStep!;
        var inst = await InstanceQuery().FirstAsync(i => i.Id == step.RouteInstanceId);

        var stillOpen = inst.Steps.First(s => s.Id == step.Id).Participants
            .Select(x => x.Resolution).Where(r => r != null)
            .SelectMany(r => r!.Remarks).Any(rm => rm.State == RemarkState.Open);

        if (!stillOpen && inst.Status == RouteInstanceStatus.OnRevision)
        {
            inst.Status = RouteInstanceStatus.Running;
            await _db.SaveChangesAsync();
            // Замечания устранены — контур должен вернуть документ из «На доработке».
            await NotifyStatusAsync(inst, actorUserId);
            await AdvanceToNextStep(inst, inst.Steps.First(s => s.Id == step.Id), actorUserId);
        }
    }

    /// <summary>Сообщить контуру документа о смене статуса маршрута.</summary>
    private async Task NotifyStatusAsync(RouteInstance inst, int actorUserId)
    {
        foreach (var handler in _completionHandlers)
            await handler.OnRouteStatusChangedAsync(inst.Id, inst.DocumentId, inst.Status, actorUserId);
    }

    public async Task ApplyOverdueAsync(DateTime now)
    {
        // TID-08: просроченные активные участники → автоакцепт; на финальном этапе ОМ — эскалация.
        var overdue = await _db.RouteParticipants
            .Include(p => p.RouteStep!).ThenInclude(s => s.RouteInstance)
            .Include(p => p.RouteStep!).ThenInclude(s => s.Participants)
                .ThenInclude(x => x.Resolution).ThenInclude(r => r!.Remarks)
            .Where(p => p.State == ParticipantState.Active && p.DueAt != null && p.DueAt < now)
            .ToListAsync();

        foreach (var p in overdue)
        {
            // TID-10: при противоречии автоакцепта и строгого режима приоритет — блокировке.
            // Документ, возвращённый на доработку или имеющий открытые замечания, не может
            // «досогласоваться сам по себе»: молчание согласующего здесь не согласие, а
            // ожидание правок от инициатора.
            if (IsBlockedByRemarks(p))
            {
                await _audit.LogAsync("RouteParticipant", p.Id, "AutoAcceptSuppressedByRemarks", null);
                continue;
            }

            if (p.RouteStep!.IsFinalMethodology)
            {
                // Эскалация вместо автоакцепта.
                var task = await _db.WorkflowTasks.FirstOrDefaultAsync(t => t.RouteParticipantId == p.Id && t.State == WorkflowTaskState.Open);
                if (task != null) { task.State = WorkflowTaskState.Escalated; await _db.SaveChangesAsync(); }
                await _audit.LogAsync("RouteParticipant", p.Id, "EscalatedFinalControl", null);
                await _notifier.OverdueAsync(p.Id, escalated: true);
                continue;
            }

            // Оповещаем до автоакцепта: после него участник уже не активен, и письмо
            // «срок истёк» строилось бы по закрытому этапу.
            await _notifier.OverdueAsync(p.Id, escalated: false);
            await ResolveAsync(p.Id, ResolutionType.AutoAccept, "Автоакцепт по нормативу времени", actorUserId: 0);
        }
    }

    public async Task InterruptAsync(int routeInstanceId, int actorUserId)
    {
        var inst = await InstanceQuery().FirstOrDefaultAsync(i => i.Id == routeInstanceId);
        if (inst is null || inst.Status is RouteInstanceStatus.Approved or RouteInstanceStatus.Rejected
            or RouteInstanceStatus.Interrupted) return;

        // Без аннулирования задачи согласующих остались бы активными и документ
        // продолжал бы висеть у них в «согласую я» после отзыва.
        foreach (var p in inst.Steps.SelectMany(s => s.Participants)
                     .Where(x => x.State is ParticipantState.Active or ParticipantState.Pending))
        {
            p.State = ParticipantState.Cancelled;
            CloseTask(p.Id, WorkflowTaskState.Cancelled);
        }

        inst.Status = RouteInstanceStatus.Interrupted;
        inst.FinishedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Entity, inst.Id, "Interrupted", actorUserId);
        await _notifier.RouteFinishedAsync(inst.Id, RouteInstanceStatus.Interrupted, null);
    }

    public async Task<RouteInstance> RestartAsync(int routeInstanceId, int actorUserId)
    {
        var old = await InstanceQuery().FirstOrDefaultAsync(i => i.Id == routeInstanceId)
            ?? throw new KeyNotFoundException($"Маршрут id={routeInstanceId} не найден");

        if (old.Status is not (RouteInstanceStatus.Running or RouteInstanceStatus.OnRevision))
            throw new InvalidOperationException("Перезапустить можно только идущий маршрут");

        // Копируем этапы и действующих участников. Снятые с маршрута (Cancelled) в новый круг
        // не переходят; исключение — кворумные этапы, где Cancelled означает «не понадобился».
        var steps = new List<RouteStep>();
        var order = 0;
        foreach (var s in old.Steps.OrderBy(s => s.Order))
        {
            var participants = s.Participants
                .Where(p => p.State != ParticipantState.Cancelled || s.Quorum is > 0)
                .OrderBy(p => p.Id)
                .Select(p => new RouteParticipant
                {
                    UserId = p.UserId, UnitId = p.UnitId, RoleRef = p.RoleRef,
                    Required = p.Required, State = ParticipantState.Pending,
                })
                .ToList();
            if (participants.Count == 0) continue;

            steps.Add(new RouteStep
            {
                Order = ++order,
                Mode = s.Mode,
                Kind = s.Kind,
                IsFinalMethodology = s.IsFinalMethodology,
                TimeNormHours = s.TimeNormHours,
                Quorum = s.Quorum,
                RequiredSignatureLevel = s.RequiredSignatureLevel,
                Participants = participants,
            });
        }

        if (steps.Count == 0)
            throw new InvalidOperationException("В маршруте не осталось участников — перезапускать нечего");

        await InterruptAsync(old.Id, actorUserId);

        var fresh = new RouteInstance
        {
            DocumentId = old.DocumentId,
            TemplateId = old.TemplateId,
            Status = RouteInstanceStatus.Draft,
            Steps = steps,
        };
        _db.RouteInstances.Add(fresh);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(Entity, fresh.Id, "Restarted", actorUserId, new { from = old.Id });

        await StartAsync(fresh.Id, actorUserId);
        return fresh;
    }

    public async Task<RouteParticipant> AddApproverStepAsync(
        int routeInstanceId, int userId, int? beforeStepOrder, int actorUserId)
    {
        var inst = await InstanceQuery().FirstOrDefaultAsync(i => i.Id == routeInstanceId)
            ?? throw new KeyNotFoundException($"Маршрут id={routeInstanceId} не найден");

        if (inst.Status != RouteInstanceStatus.Running)
            throw new InvalidOperationException("Добавить участника можно только в идущий маршрут");

        if (inst.Steps.SelectMany(s => s.Participants)
            .Any(p => p.UserId == userId && p.State is ParticipantState.Pending or ParticipantState.Active))
            throw new InvalidOperationException("Этот сотрудник уже есть в маршруте и ещё не вынес решения");

        var current = inst.CurrentStepOrder;
        var maxOrder = inst.Steps.Count == 0 ? 0 : inst.Steps.Max(s => s.Order);
        int position;
        if (beforeStepOrder is { } before)
        {
            if (before <= current)
                throw new InvalidOperationException("Нового участника можно поставить только после текущего этапа");
            position = Math.Min(before, maxOrder + 1);
        }
        else
        {
            // Подписание и финальный контроль — в конце маршрута: новый согласующий встаёт перед ними.
            var tail = inst.Steps
                .Where(s => s.Order > current && (s.Kind == StepKind.Signing || s.IsFinalMethodology))
                .OrderBy(s => s.Order)
                .FirstOrDefault();
            position = tail?.Order ?? maxOrder + 1;
        }

        foreach (var s in inst.Steps.Where(s => s.Order >= position))
            s.Order++;

        var currentStep = inst.Steps.FirstOrDefault(s => s.Order == current);
        var participant = new RouteParticipant { UserId = userId, Required = true, State = ParticipantState.Pending };
        _db.RouteSteps.Add(new RouteStep
        {
            RouteInstanceId = inst.Id,
            Order = position,
            Mode = StepMode.Sequential,
            Kind = StepKind.Approval,
            TimeNormHours = currentStep?.TimeNormHours,
            Participants = [participant],
        });

        await _db.SaveChangesAsync();
        await _audit.LogAsync(Entity, inst.Id, "ParticipantAdded", actorUserId,
            new { userId, step = position, participantId = participant.Id });
        return participant;
    }

    public async Task RemoveParticipantAsync(int participantId, int actorUserId)
    {
        var instanceId = await _db.RouteSteps
            .Where(s => s.Participants.Any(p => p.Id == participantId))
            .Select(s => (int?)s.RouteInstanceId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Участник маршрута не найден");

        var inst = await InstanceQuery().FirstAsync(i => i.Id == instanceId);
        if (inst.Status != RouteInstanceStatus.Running)
            throw new InvalidOperationException("Убрать участника можно только из идущего маршрута");

        var step = inst.Steps.First(s => s.Participants.Any(p => p.Id == participantId));
        var participant = step.Participants.First(p => p.Id == participantId);

        if (participant.Resolution != null || participant.State == ParticipantState.Done)
            throw new InvalidOperationException("Участник уже вынес решение — убрать его из маршрута нельзя");
        if (participant.State == ParticipantState.Cancelled)
            throw new InvalidOperationException("Участник уже снят с маршрута");

        var wasActive = participant.State == ParticipantState.Active;
        if (wasActive)
        {
            // Задача уже выдана — снимаем её; запись участника остаётся в истории как снятая.
            participant.State = ParticipantState.Cancelled;
            CloseTask(participant.Id, WorkflowTaskState.Cancelled);
        }
        else
        {
            // До участника очередь не дошла: задачи нет, следов не остаётся.
            step.Participants.Remove(participant);
            _db.Remove(participant);
            if (step.Participants.Count == 0 && step.Order > inst.CurrentStepOrder)
                _db.RouteSteps.Remove(step);
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync(Entity, inst.Id, "ParticipantRemoved", actorUserId,
            new { participantId, participant.UserId, step = step.Order, wasActive });

        // Снятый активный участник — как вынесенное решение: следующий по очереди или следующий этап.
        if (wasActive)
            await AdvanceIfStepComplete(inst.Id, step.Id, actorUserId);
    }

    public async Task<RouteParticipant> ReplaceParticipantAsync(int participantId, int newUserId, int actorUserId)
    {
        var instanceId = await _db.RouteSteps
            .Where(s => s.Participants.Any(p => p.Id == participantId))
            .Select(s => (int?)s.RouteInstanceId)
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Участник маршрута не найден");

        var inst = await InstanceQuery().FirstAsync(i => i.Id == instanceId);
        if (inst.Status != RouteInstanceStatus.Running)
            throw new InvalidOperationException("Заменить участника можно только в идущем маршруте");

        var step = inst.Steps.First(s => s.Participants.Any(p => p.Id == participantId));
        var participant = step.Participants.First(p => p.Id == participantId);

        if (participant.Resolution != null || participant.State == ParticipantState.Done)
            throw new InvalidOperationException("Участник уже вынес решение — заменить его нельзя");
        if (participant.State == ParticipantState.Cancelled)
            throw new InvalidOperationException("Участник уже снят с маршрута");
        if (participant.UserId == newUserId)
            return participant;

        if (inst.Steps.SelectMany(s => s.Participants)
            .Any(p => p.Id != participantId && p.UserId == newUserId
                      && p.State is ParticipantState.Pending or ParticipantState.Active))
            throw new InvalidOperationException("Этот сотрудник уже есть в маршруте и ещё не вынес решения");

        var previousUserId = participant.UserId;
        var activated = new List<int>();
        RouteParticipant result;

        if (participant.State == ParticipantState.Active)
        {
            // Задача уже у прежнего: снимаем её, а прежнего оставляем в истории снятым. Новый
            // участник встаёт на тот же этап и сразу получает задачу с тем же сроком —
            // маршрут не продвигается и не начинается заново.
            participant.State = ParticipantState.Cancelled;
            CloseTask(participant.Id, WorkflowTaskState.Cancelled);

            result = new RouteParticipant
            {
                UserId = newUserId,
                Required = participant.Required,
                State = ParticipantState.Pending,
            };
            step.Participants.Add(result);
            await _db.SaveChangesAsync();

            activated.AddRange(ActivateParticipant(result, participant.DueAt));
        }
        else
        {
            // До участника очередь не дошла — задачи нет, просто меняем исполнителя этапа.
            participant.UserId = newUserId;
            participant.UnitId = null;
            participant.RoleRef = null;
            result = participant;
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync(Entity, inst.Id, "ParticipantReplaced", actorUserId,
            new { participantId, fromUserId = previousUserId, toUserId = newUserId, newParticipantId = result.Id, step = step.Order });

        if (activated.Count > 0)
            await _notifier.TaskAssignedAsync(activated);
        return result;
    }

    /// <summary>
    /// Заблокирован ли процесс строгим режимом: маршрут на доработке либо на этапе
    /// есть неснятые замечания.
    /// </summary>
    private static bool IsBlockedByRemarks(RouteParticipant participant)
    {
        var step = participant.RouteStep;
        if (step is null) return false;

        if (step.RouteInstance?.Status == RouteInstanceStatus.OnRevision) return true;

        return step.Participants
            .Select(x => x.Resolution)
            .Where(r => r is not null)
            .SelectMany(r => r!.Remarks)
            .Any(rm => rm.State == RemarkState.Open);
    }

    /// <summary>
    /// Проверяет подпись, которой закрывается этап (SIG-04).
    ///
    /// Подпись обязательна только для согласующих резолюций: отклонение и возврат на
    /// доработку ничего не удостоверяют, и требовать под ними ЭЦП значит мешать
    /// остановить процесс. Автоакцепт идёт от системы и подписи не имеет по определению.
    /// </summary>
    private async Task RequireSignatureAsync(
        RouteStep step, ResolutionType type, int actorUserId, int? signatureId)
    {
        if (step.RequiredSignatureLevel is not { } required) return;
        if (type is ResolutionType.Rejected or ResolutionType.AutoAccept or ResolutionType.Veto) return;

        var levelTitle = required == Signing.Models.SignatureLevel.Qualified
            ? "квалифицированной электронной подписью"
            : "электронной подписью";

        if (signatureId is not { } id)
            throw new InvalidOperationException($"Этап закрывается {levelTitle} — подпись не приложена");

        var signature = await _db.Signatures.FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException("Приложенная подпись не найдена");

        if (signature.Revoked)
            throw new InvalidOperationException(
                "Подпись аннулирована — файл изменился после подписания, требуется подписать заново");

        // Подписывает тот, кто выносит резолюцию: чужая подпись под своей визой
        // превращает маршрут в формальность.
        if (signature.UserId != actorUserId)
            throw new InvalidOperationException("Подпись принадлежит другому пользователю");

        // Квалифицированная подпись сильнее простой и закрывает этап, требующий простую;
        // обратное неверно.
        if (required == Signing.Models.SignatureLevel.Qualified &&
            signature.Level != Signing.Models.SignatureLevel.Qualified)
            throw new InvalidOperationException($"Этап закрывается {levelTitle}, приложена простая подпись");
    }

    private void CloseTask(int participantId, WorkflowTaskState state = WorkflowTaskState.Done)
    {
        var task = _db.WorkflowTasks.Local.FirstOrDefault(t => t.RouteParticipantId == participantId && t.State == WorkflowTaskState.Open)
                   ?? _db.WorkflowTasks.FirstOrDefault(t => t.RouteParticipantId == participantId && t.State == WorkflowTaskState.Open);
        if (task != null)
        {
            task.State = state;
            task.CompletedAt = DateTime.UtcNow;
        }
    }
}
