using Microsoft.EntityFrameworkCore;
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

    public RouteEngine(
        DelosferaDbContext db, IAuditService audit,
        IEnumerable<IRouteCompletionHandler> completionHandlers,
        ISubstitutionService substitutions,
        IWorkflowNotifier notifier,
        ISignatureService signatures)
    {
        _db = db;
        _audit = audit;
        _completionHandlers = completionHandlers;
        _substitutions = substitutions;
        _notifier = notifier;
        _signatures = signatures;
    }

    private IQueryable<RouteInstance> InstanceQuery() =>
        _db.RouteInstances
            .Include(i => i.Steps.OrderBy(s => s.Order))
                .ThenInclude(s => s.Participants)
                    .ThenInclude(p => p.Resolution)
                        .ThenInclude(r => r!.Remarks);

    public async Task<RouteInstance> InstantiateFromTemplateAsync(int documentId, int templateId)
    {
        var tpl = await _db.RouteTemplates
            .Include(t => t.Steps.OrderBy(s => s.Order)).ThenInclude(s => s.Participants)
            .FirstOrDefaultAsync(t => t.Id == templateId)
            ?? throw new KeyNotFoundException($"Шаблон маршрута id={templateId} не найден");

        var instance = new RouteInstance
        {
            DocumentId = documentId,
            TemplateId = templateId,
            Status = RouteInstanceStatus.Draft,
            Steps = tpl.Steps.Select(ts => new RouteStep
            {
                Order = ts.Order,
                Mode = ts.Mode,
                Kind = ts.Kind,
                IsFinalMethodology = ts.IsFinalMethodology,
                TimeNormHours = ts.TimeNormHours,
                RequiredSignatureLevel = ts.RequiredSignatureLevel,
                Participants = ts.Participants.Select(tp => new RouteParticipant
                {
                    UserId = tp.UserId,
                    UnitId = tp.UnitId,
                    RoleRef = tp.RoleRef,
                    Required = tp.Required,
                    State = ParticipantState.Pending
                }).ToList()
            }).ToList()
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
        var due = step.TimeNormHours.HasValue ? DateTime.UtcNow.AddHours(step.TimeNormHours.Value) : (DateTime?)null;

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
                var due = step.TimeNormHours.HasValue ? DateTime.UtcNow.AddHours(step.TimeNormHours.Value) : (DateTime?)null;
                var activatedNext = ActivateParticipant(next, due);
                await _db.SaveChangesAsync();
                await _notifier.TaskAssignedAsync(activatedNext);
                return;
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
        if (task != null) task.State = state;
    }
}
