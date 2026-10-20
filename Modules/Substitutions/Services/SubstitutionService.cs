using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Notifications.DTO.Request;
using delosfera_server.Modules.Notifications.Models;
using delosfera_server.Modules.Notifications.Services;
using delosfera_server.Modules.Substitutions.DTO;
using delosfera_server.Modules.Substitutions.Models;
using ActingFor = delosfera_server.Modules.Users.Services.ActingFor;

namespace delosfera_server.Modules.Substitutions.Services;

public interface ISubstitutionService
{
    /// <summary>
    /// Реестр заявок. mineOnly — только где пользователь инициатор; participantOnly —
    /// где он инициатор или согласующий одного из этапов (так реестр видят все, у кого
    /// нет права на полный реестр).
    /// </summary>
    /// <para>absentUserId / substituteUserId / authorUserId — отсутствующий, замещающий и автор; periodFrom / periodTo —
    /// заявки, период замещения которых пересекается с указанным (границы включительно; любую
    /// можно не задавать).</para>
    Task<SubstitutionPage> SearchAsync(string? query, string? status, bool mineOnly, int currentUserId, int page, int pageSize,
        bool participantOnly = false, int? absentUserId = null, int? substituteUserId = null,
        DateOnly? periodFrom = null, DateOnly? periodTo = null, int? authorUserId = null, CancellationToken ct = default);
    Task<SubstitutionDetails?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Видна ли заявка пользователю: инициатору, согласующим её маршрута и тем, у кого
    /// есть право на полный реестр (<paramref name="isPrivileged"/>). Карточка содержит
    /// паспорт, ИНН и адреса замещающего — открывать её по одному {id} кому угодно нельзя.
    /// </summary>
    Task<bool> CanViewAsync(int id, int currentUserId, bool isPrivileged, CancellationToken ct = default);
    Task<SubstitutionDetails> CreateAsync(SubstitutionSaveRequest request, int actorUserId, CancellationToken ct = default);
    Task<SubstitutionDetails> UpdateAsync(int id, SubstitutionSaveRequest request, int actorUserId, bool isHrEditor = false, CancellationToken ct = default);
    Task<SubstitutionDetails> SubmitAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default);
    Task<SubstitutionDetails> ApproveAsync(int id, int actorUserId, string? comment, CancellationToken ct = default);
    Task<SubstitutionDetails> RejectAsync(int id, int actorUserId, string? comment, CancellationToken ct = default);

    /// <summary>
    /// Переназначить этап маршрута текущего круга другому сотруднику: согласующий уволен,
    /// заблокирован или надолго отсутствует без оформленного замещения. Ведут УЧР и
    /// администратор (право проверяет контроллер). Обоснование обязательно и пишется в журнал.
    /// </summary>
    Task<SubstitutionDetails> ReassignStepAsync(int id, int approvalId, int newUserId, string reason, int actorUserId,
        CancellationToken ct = default);

    /// <summary>За кого пользователь сейчас действует по замещению (GEN-14) — для карточки.</summary>
    Task<List<int>> ActingForUserIdsAsync(int userId, CancellationToken ct = default);
    Task<SubstitutionDetails> ExecuteAsync(int id, int actorUserId, CancellationToken ct = default);
    Task<SubstitutionDetails> WithdrawAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default);
    Task DeleteAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default);

    /// <summary>Печатная форма: form = "order" (приказ) или "liability" (договор МО).</summary>
    Task<(byte[] Bytes, string FileName)> PrintAsync(int id, string form, CancellationToken ct = default);

    /// <summary>Норматив срока согласования (ЗМ-SLA), рабочих дней на этап.</summary>
    Task<int> GetSlaDaysAsync(CancellationToken ct = default);

    /// <summary>Задать норматив срока согласования (ЗМ-SLA). Ведёт администратор.</summary>
    Task<int> SetSlaDaysAsync(int days, CancellationToken ct = default);

    /// <summary>
    /// Ведёт ли пользователь заявки на замещение как УЧР: он выбран кадровиком УЧР (по ГО или
    /// по филиалам) в настройках маршрута, либо сейчас замещает такого кадровика (GEN-14).
    /// Раньше полный доступ давало право «Видеть все СЗ» — а оно есть у Председателя, Правления,
    /// делопроизводства и руководителей, и все они видели паспорт, ИНН и адреса замещающих и
    /// могли «исполнить» чужую заявку.
    /// </summary>
    Task<bool> IsHrManagerAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Кадровик УЧР именно этой заявки — тот, кто отвечает за её кадровое оформление и отмечает
    /// «Исполнено (приказ оформлен)»: согласующий этапа «УЧР» действующего круга (кадровик по
    /// Головному офису, если отсутствующий из ГО, по филиалам — если из филиала; с учётом
    /// переназначения этапа), а если этапа нет — кадровик по области отсутствующего из настроек
    /// маршрута. Также тот, кто его сейчас замещает (отпуск, больничный — GEN-14).
    /// Администратор системы проверяется отдельно (контроллер) — как технический запасной вариант.
    /// </summary>
    Task<bool> IsResponsibleHrAsync(int id, int userId, CancellationToken ct = default);
}

/// <summary>
/// Заявки на замещение (КСЗ-В9). Инициатор заполняет карточку с комиссией приёма-передачи,
/// заявка идёт в УЧР на исполнение (приказ на время замещения).
/// </summary>
public class SubstitutionService : ISubstitutionService
{
    /// <summary>
    /// Тип записи в техническом аудите. Раньше заявки писались под «Substitution» — тем же
    /// типом, что и оформление замещения в настройках (Users.SubstitutionService), с
    /// пересекающимися id: журнал не мог отличить одно от другого, и заявки не попадали в
    /// «Последнюю активность». Свой тип — и строки журнала ведут в карточку заявки.
    /// </summary>
    public const string AuditEntityType = "SubstitutionRequest";

    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly INumeratorService _numerator;
    private readonly IBankClock _clock;
    private readonly ISubstitutionPrintService _print;
    private readonly INotificationService? _notifications;
    private readonly ILogger<SubstitutionService>? _logger;

    public SubstitutionService(DelosferaDbContext db, IAuditService audit, INumeratorService numerator,
        IBankClock clock, ISubstitutionPrintService print,
        INotificationService? notifications = null, ILogger<SubstitutionService>? logger = null)
    {
        _db = db;
        _audit = audit;
        _numerator = numerator;
        _clock = clock;
        _print = print;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<(byte[] Bytes, string FileName)> PrintAsync(int id, string form, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests
            .Include(x => x.CommissionMembers)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        // На подписании и после — тот самый зафиксированный файл, который подписывают, а не
        // заново собранный: иначе «без подписей» могло бы отличаться от подписанного.
        if (form is "order" or "liability")
        {
            var kind = form == "order" ? SubstitutionDocumentKind.Order : SubstitutionDocumentKind.Liability;
            var frozen = await _db.SubstitutionDocuments.AsNoTracking()
                .Where(d => d.RequestId == id && d.Kind == kind && d.IsCurrent)
                .FirstOrDefaultAsync(ct);
            if (frozen is not null) return (frozen.Content, frozen.FileName);
        }

        var stamp = entity.RegNumber ?? id.ToString();
        return form switch
        {
            "order" => (_print.Order(entity), $"Приказ о возложении обязанностей {stamp}.docx"),
            "liability" => (_print.Liability(entity), $"Договор о матответственности {stamp}.docx"),
            "card" => (_print.Card(entity), $"Заявка на замещение {stamp}.docx"),
            _ => throw new InvalidOperationException("Неизвестная форма печати"),
        };
    }

    private IQueryable<SubstitutionRequest> BaseQuery() =>
        _db.SubstitutionRequests.AsNoTracking()
            .Include(x => x.InitiatorUser)
            .Include(x => x.AuthorUser)
            .Include(x => x.AbsentUnit)
            .Include(x => x.SubstituteUnit)
            .Include(x => x.CommissionMembers.OrderBy(m => m.SortOrder))
            .Include(x => x.Approvals).ThenInclude(a => a.User);

    public async Task<SubstitutionPage> SearchAsync(
        string? query, string? status, bool mineOnly, int currentUserId, int page, int pageSize,
        bool participantOnly = false, int? absentUserId = null, int? substituteUserId = null,
        DateOnly? periodFrom = null, DateOnly? periodTo = null, int? authorUserId = null, CancellationToken ct = default)
    {
        var q = _db.SubstitutionRequests.AsNoTracking()
            .Include(x => x.InitiatorUser)
            .Include(x => x.AuthorUser)
            .AsQueryable();

        if (mineOnly)
            q = q.Where(x => x.InitiatorUserId == currentUserId);
        else if (participantOnly)
        {
            // Согласующий (сам или по замещению) должен находить заявку, которая ждёт или
            // ждала его решения. Раньше без права на полный реестр показывались только свои
            // заявки, и директор филиала не видел заявку, пришедшую к нему на согласование.
            q = await ParticipantFilterAsync(q, currentUserId, ct);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SubstitutionStatus>(status, out var st))
            q = q.Where(x => x.Status == st);

        if (absentUserId is int absentId)
            q = q.Where(x => x.AbsentUserId == absentId);
        if (substituteUserId is int substituteId)
            q = q.Where(x => x.SubstituteUserId == substituteId);
        if (authorUserId is int authorId)
            q = q.Where(x => x.AuthorUserId == authorId);

        // Период: заявка попадает, если её период замещения пересекается с заданным.
        // Незаполненная граница заявки считается открытой (замещение «с …» или «по …»).
        if (periodFrom is DateOnly from)
            q = q.Where(x => x.EndsOn == null || x.EndsOn >= from);
        if (periodTo is DateOnly to)
            q = q.Where(x => x.StartsOn == null || x.StartsOn <= to);
        if (periodFrom is not null || periodTo is not null)
            q = q.Where(x => x.StartsOn != null || x.EndsOn != null);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var text = query.Trim();
            q = q.Where(x =>
                EF.Functions.ILike(x.Subject, $"%{text}%")
                || EF.Functions.ILike(x.AbsentName, $"%{text}%")
                || EF.Functions.ILike(x.SubstituteName, $"%{text}%")
                || (x.RegNumber != null && EF.Functions.ILike(x.RegNumber, $"%{text}%")));
        }

        var total = await q.CountAsync(ct);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var rows = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return new SubstitutionPage
        {
            Items = rows.Select(ToListItem).ToList(),
            Total = total, Page = page, PageSize = pageSize,
        };
    }

    public async Task<bool> CanViewAsync(int id, int currentUserId, bool isPrivileged, CancellationToken ct = default)
    {
        if (isPrivileged) return true;
        // Согласующий этапа — сам или по замещению (GEN-14): замещающий согласует за
        // отсутствующего, значит и карточку должен открыть.
        var q = await ParticipantFilterAsync(_db.SubstitutionRequests.AsNoTracking(), currentUserId, ct);
        return await q.AnyAsync(x => x.Id == id, ct);
    }

    /// <summary>
    /// Заявки, в которых пользователь участвует: инициатор; согласующий любого этапа (сам или
    /// по замещению); отсутствующий и замещающий — с момента отправки (черновик видит только
    /// автор); подписант документов на этапе подписания (директор — в т.ч. подписант ГО из
    /// настроек). Остальным заявка не видна: в ней паспорт, ИНН и адреса замещающего.
    /// </summary>
    private async Task<IQueryable<SubstitutionRequest>> ParticipantFilterAsync(
        IQueryable<SubstitutionRequest> q, int userId, CancellationToken ct)
    {
        var approvers = await ActingFor.SelfAndAbsentAsync(_db, userId, _clock.Today);
        var signerDocs = _db.SubstitutionDocuments.Where(d => d.IsCurrent
            && d.DirectorUserId != null && approvers.Contains(d.DirectorUserId.Value));
        return q.Where(x => x.InitiatorUserId == userId
                            || x.Approvals.Any(a => approvers.Contains(a.UserId))
                            || (x.Status != SubstitutionStatus.Draft
                                && (x.AbsentUserId == userId || x.SubstituteUserId == userId))
                            || signerDocs.Any(d => d.RequestId == x.Id));
    }

    public async Task<bool> IsHrManagerAsync(int userId, CancellationToken ct = default)
    {
        var hr = await _db.HrRoutingSettings.AsNoTracking()
            .Select(s => new { s.HeadOfficeHrUserId, s.BranchHrUserId })
            .FirstOrDefaultAsync(ct);
        if (hr is null) return false;
        var self = await ActingFor.SelfAndAbsentAsync(_db, userId, _clock.Today);
        return (hr.HeadOfficeHrUserId is { } ho && self.Contains(ho))
               || (hr.BranchHrUserId is { } br && self.Contains(br));
    }

    public async Task<bool> IsResponsibleHrAsync(int id, int userId, CancellationToken ct = default)
    {
        var r = await _db.SubstitutionRequests.AsNoTracking()
            .Include(x => x.Approvals)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;

        var owner = await ResponsibleHrUserIdAsync(r, ct);
        return owner is { } o && await ActsForAsync(userId, o);
    }

    /// <summary>Кадровик УЧР заявки: этап «УЧР» действующего круга, иначе — по настройкам маршрута.</summary>
    private async Task<int?> ResponsibleHrUserIdAsync(SubstitutionRequest r, CancellationToken ct)
    {
        var round = CurrentRound(r.Approvals);
        var step = r.Approvals
            .Where(a => a.Round == round && a.RoleLabel == HrStepRole)
            .OrderByDescending(a => a.Order)
            .FirstOrDefault();
        return step?.UserId ?? await ResolveHrOfficerAsync(r.AbsentUnitId, ct);
    }

    /// <summary>Роль этапа УЧР в маршруте (см. BuildApprovalRouteAsync).</summary>
    private const string HrStepRole = "УЧР";

    public async Task<SubstitutionDetails?> GetAsync(int id, CancellationToken ct = default)
    {
        var x = await BaseQuery().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (x is null) return null;

        var details = ToDetails(x);

        // Кто фактически решал за назначенного (замещающий, GEN-14) — показываем в маршруте.
        var deciderIds = x.Approvals
            .Where(a => a.DecidedByUserId is int d && d != a.UserId)
            .Select(a => a.DecidedByUserId!.Value)
            .Distinct()
            .ToList();
        if (deciderIds.Count > 0)
            details.DeciderNames = await _db.Users.AsNoTracking()
                .Where(u => deciderIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return details;
    }

    public async Task<SubstitutionDetails> CreateAsync(SubstitutionSaveRequest request, int actorUserId, CancellationToken ct = default)
    {
        Validate(request);

        var now = DateTime.UtcNow;
        var entity = new SubstitutionRequest
        {
            Status = SubstitutionStatus.Draft,
            InitiatorUserId = request.InitiatorUserId ?? actorUserId,
            // Автор — тот, кто создаёт заявку; дальше Apply его не трогает, поле неизменяемое.
            AuthorUserId = actorUserId,
            Year = _clock.Today.Year,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(entity, request);

        _db.SubstitutionRequests.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditEntityType, entity.Id, "Created", actorUserId);

        return (await GetAsync(entity.Id, ct))!;
    }

    public async Task<SubstitutionDetails> UpdateAsync(int id, SubstitutionSaveRequest request, int actorUserId, bool isHrEditor = false, CancellationToken ct = default)
    {
        Validate(request);

        var entity = await _db.SubstitutionRequests
            .Include(x => x.CommissionMembers)
            .Include(x => x.Approvals)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        // Заявку правит её инициатор; УЧР — по праву ведения кадровых СЗ (isHrEditor).
        // Раньше проверялся только статус: любой мог переписать чужую карточку
        // (паспорт, ИНН, адреса) по {id}. Теперь доступ решает владелец/право.
        var isInitiator = entity.InitiatorUserId == actorUserId;

        // Withdrawn — заявки, отозванные до того, как отзыв стал возвращать в черновик: их тоже
        // можно поправить и отправить заново, а не оставлять зависшими.
        var editableByInitiator = isInitiator
            && entity.Status is SubstitutionStatus.Draft or SubstitutionStatus.Rejected or SubstitutionStatus.Withdrawn;

        // УЧР правит заявку после согласования Начальником операционного управления —
        // то есть когда этап «Операционное управление» уже пройден (Approved).
        var currentRound = CurrentRound(entity.Approvals);
        var opsApproved = entity.Approvals.Any(a => a.Round == currentRound &&
            a.RoleLabel == "Операционное управление" && a.State == SubstitutionApprovalState.Approved);
        var editableByHr = isHrEditor && opsApproved
            && entity.Status is SubstitutionStatus.OnApproval or SubstitutionStatus.OnExecution;

        if (!editableByInitiator && !editableByHr)
        {
            // Посторонний (не инициатор и без права УЧР) — это отказ в доступе (403),
            // а не ошибка статуса. Инициатору/УЧР с неподходящим статусом — 400/409.
            if (!isInitiator && !isHrEditor)
                throw new UnauthorizedAccessException(
                    "Редактировать заявку на замещение может только инициатор или УЧР");
            throw new InvalidOperationException(
                "Изменять можно черновик, отклонённую заявку, либо (УЧР) после согласования Операционным управлением");
        }

        // Лог изменений: снимок ключевых полей до и после для аудита.
        var before = Snapshot(entity);
        Apply(entity, request);
        var after = Snapshot(entity);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var changes = before.Where(kv => !Equals(kv.Value, after[kv.Key]))
            .ToDictionary(kv => kv.Key, kv => new { was = kv.Value, now = after[kv.Key] });
        await _audit.LogAsync(AuditEntityType, id, editableByHr ? "UpdatedByHr" : "Updated", actorUserId,
            new { byHr = editableByHr, changes });

        // Правка УЧР меняет текст приказа и договора — подписывать надо уже новую версию:
        // документы фиксируются заново, прежние подписи остаются в журнале под старой версией и
        // показываются неактуальными (как у СЗ и ВНД). На подписании подписантам сразу уходит
        // уведомление; на согласовании — новая подпись запрашивается после прохождения маршрута.
        if (editableByHr && changes.Count > 0)
        {
            if (entity.Status == SubstitutionStatus.OnExecution)
                await StartSigningAsync(entity, actorUserId, refreeze: true, ct, edited: true);
            else if (entity.Status == SubstitutionStatus.OnApproval && await HasDocumentsAsync(id, ct))
                await RefreezeQuietlyAsync(entity, actorUserId, "edited", ct);
        }

        return (await GetAsync(id, ct))!;
    }

    /// <summary>Снимок редактируемых полей заявки — для журнала изменений.</summary>
    private static Dictionary<string, object?> Snapshot(SubstitutionRequest e) => new()
    {
        ["subject"] = e.Subject,
        ["reason"] = e.Reason.ToString(),
        ["absentName"] = e.AbsentName,
        ["absentPosition"] = e.AbsentPosition,
        ["absentUnitId"] = e.AbsentUnitId,
        ["substituteName"] = e.SubstituteName,
        ["substitutePosition"] = e.SubstitutePosition,
        ["substituteUnitId"] = e.SubstituteUnitId,
        ["passportSeriesNumber"] = e.PassportSeriesNumber,
        ["inn"] = e.Inn,
        ["startsOn"] = e.StartsOn?.ToString("yyyy-MM-dd"),
        ["endsOn"] = e.EndsOn?.ToString("yyyy-MM-dd"),
        ["handoverOn"] = e.HandoverOn?.ToString("yyyy-MM-dd"),
        ["description"] = e.Description,
        // Остальное, что попадает в приказ и договор: без этого правка, например, состава
        // комиссии или адреса не заводила бы новую версию документов на подпись.
        ["absentUserId"] = e.AbsentUserId,
        ["absentBranch"] = e.AbsentBranch,
        ["substituteUserId"] = e.SubstituteUserId,
        ["substituteBranch"] = e.SubstituteBranch,
        ["passportIssuedBy"] = e.PassportIssuedBy,
        ["passportIssuedOn"] = e.PassportIssuedOn?.ToString("yyyy-MM-dd"),
        ["passportValidUntil"] = e.PassportValidUntil?.ToString("yyyy-MM-dd"),
        ["addressRegistration"] = e.AddressRegistration,
        ["addressResidence"] = e.AddressResidence,
        ["daysCount"] = e.DaysCount,
        ["commissionChair"] = $"{e.CommissionChairName} | {e.CommissionChairPosition}",
        ["handoverMoment"] = e.HandoverMoment.ToString(),
        ["commission"] = string.Join("; ", e.CommissionMembers.OrderBy(m => m.SortOrder)
            .Select(m => $"{m.FullName} | {m.Position}")),
    };

    public async Task<SubstitutionDetails> SubmitAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        // Отправить черновик в УЧР может только его инициатор (или администратор).
        // Иначе посторонний присваивал бы рег.номер чужой заявке по {id}.
        if (entity.InitiatorUserId != actorUserId && !isPrivileged)
            throw new UnauthorizedAccessException("Отправить заявку может только инициатор");

        if (entity.Status is not (SubstitutionStatus.Draft or SubstitutionStatus.Rejected or SubstitutionStatus.Withdrawn))
            throw new InvalidOperationException("Отправить можно только черновик или отклонённую заявку");

        if (string.IsNullOrWhiteSpace(entity.SubstituteName) || entity.StartsOn is null || entity.EndsOn is null)
            throw new InvalidOperationException("Заполните замещающего и период замещения");

        // Маршрут согласования: директор филиала → Операционное управление → УЧР.
        // Повторная отправка (после отклонения) собирает маршрут заново — новым кругом.
        // Этапы прошлых кругов остаются в истории вместе с решениями и комментариями: раньше
        // они удалялись, и из карточки пропадало, кто и почему возвращал заявку.
        var previous = await _db.SubstitutionApprovals.Where(a => a.RequestId == id).ToListAsync(ct);
        foreach (var old in previous.Where(a => a.State is SubstitutionApprovalState.Pending or SubstitutionApprovalState.Active))
            old.State = SubstitutionApprovalState.Skipped;
        var round = previous.Count == 0 ? 1 : previous.Max(a => a.Round) + 1;

        var (steps, routeNotes) = await BuildApprovalRouteAsync(entity, round, ct);
        entity.RouteNote = routeNotes.Count == 0 ? null : string.Join("\n", routeNotes);

        // Сквозная нумерация HR-1, HR-2, … без сброса по годам (scope Global).
        // Формат и текущий счётчик правятся в настройках (SubstitutionNumberingController).
        // Номер выдаётся после сборки маршрута: если отправка остановится (заблокирован
        // согласующий), номер не пропадёт впустую.
        if (string.IsNullOrWhiteSpace(entity.RegNumber))
            entity.RegNumber = await _numerator.NextAsync(
                DocumentType.Custom, "Substitution", "Global", "HR-{seq}");

        if (steps.Count > 0)
        {
            steps[0].State = SubstitutionApprovalState.Active;
            steps[0].ActivatedAt = DateTime.UtcNow;   // старт отсчёта SLA первого этапа (ЗМ-SLA)
            foreach (var s in steps) _db.SubstitutionApprovals.Add(s);
            entity.Status = SubstitutionStatus.OnApproval;
        }
        else
        {
            // Согласующих определить не удалось (например, замещение в ГО без Опер. управления
            // и УЧР) — заявка идёт сразу в УЧР на исполнение, как раньше.
            entity.Status = SubstitutionStatus.OnExecution;
        }
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditEntityType, id, "Submitted", actorUserId,
            new { regNumber = entity.RegNumber, steps = steps.Count, round, skipped = routeNotes });

        if (steps.Count > 0)
        {
            // Приказ и договор фиксируются уже при отправке: директор филиала (а также согласующие
            // Опер. управления и УЧР, если они же отсутствующий или замещающий) подписывают их ЭЦП
            // прямо на своём этапе согласования, не дожидаясь конца маршрута. Повторная отправка
            // заводит новую версию — подписи прошлой версии становятся неактуальными.
            await FreezeDocumentsAsync(entity.Id, ct);
            await NotifyApproverAsync(entity, steps[0], actorUserId, ct);
        }
        else
            await StartSigningAsync(entity, actorUserId, refreeze: true, ct);

        // Пропущенные этапы не останавливают заявку, но о них должны знать и инициатор, и УЧР:
        // иначе выглядело так, будто заявку «никто не согласовывал».
        if (routeNotes.Count > 0)
            await NotifyAsync(
                (await HrOfficerIdsAsync(ct)).Prepend(entity.InitiatorUserId).Distinct().ToList(),
                "Заявка на замещение отправлена не по полному маршруту",
                $"Заявка на замещение {Number(entity)}: {string.Join(" ", routeNotes)} " +
                "Назначьте согласующих в оргструктуре или в настройках маршрута УЧР.",
                entity.Id, actorUserId, ct);

        return (await GetAsync(id, ct))!;
    }

    public async Task<SubstitutionDetails> ExecuteAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        if (entity.Status != SubstitutionStatus.OnExecution)
            throw new InvalidOperationException("Исполнить можно только заявку на подписании");

        // Кто отметил: кадровик заявки сам, его замещающий (за кадровика) или администратор.
        var hrOwner = await ResponsibleHrUserIdAsync(
            await _db.SubstitutionRequests.AsNoTracking().Include(x => x.Approvals).FirstAsync(x => x.Id == id, ct), ct);

        entity.Status = SubstitutionStatus.Executed;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        var onBehalfOf = hrOwner is { } h && h != actorUserId && await ActsForAsync(actorUserId, h) ? h : (int?)null;
        await _audit.LogAsync(AuditEntityType, id, "Executed", actorUserId, new { onBehalfOf });

        return (await GetAsync(id, ct))!;
    }

    // ── Маршрут согласования: директор филиала → Операционное управление → УЧР ──

    // Операционное управление согласует Касымов К.Т.; при его отсутствии — Ермакова Ю.А.
    // Идентификаторы вынесены в HrRoutingSettings (ЗМ-Настр); константы — только крайний
    // fallback, если строка настроек ещё не заполнена, чтобы маршрут не сломался.
    private const int OperationsApproverId = 141;   // Касымов Кубатбек (Операционное управление)
    private const int OperationsFallbackId = 563;   // Ермакова Юлия (замена при отсутствии)

    /// <summary>
    /// Маршрут заявки и пометки о пропущенных этапах. Этап, для которого в системе не настроен
    /// согласующий (у филиала не назначен начальник, в настройках маршрута УЧР не выбран
    /// согласующий Операционного управления или кадровик), пропускается — как и раньше, — но
    /// теперь не молча: пометка уходит в карточку и уведомлением инициатору и УЧР. У сотрудника
    /// Головного офиса этапа директора филиала нет по устройству маршрута — это не пропуск.
    /// </summary>
    private async Task<(List<SubstitutionApproval> Steps, List<string> Notes)> BuildApprovalRouteAsync(
        SubstitutionRequest entity, int round, CancellationToken ct)
    {
        var steps = new List<SubstitutionApproval>();
        var notes = new List<string>();
        var order = 1;

        var (isBranch, directorId) = await SubstitutionDocuments.BranchOfAsync(_db, entity.AbsentUnitId, ct);
        if (directorId is { } dir)
        {
            await EnsureApproverActiveAsync(dir, "Директор филиала", ct);
            steps.Add(new SubstitutionApproval { RequestId = entity.Id, Round = round, Order = order++, RoleLabel = "Директор филиала", UserId = dir });
        }
        else if (isBranch)
        {
            notes.Add($"Этап «Директор филиала» пропущен: у филиала «{entity.AbsentBranch ?? "—"}» в оргструктуре не назначен начальник.");
        }

        var opsId = await ResolveOperationsApproverAsync(ct);
        if (opsId is { } ops)
            steps.Add(new SubstitutionApproval { RequestId = entity.Id, Round = round, Order = order++, RoleLabel = "Операционное управление", UserId = ops });
        else
            notes.Add("Этап «Операционное управление» пропущен: в настройках маршрута УЧР не выбран действующий согласующий.");

        var hrId = await ResolveHrOfficerAsync(entity.AbsentUnitId, ct);
        if (hrId is { } hr)
        {
            await EnsureApproverActiveAsync(hr, "УЧР", ct);
            steps.Add(new SubstitutionApproval { RequestId = entity.Id, Round = round, Order = order++, RoleLabel = "УЧР", UserId = hr });
        }
        else
        {
            notes.Add($"Этап «УЧР» пропущен: в настройках маршрута УЧР не выбран кадровик по {(isBranch ? "филиальной сети" : "Головному офису")}.");
        }

        return (steps, notes);
    }

    /// <summary>
    /// Этап не назначается заблокированному или отключённому сотруднику: решение по нему не
    /// принял бы никто, и заявка стояла бы бессрочно. Пропускать этап молча тоже нельзя —
    /// это обход согласования, — поэтому отправка останавливается с понятной причиной.
    /// </summary>
    private async Task EnsureApproverActiveAsync(int userId, string roleLabel, CancellationToken ct)
    {
        if (await IsActiveAsync(userId, ct)) return;

        var name = await _db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => u.FullName).FirstOrDefaultAsync(ct);
        throw new InvalidOperationException(
            $"Согласующий этапа «{roleLabel}» ({name ?? $"id {userId}"}) заблокирован или отключён. " +
            "Назначьте действующего сотрудника в оргструктуре или в настройках маршрута УЧР и отправьте заявку снова.");
    }

    /// <summary>Номер действующего круга согласования — последний.</summary>
    private static int CurrentRound(IEnumerable<SubstitutionApproval> approvals) =>
        approvals.Select(a => a.Round).DefaultIfEmpty(1).Max();

    /// <summary>Активный этап действующего круга.</summary>
    private static SubstitutionApproval? ActiveStep(SubstitutionRequest entity)
    {
        var round = CurrentRound(entity.Approvals);
        return entity.Approvals.FirstOrDefault(a => a.Round == round && a.State == SubstitutionApprovalState.Active);
    }

    /// <summary>Пользователь — это назначенный на этап или его действующий замещающий (GEN-14).</summary>
    private Task<bool> ActsForAsync(int actorUserId, int principalUserId) =>
        ActingFor.ActsForAsync(_db, actorUserId, principalUserId, _clock.Today);

    public Task<List<int>> ActingForUserIdsAsync(int userId, CancellationToken ct = default) =>
        ActingFor.AbsentUserIdsAsync(_db, userId, _clock.Today);

    /// <summary>Директор филиала: поднимаемся по дереву от подразделения сотрудника до филиала и берём его начальника.</summary>
    private async Task<int?> ResolveBranchDirectorAsync(int? unitId, CancellationToken ct)
    {
        var guard = 0;
        var current = unitId;
        while (current is { } id && guard++ < 20)
        {
            var u = await _db.OrganizationUnits.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.TitleRu, x.HeadUserId, x.ParentId }).FirstOrDefaultAsync(ct);
            if (u is null) return null;
            if (u.TitleRu.Contains("Филиал", StringComparison.OrdinalIgnoreCase))
                return u.HeadUserId;   // если начальник филиала не задан — этап пропускается
            current = u.ParentId;
        }
        return null;
    }

    private async Task<int?> ResolveOperationsApproverAsync(CancellationToken ct)
    {
        var settings = await _db.HrRoutingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var primary = settings?.OperationsApproverUserId ?? OperationsApproverId;
        var fallback = settings?.OperationsFallbackUserId ?? OperationsFallbackId;

        if (await IsActiveAsync(primary, ct)) return primary;
        if (await IsActiveAsync(fallback, ct)) return fallback;
        return null;
    }

    /// <summary>УЧР: кадровик по области сотрудника (филиал → кадровик по филиалам, иначе по головному офису).</summary>
    private async Task<int?> ResolveHrOfficerAsync(int? unitId, CancellationToken ct)
    {
        var settings = await _db.HrRoutingSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings is null) return null;
        return await IsBranchUnitAsync(unitId, ct) ? settings.BranchHrUserId : settings.HeadOfficeHrUserId;
    }

    private async Task<bool> IsBranchUnitAsync(int? unitId, CancellationToken ct)
    {
        var guard = 0;
        var current = unitId;
        while (current is { } id && guard++ < 20)
        {
            var u = await _db.OrganizationUnits.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.TitleRu, x.ParentId }).FirstOrDefaultAsync(ct);
            if (u is null) return false;
            if (u.TitleRu.Contains("Филиал", StringComparison.OrdinalIgnoreCase)) return true;
            current = u.ParentId;
        }
        return false;
    }

    private Task<bool> IsActiveAsync(int userId, CancellationToken ct) =>
        _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.BlockedAt == null, ct);

    public async Task<SubstitutionDetails> ApproveAsync(int id, int actorUserId, string? comment, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");
        if (entity.Status != SubstitutionStatus.OnApproval)
            throw new InvalidOperationException("Согласовать можно только заявку на согласовании");

        var step = ActiveStep(entity)
            ?? throw new InvalidOperationException("Нет активного этапа согласования");
        // Согласует назначенный на этап или тот, кто его сейчас замещает (GEN-14): иначе
        // заявка стояла, пока директор филиала в отпуске.
        if (!await ActsForAsync(actorUserId, step.UserId))
            throw new UnauthorizedAccessException("Согласовать может только назначенный на этап сотрудник или его замещающий");

        step.State = SubstitutionApprovalState.Approved;
        step.Comment = comment;
        step.DecidedAt = DateTime.UtcNow;
        step.DecidedByUserId = actorUserId;

        var next = entity.Approvals
            .Where(a => a.Round == step.Round && a.State == SubstitutionApprovalState.Pending)
            .OrderBy(a => a.Order).FirstOrDefault();
        if (next is not null)
        {
            next.State = SubstitutionApprovalState.Active;
            next.ActivatedAt = DateTime.UtcNow;   // отсчёт SLA следующего этапа (ЗМ-SLA)
        }
        else
            entity.Status = SubstitutionStatus.OnExecution;   // маршрут пройден — УЧР исполняет

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditEntityType, id, "Approved", actorUserId, new
        {
            step = step.RoleLabel,
            round = step.Round,
            onBehalfOf = step.UserId != actorUserId ? step.UserId : (int?)null,
        });

        if (next is not null)
            await NotifyApproverAsync(entity, next, actorUserId, ct);
        else
        {
            // Маршрут пройден — фиксируем приказ и договор и зовём подписантов (этап «На подписании»).
            // Инициатора извещаем последним: его уведомление — итог согласования.
            await StartSigningAsync(entity, actorUserId, refreeze: false, ct);
            await NotifyInitiatorAsync(entity,
                "Заявка на замещение согласована",
                $"Заявка на замещение {Number(entity)} согласована и передана на подписание документов. Тема: {entity.Subject}",
                actorUserId, ct);
        }

        return (await GetAsync(id, ct))!;
    }

    public async Task<SubstitutionDetails> RejectAsync(int id, int actorUserId, string? comment, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");
        if (entity.Status != SubstitutionStatus.OnApproval)
            throw new InvalidOperationException("Отклонить можно только заявку на согласовании");

        // Причина отклонения обязательна: по ней инициатор исправляет заявку, и она остаётся
        // в истории круга. Раньше отклонение проходило и без неё — в том числе по кнопке
        // «Отмена» в окне ввода причины.
        if (string.IsNullOrWhiteSpace(comment))
            throw new InvalidOperationException("Укажите причину отклонения");

        var step = ActiveStep(entity)
            ?? throw new InvalidOperationException("Нет активного этапа согласования");
        if (!await ActsForAsync(actorUserId, step.UserId))
            throw new UnauthorizedAccessException("Отклонить может только назначенный на этап сотрудник или его замещающий");

        step.State = SubstitutionApprovalState.Rejected;
        step.Comment = comment.Trim();
        step.DecidedAt = DateTime.UtcNow;
        step.DecidedByUserId = actorUserId;

        // Дальше по этому кругу очередь не дойдёт: этапы закрываются, но остаются в истории.
        foreach (var rest in entity.Approvals.Where(a => a.Round == step.Round && a.State == SubstitutionApprovalState.Pending))
            rest.State = SubstitutionApprovalState.Skipped;
        entity.Status = SubstitutionStatus.Rejected;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditEntityType, id, "Rejected", actorUserId, new
        {
            step = step.RoleLabel,
            round = step.Round,
            comment = step.Comment,
            onBehalfOf = step.UserId != actorUserId ? step.UserId : (int?)null,
        });

        await NotifyInitiatorAsync(entity,
            "Заявка на замещение отклонена",
            $"Заявка на замещение {Number(entity)} отклонена на этапе «{step.RoleLabel}»: {step.Comment}",
            actorUserId, ct);

        return (await GetAsync(id, ct))!;
    }

    public async Task<SubstitutionDetails> ReassignStepAsync(
        int id, int approvalId, int newUserId, string reason, int actorUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Укажите причину переназначения этапа");

        var entity = await _db.SubstitutionRequests.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");
        if (entity.Status != SubstitutionStatus.OnApproval)
            throw new InvalidOperationException("Переназначить этап можно только у заявки на согласовании");

        var step = entity.Approvals.FirstOrDefault(a => a.Id == approvalId)
            ?? throw new KeyNotFoundException("Этап согласования не найден");
        if (step.Round != CurrentRound(entity.Approvals)
            || step.State is not (SubstitutionApprovalState.Active or SubstitutionApprovalState.Pending))
            throw new InvalidOperationException("Переназначить можно только ещё не пройденный этап действующего маршрута");
        if (step.UserId == newUserId)
            throw new InvalidOperationException("Этап уже назначен этому сотруднику");
        if (!await IsActiveAsync(newUserId, ct))
            throw new InvalidOperationException("Нельзя назначить этап заблокированному, отключённому или несуществующему сотруднику");

        var from = step.UserId;
        step.UserId = newUserId;
        // Новый согласующий получает полный срок: отсчёт SLA этапа начинается заново.
        if (step.State == SubstitutionApprovalState.Active)
            step.ActivatedAt = DateTime.UtcNow;

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditEntityType, id, "StepReassigned", actorUserId, new
        {
            step = step.RoleLabel,
            round = step.Round,
            from,
            to = newUserId,
            reason = reason.Trim(),
        });

        // Директор филиала — он же подписант приказа и договора: место подписи в зафиксированных
        // документах должно перейти к новому директору — заводим новую версию.
        if (step.RoleLabel == SubstitutionDocuments.DirectorStepRole && await HasDocumentsAsync(id, ct))
            await RefreezeQuietlyAsync(entity, actorUserId, "directorReassigned", ct);

        if (step.State == SubstitutionApprovalState.Active)
            await NotifyApproverAsync(entity, step, actorUserId, ct);

        return (await GetAsync(id, ct))!;
    }

    public async Task<SubstitutionDetails> WithdrawAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        // Отозвать заявку из работы вправе только её инициатор (или администратор).
        if (entity.InitiatorUserId != actorUserId && !isPrivileged)
            throw new UnauthorizedAccessException("Отозвать заявку может только инициатор");

        if (entity.Status is not (SubstitutionStatus.OnApproval or SubstitutionStatus.OnExecution))
            throw new InvalidOperationException("Отозвать можно только заявку на согласовании или на подписании");

        // Отзыв возвращает заявку в черновик: инициатор правит её и отправляет заново (новым
        // кругом). Раньше заявка переходила в «Отозвано» и там зависала — ни правки, ни повторной
        // отправки, а пометка «отправлена не по полному маршруту» висела от прошлой отправки.
        // Непройденные этапы круга закрываются (остаются в истории), пометка о маршруте снимается —
        // при повторной отправке маршрут и пометка соберутся заново.
        var round = CurrentRound(entity.Approvals);
        var active = entity.Approvals.FirstOrDefault(a => a.Round == round && a.State == SubstitutionApprovalState.Active);
        foreach (var step in entity.Approvals.Where(a => a.Round == round
                     && a.State is SubstitutionApprovalState.Pending or SubstitutionApprovalState.Active))
            step.State = SubstitutionApprovalState.Skipped;

        var from = entity.Status;
        entity.Status = SubstitutionStatus.Draft;
        entity.RouteNote = null;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditEntityType, id, "Withdrawn", actorUserId,
            new { from = from.ToString(), to = SubstitutionStatus.Draft.ToString(), round });

        // Тот, кто ждал решения по этапу, должен знать, что заявки у него больше нет.
        if (active is not null)
            await NotifyAsync(
                (await ActingFor.SubstituteIdsAsync(_db, active.UserId, _clock.Today)).Prepend(active.UserId).Distinct().ToList(),
                "Заявка на замещение отозвана",
                $"Заявка на замещение {Number(entity)} отозвана инициатором и возвращена в черновик — решение по этапу «{active.RoleLabel}» больше не требуется.",
                entity.Id, actorUserId, ct);

        return (await GetAsync(id, ct))!;
    }

    public async Task DeleteAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        // Удалить черновик может только его инициатор (или администратор).
        if (entity.InitiatorUserId != actorUserId && !isPrivileged)
            throw new UnauthorizedAccessException("Удалить заявку может только инициатор");

        if (entity.Status != SubstitutionStatus.Draft)
            throw new InvalidOperationException("Удалить можно только черновик");

        _db.SubstitutionRequests.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditEntityType, id, "Deleted", actorUserId);
    }

    // ── Подписание документов (этап «На подписании») ─────────────────────────

    /// <summary>
    /// Этап «На подписании»: зафиксировать (или взять уже зафиксированные) приказ и договор и
    /// позвать подписантов. refreeze — собрать новую версию документов; edited — это правка УЧР
    /// на подписании (в журнал — «DocumentsRefrozen»). Сбой формирования документов не откатывает
    /// согласование: заявку можно исполнить и с бумажными формами, а УЧР получит запись в журнале.
    /// </summary>
    private async Task StartSigningAsync(SubstitutionRequest entity, int actorUserId, bool refreeze, CancellationToken ct,
        bool edited = false)
    {
        // Документы, зафиксированные при отправке, переиспользуются: подписи, поставленные на
        // этапе согласования (директор филиала и др.), остаются действующими. Новая версия
        // заводится только если документов ещё нет или их надо собрать заново (refreeze).
        var docs = refreeze
            ? []
            : await _db.SubstitutionDocuments.Where(d => d.RequestId == entity.Id && d.IsCurrent).ToListAsync(ct);
        if (docs.Count == 0)
        {
            var fresh = await FreezeDocumentsAsync(entity.Id, ct);
            if (fresh is null) return;
            docs = fresh;
        }

        await _audit.LogAsync(AuditEntityType, entity.Id, edited ? "DocumentsRefrozen" : "SigningStarted", actorUserId,
            new { documents = docs.Select(d => new { kind = d.Kind.ToString(), d.Version }).ToList() });

        await NotifyPendingSignersAsync(entity, docs, actorUserId, ct);
    }

    /// <summary>
    /// Зафиксировать приказ и договор новой версией. null — не удалось (ошибка в журнале):
    /// сбой формирования документов не откатывает согласование — заявку можно исполнить и с
    /// бумажными формами.
    /// </summary>
    private async Task<List<SubstitutionDocument>?> FreezeDocumentsAsync(int requestId, CancellationToken ct)
    {
        try
        {
            var docs = await SubstitutionDocuments.FreezeAsync(_db, _print, requestId, ct);
            await _db.SaveChangesAsync(ct);
            return docs;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Не удалось зафиксировать документы заявки на замещение {RequestId}", requestId);
            return null;
        }
    }

    /// <summary>
    /// Заявка ещё на согласовании, а документы изменились (правка УЧР, переназначение директора):
    /// новая версия заводится сразу, прежние подписи становятся неактуальными. Новую подпись
    /// запрашиваем, когда маршрут будет пройден (StartSigningAsync), — до того только журнал.
    /// </summary>
    private async Task RefreezeQuietlyAsync(SubstitutionRequest entity, int actorUserId, string reason, CancellationToken ct)
    {
        var docs = await FreezeDocumentsAsync(entity.Id, ct);
        if (docs is null) return;
        await _audit.LogAsync(AuditEntityType, entity.Id, "DocumentsRefrozen", actorUserId, new
        {
            reason,
            documents = docs.Select(d => new { kind = d.Kind.ToString(), d.Version }).ToList(),
        });
    }

    private Task<bool> HasDocumentsAsync(int requestId, CancellationToken ct) =>
        _db.SubstitutionDocuments.AnyAsync(d => d.RequestId == requestId && d.IsCurrent, ct);

    /// <summary>
    /// Позвать тех, чья подпись под действующими версиями ещё не стоит. Кто подписывал прежнюю
    /// версию, получает «подпишите заново»: его подпись неактуальна, и пока новой нет, на листе
    /// подписей ЭЦП место отмечается автосогласованием. Уведомление уходит и замещающим
    /// подписантов (GEN-14): директор в отпуске — подпишет замещающий.
    /// </summary>
    private async Task NotifyPendingSignersAsync(SubstitutionRequest entity, List<SubstitutionDocument> docs,
        int actorUserId, CancellationToken ct)
    {
        var docIds = docs.Select(d => d.Id).ToList();
        var signatures = await _db.SubstitutionSignatures.AsNoTracking()
            .Where(s => s.RequestId == entity.Id)
            .Select(s => new { s.DocumentId, s.Role, Owner = s.OnBehalfOfUserId ?? s.UserId, s.Document!.Kind })
            .ToListAsync(ct);

        var fresh = new List<int>();
        var stale = new List<int>();
        foreach (var d in docs)
        foreach (var slot in SubstitutionDocuments.Slots(d))
        {
            if (slot.UserId is not { } owner) continue;
            if (signatures.Any(s => s.DocumentId == d.Id && s.Role == slot.Role)) continue;
            var signedBefore = signatures.Any(s => !docIds.Contains(s.DocumentId)
                                                   && s.Kind == d.Kind && s.Role == slot.Role && s.Owner == owner);
            (signedBefore ? stale : fresh).Add(owner);
        }

        async Task<List<int>> WithSubstitutes(IEnumerable<int> ids)
        {
            var list = ids.Distinct().ToList();
            foreach (var id in list.ToList())
                list.AddRange(await ActingFor.SubstituteIdsAsync(_db, id, _clock.Today));
            return list.Distinct().ToList();
        }

        var staleRecipients = await WithSubstitutes(stale);
        var freshRecipients = (await WithSubstitutes(fresh)).Except(staleRecipients).ToList();
        var who = $"Отсутствующий: {entity.AbsentName}, замещающий: {entity.SubstituteName}.";

        await NotifyAsync(freshRecipients,
            "Документы по заявке на замещение ждут вашей подписи",
            $"Заявка на замещение {Number(entity)}: приказ о временном возложении обязанностей и договор о материальной " +
            $"ответственности ждут подписи ЭЦП. {who}",
            entity.Id, actorUserId, ct);
        await NotifyAsync(staleRecipients,
            "Документы по заявке на замещение изменены — подпишите заново",
            $"Заявка на замещение {Number(entity)}: после вашей подписи приказ и договор были изменены — прежняя " +
            $"подпись неактуальна, подпишите новую версию ЭЦП. Пока новой подписи нет, на листе подписей ЭЦП " +
            $"ставится автосогласование. {who}",
            entity.Id, actorUserId, ct);
    }

    /// <summary>Кадровики УЧР из настроек маршрута (по ГО и по филиалам).</summary>
    private async Task<List<int>> HrOfficerIdsAsync(CancellationToken ct)
    {
        var s = await _db.HrRoutingSettings.AsNoTracking()
            .Select(x => new { x.HeadOfficeHrUserId, x.BranchHrUserId }).FirstOrDefaultAsync(ct);
        return new[] { s?.HeadOfficeHrUserId, s?.BranchHrUserId }
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
    }

    // ── Норматив срока согласования (ЗМ-SLA) ──────────────────────────────────

    /// <summary>Минимум/максимум норматива, чтобы настройка не выключала контроль и не была абсурдной.</summary>
    private const int MinSlaDays = 1;
    private const int MaxSlaDays = 30;
    private const int DefaultSlaDays = 3;

    public async Task<int> GetSlaDaysAsync(CancellationToken ct = default)
    {
        var s = await _db.SubstitutionSlaSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return s?.ApprovalStepSlaDays ?? DefaultSlaDays;
    }

    public async Task<int> SetSlaDaysAsync(int days, CancellationToken ct = default)
    {
        if (days is < MinSlaDays or > MaxSlaDays)
            throw new InvalidOperationException($"Норматив срока согласования — от {MinSlaDays} до {MaxSlaDays} рабочих дней");

        var now = DateTime.UtcNow;
        var s = await _db.SubstitutionSlaSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            s = new SubstitutionSlaSettings { ApprovalStepSlaDays = days, CreatedAt = now };
            _db.SubstitutionSlaSettings.Add(s);
        }
        else
        {
            s.ApprovalStepSlaDays = days;
        }
        s.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return s.ApprovalStepSlaDays;
    }

    // ── Уведомления ──────────────────────────────────────────────────────────

    /// <summary>
    /// Согласующему — что заявка ждёт его решения. Раньше не уходило ничего: ни задачи,
    /// ни уведомления, и согласующий узнавал о заявке только из напоминания о просрочке
    /// норматива (ЗМ-SLA). Сбой рассылки не откатывает действие.
    /// </summary>
    private async Task NotifyApproverAsync(SubstitutionRequest entity, SubstitutionApproval step, int actorUserId, CancellationToken ct) =>
        // И тем, кто сейчас замещает согласующего (GEN-14): иначе о задаче узнаёт только
        // тот, кто в отпуске.
        await NotifyAsync(
            (await ActingFor.SubstituteIdsAsync(_db, step.UserId, _clock.Today)).Prepend(step.UserId).Distinct().ToList(),
            "Заявка на замещение ждёт вашего согласования",
            $"Заявка на замещение {Number(entity)} ждёт вашего решения на этапе «{step.RoleLabel}». " +
            $"Отсутствующий: {entity.AbsentName}, замещающий: {entity.SubstituteName}. Тема: {entity.Subject}",
            entity.Id, actorUserId, ct);

    private Task NotifyInitiatorAsync(SubstitutionRequest entity, string title, string body, int actorUserId, CancellationToken ct) =>
        NotifyAsync([entity.InitiatorUserId], title, body, entity.Id, actorUserId, ct);

    private async Task NotifyAsync(List<int> userIds, string title, string body, int requestId, int actorUserId, CancellationToken ct)
    {
        // Некому слать (например, подписанты не определены) — пустое уведомление не создаём.
        if (_notifications is null || userIds.Count == 0) return;
        try
        {
            await _notifications.CreateAsync(new CreateNotificationRequest
            {
                TitleRu = title,
                BodyRu = body,
                // Заявки на замещение ведутся в разделе СЗ (УЧР) — и уведомления о них идут под
                // вкладкой «СЗ» с её значком, а не в легаси-«Разное» без вкладки (значок «…»).
                Category = NotificationCategory.Sz,
                EntityType = "SubstitutionRequest",
                EntityId = requestId,
                Url = $"/substitutions/{requestId}",
                UserIds = userIds,
            }, actorUserId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Заявка уже сохранена; неотправленное уведомление этого не отменяет.
            _logger?.LogWarning(ex, "Не удалось отправить уведомление по заявке на замещение {RequestId}", requestId);
        }
    }

    private static string Number(SubstitutionRequest e) =>
        string.IsNullOrWhiteSpace(e.RegNumber) ? $"#{e.Id}" : e.RegNumber;

    // ── вспомогательное ──────────────────────────────────────────────────────

    private static void Validate(SubstitutionSaveRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Subject))
            throw new InvalidOperationException("Укажите тему заявки");

        if (r.StartsOn is { } s && r.EndsOn is { } e && e < s)
            throw new InvalidOperationException("Дата окончания замещения раньше даты начала");

        if (!string.IsNullOrWhiteSpace(r.Inn))
        {
            var inn = r.Inn.Trim();
            if (inn.Length != 14 || !inn.All(char.IsDigit))
                throw new InvalidOperationException("ИНН должен содержать 14 цифр");
        }
    }

    private static void Apply(SubstitutionRequest e, SubstitutionSaveRequest r)
    {
        e.Subject = r.Subject.Trim();
        e.Reason = r.Reason;

        e.AbsentUserId = r.AbsentUserId;
        e.AbsentName = r.AbsentName.Trim();
        e.AbsentPosition = Trim(r.AbsentPosition);
        e.AbsentBranch = Trim(r.AbsentBranch);
        e.AbsentUnitId = r.AbsentUnitId;

        e.SubstituteUserId = r.SubstituteUserId;
        e.SubstituteName = r.SubstituteName.Trim();
        e.SubstitutePosition = Trim(r.SubstitutePosition);
        e.SubstituteBranch = Trim(r.SubstituteBranch);
        e.SubstituteUnitId = r.SubstituteUnitId;
        e.PassportSeriesNumber = Trim(r.PassportSeriesNumber);
        e.PassportIssuedBy = Trim(r.PassportIssuedBy);
        e.Inn = Trim(r.Inn);
        e.PassportIssuedOn = r.PassportIssuedOn;
        e.PassportValidUntil = r.PassportValidUntil;
        e.AddressRegistration = Trim(r.AddressRegistration);
        e.AddressResidence = Trim(r.AddressResidence);

        // Число дней: если не задано, считаем по датам (включительно).
        e.DaysCount = r.DaysCount
            ?? (r.StartsOn is { } s && r.EndsOn is { } en ? en.DayNumber - s.DayNumber + 1 : null);
        e.StartsOn = r.StartsOn;
        e.EndsOn = r.EndsOn;

        e.CommissionChairUserId = r.CommissionChairUserId;
        e.CommissionChairName = Trim(r.CommissionChairName);
        e.CommissionChairPosition = Trim(r.CommissionChairPosition);
        e.HandoverMoment = r.HandoverMoment;
        e.HandoverOn = r.HandoverOn;

        e.Description = Trim(r.Description);

        e.CommissionMembers.Clear();
        var order = 0;
        foreach (var m in r.CommissionMembers.Where(m => !string.IsNullOrWhiteSpace(m.FullName)))
            e.CommissionMembers.Add(new SubstitutionCommissionMember
            {
                UserId = m.UserId,
                FullName = m.FullName.Trim(),
                Position = Trim(m.Position),
                SortOrder = order++,
            });
    }

    private static SubstitutionListItem ToListItem(SubstitutionRequest x) => new()
    {
        Id = x.Id,
        RegNumber = x.RegNumber,
        Status = x.Status.ToString(),
        StatusTitle = StatusTitle(x.Status),
        Subject = x.Subject,
        ReasonTitle = ReasonTitle(x.Reason),
        AbsentName = x.AbsentName,
        SubstituteName = x.SubstituteName,
        StartsOn = x.StartsOn,
        EndsOn = x.EndsOn,
        InitiatorName = x.InitiatorUser == null ? null : x.InitiatorUser.FullName,
        AuthorUserId = x.AuthorUserId,
        AuthorName = x.AuthorUser == null ? null : x.AuthorUser.FullName,
        CreatedAt = x.CreatedAt,
    };

    private static SubstitutionDetails ToDetails(SubstitutionRequest x)
    {
        var d = new SubstitutionDetails
        {
            Id = x.Id,
            RegNumber = x.RegNumber,
            Status = x.Status.ToString(),
            StatusTitle = StatusTitle(x.Status),
            Subject = x.Subject,
            ReasonCode = x.Reason.ToString(),
            ReasonTitle = ReasonTitle(x.Reason),
            InitiatorUserId = x.InitiatorUserId,
            InitiatorName = x.InitiatorUser?.FullName,
            AuthorUserId = x.AuthorUserId,
            AuthorName = x.AuthorUser?.FullName,
            CreatedAt = x.CreatedAt,
            AbsentUserId = x.AbsentUserId,
            AbsentName = x.AbsentName,
            AbsentPosition = x.AbsentPosition,
            AbsentBranch = x.AbsentBranch,
            AbsentUnitId = x.AbsentUnitId,
            AbsentUnit = x.AbsentUnit?.TitleRu,
            SubstituteUserId = x.SubstituteUserId,
            SubstituteName = x.SubstituteName,
            SubstitutePosition = x.SubstitutePosition,
            SubstituteBranch = x.SubstituteBranch,
            SubstituteUnitId = x.SubstituteUnitId,
            SubstituteUnit = x.SubstituteUnit?.TitleRu,
            PassportSeriesNumber = x.PassportSeriesNumber,
            PassportIssuedBy = x.PassportIssuedBy,
            Inn = x.Inn,
            PassportIssuedOn = x.PassportIssuedOn,
            PassportValidUntil = x.PassportValidUntil,
            AddressRegistration = x.AddressRegistration,
            AddressResidence = x.AddressResidence,
            DaysCount = x.DaysCount,
            StartsOn = x.StartsOn,
            EndsOn = x.EndsOn,
            CommissionChairUserId = x.CommissionChairUserId,
            CommissionChairName = x.CommissionChairName,
            CommissionChairPosition = x.CommissionChairPosition,
            HandoverMoment = x.HandoverMoment.ToString(),
            HandoverOn = x.HandoverOn,
            Description = x.Description,
            RouteNote = x.RouteNote,
            CommissionMembers = x.CommissionMembers
                .OrderBy(m => m.SortOrder)
                .Select(m => new CommissionMemberDto { UserId = m.UserId, FullName = m.FullName, Position = m.Position })
                .ToList(),
            Approvals = x.Approvals
                .OrderBy(a => a.Round).ThenBy(a => a.Order)
                .Select(a => new ApprovalStepDto
                {
                    Id = a.Id,
                    Round = a.Round,
                    DecidedByUserId = a.DecidedByUserId,
                    Order = a.Order,
                    RoleLabel = a.RoleLabel,
                    UserId = a.UserId,
                    UserName = a.User != null ? a.User.FullName : null,
                    State = a.State.ToString(),
                    Comment = a.Comment,
                    DecidedAt = a.DecidedAt,
                })
                .ToList(),
        };

        // КСЗ-20: паспорт замещающего истекает раньше окончания замещения.
        d.PassportExpiresBeforeEnd = x.PassportValidUntil is { } pv && x.EndsOn is { } en && pv < en;
        return d;
    }

    public static string StatusTitle(SubstitutionStatus s) => s switch
    {
        SubstitutionStatus.Draft => "Черновик",
        SubstitutionStatus.OnApproval => "На согласовании",
        // Этап после согласования: приказ и договор подписывают директор, отсутствующий и
        // замещающий; УЧР отмечает «Исполнено» (в том числе без подписей — с бумажными формами).
        SubstitutionStatus.OnExecution => "На подписании",
        SubstitutionStatus.Executed => "Исполнено",
        SubstitutionStatus.Rejected => "Отклонено",
        SubstitutionStatus.Withdrawn => "Отозвано",
        _ => s.ToString(),
    };

    public static string ReasonTitle(SubstitutionReason r) => r switch
    {
        SubstitutionReason.Sick => "Больничный",
        SubstitutionReason.Vacation => "Отпуск",
        SubstitutionReason.Dismissal => "Увольнение",
        _ => "Другое",
    };

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
