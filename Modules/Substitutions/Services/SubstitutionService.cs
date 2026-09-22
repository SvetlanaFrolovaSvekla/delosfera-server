using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Models;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Substitutions.DTO;
using delosfera_server.Modules.Substitutions.Models;

namespace delosfera_server.Modules.Substitutions.Services;

public interface ISubstitutionService
{
    Task<SubstitutionPage> SearchAsync(string? query, string? status, bool mineOnly, int currentUserId, int page, int pageSize, CancellationToken ct = default);
    Task<SubstitutionDetails?> GetAsync(int id, CancellationToken ct = default);
    Task<SubstitutionDetails> CreateAsync(SubstitutionSaveRequest request, int actorUserId, CancellationToken ct = default);
    Task<SubstitutionDetails> UpdateAsync(int id, SubstitutionSaveRequest request, int actorUserId, bool isHrEditor = false, CancellationToken ct = default);
    Task<SubstitutionDetails> SubmitAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default);
    Task<SubstitutionDetails> ApproveAsync(int id, int actorUserId, string? comment, CancellationToken ct = default);
    Task<SubstitutionDetails> RejectAsync(int id, int actorUserId, string? comment, CancellationToken ct = default);
    Task<SubstitutionDetails> ExecuteAsync(int id, int actorUserId, CancellationToken ct = default);
    Task<SubstitutionDetails> WithdrawAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default);
    Task DeleteAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default);

    /// <summary>Печатная форма: form = "order" (приказ) или "liability" (договор МО).</summary>
    Task<(byte[] Bytes, string FileName)> PrintAsync(int id, string form, CancellationToken ct = default);

    /// <summary>Норматив срока согласования (ЗМ-SLA), рабочих дней на этап.</summary>
    Task<int> GetSlaDaysAsync(CancellationToken ct = default);

    /// <summary>Задать норматив срока согласования (ЗМ-SLA). Ведёт администратор.</summary>
    Task<int> SetSlaDaysAsync(int days, CancellationToken ct = default);
}

/// <summary>
/// Заявки на замещение (КСЗ-В9). Инициатор заполняет карточку с комиссией приёма-передачи,
/// заявка идёт в УЧР на исполнение (приказ на время замещения).
/// </summary>
public class SubstitutionService : ISubstitutionService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly INumeratorService _numerator;
    private readonly IBankClock _clock;
    private readonly ISubstitutionPrintService _print;

    public SubstitutionService(DelosferaDbContext db, IAuditService audit, INumeratorService numerator,
        IBankClock clock, ISubstitutionPrintService print)
    {
        _db = db;
        _audit = audit;
        _numerator = numerator;
        _clock = clock;
        _print = print;
    }

    public async Task<(byte[] Bytes, string FileName)> PrintAsync(int id, string form, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests
            .Include(x => x.CommissionMembers)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

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
            .Include(x => x.AbsentUnit)
            .Include(x => x.SubstituteUnit)
            .Include(x => x.CommissionMembers.OrderBy(m => m.SortOrder))
            .Include(x => x.Approvals).ThenInclude(a => a.User);

    public async Task<SubstitutionPage> SearchAsync(
        string? query, string? status, bool mineOnly, int currentUserId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.SubstitutionRequests.AsNoTracking().Include(x => x.InitiatorUser).AsQueryable();

        if (mineOnly)
            q = q.Where(x => x.InitiatorUserId == currentUserId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SubstitutionStatus>(status, out var st))
            q = q.Where(x => x.Status == st);

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

    public async Task<SubstitutionDetails?> GetAsync(int id, CancellationToken ct = default)
    {
        var x = await BaseQuery().FirstOrDefaultAsync(r => r.Id == id, ct);
        return x is null ? null : ToDetails(x);
    }

    public async Task<SubstitutionDetails> CreateAsync(SubstitutionSaveRequest request, int actorUserId, CancellationToken ct = default)
    {
        Validate(request);

        var now = DateTime.UtcNow;
        var entity = new SubstitutionRequest
        {
            Status = SubstitutionStatus.Draft,
            InitiatorUserId = request.InitiatorUserId ?? actorUserId,
            Year = _clock.Today.Year,
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(entity, request);

        _db.SubstitutionRequests.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Substitution", entity.Id, "Created", actorUserId);

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

        var editableByInitiator = isInitiator
            && entity.Status is SubstitutionStatus.Draft or SubstitutionStatus.Rejected;

        // УЧР правит заявку после согласования Начальником операционного управления —
        // то есть когда этап «Операционное управление» уже пройден (Approved).
        var opsApproved = entity.Approvals.Any(a =>
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
        await _audit.LogAsync("Substitution", id, editableByHr ? "UpdatedByHr" : "Updated", actorUserId,
            new { byHr = editableByHr, changes });

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
    };

    public async Task<SubstitutionDetails> SubmitAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        // Отправить черновик в УЧР может только его инициатор (или администратор).
        // Иначе посторонний присваивал бы рег.номер чужой заявке по {id}.
        if (entity.InitiatorUserId != actorUserId && !isPrivileged)
            throw new UnauthorizedAccessException("Отправить заявку может только инициатор");

        if (entity.Status is not (SubstitutionStatus.Draft or SubstitutionStatus.Rejected))
            throw new InvalidOperationException("Отправить можно только черновик или отклонённую заявку");

        if (string.IsNullOrWhiteSpace(entity.SubstituteName) || entity.StartsOn is null || entity.EndsOn is null)
            throw new InvalidOperationException("Заполните замещающего и период замещения");

        // Сквозная нумерация HR-1, HR-2, … без сброса по годам (scope Global).
        // Формат и текущий счётчик правятся в настройках (SubstitutionNumberingController).
        if (string.IsNullOrWhiteSpace(entity.RegNumber))
            entity.RegNumber = await _numerator.NextAsync(
                DocumentType.Custom, "Substitution", "Global", "HR-{seq}");

        // Маршрут согласования: директор филиала → Операционное управление → УЧР.
        // Повторная отправка (после отклонения) пересобирает маршрут заново.
        _db.SubstitutionApprovals.RemoveRange(
            await _db.SubstitutionApprovals.Where(a => a.RequestId == id).ToListAsync(ct));
        var steps = await BuildApprovalRouteAsync(entity, ct);

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
        await _audit.LogAsync("Substitution", id, "Submitted", actorUserId,
            new { regNumber = entity.RegNumber, steps = steps.Count });

        return (await GetAsync(id, ct))!;
    }

    public async Task<SubstitutionDetails> ExecuteAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        if (entity.Status != SubstitutionStatus.OnExecution)
            throw new InvalidOperationException("Исполнить можно только заявку на исполнении");

        entity.Status = SubstitutionStatus.Executed;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Substitution", id, "Executed", actorUserId);

        return (await GetAsync(id, ct))!;
    }

    // ── Маршрут согласования: директор филиала → Операционное управление → УЧР ──

    // Операционное управление согласует Касымов К.Т.; при его отсутствии — Ермакова Ю.А.
    // Идентификаторы вынесены в HrRoutingSettings (ЗМ-Настр); константы — только крайний
    // fallback, если строка настроек ещё не заполнена, чтобы маршрут не сломался.
    private const int OperationsApproverId = 141;   // Касымов Кубатбек (Операционное управление)
    private const int OperationsFallbackId = 563;   // Ермакова Юлия (замена при отсутствии)

    private async Task<List<SubstitutionApproval>> BuildApprovalRouteAsync(SubstitutionRequest entity, CancellationToken ct)
    {
        var steps = new List<SubstitutionApproval>();
        var order = 1;

        var directorId = await ResolveBranchDirectorAsync(entity.AbsentUnitId, ct);
        if (directorId is { } dir)
            steps.Add(new SubstitutionApproval { RequestId = entity.Id, Order = order++, RoleLabel = "Директор филиала", UserId = dir });

        var opsId = await ResolveOperationsApproverAsync(ct);
        if (opsId is { } ops)
            steps.Add(new SubstitutionApproval { RequestId = entity.Id, Order = order++, RoleLabel = "Операционное управление", UserId = ops });

        var hrId = await ResolveHrOfficerAsync(entity.AbsentUnitId, ct);
        if (hrId is { } hr)
            steps.Add(new SubstitutionApproval { RequestId = entity.Id, Order = order++, RoleLabel = "УЧР", UserId = hr });

        return steps;
    }

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

        var step = entity.Approvals.FirstOrDefault(a => a.State == SubstitutionApprovalState.Active)
            ?? throw new InvalidOperationException("Нет активного этапа согласования");
        if (step.UserId != actorUserId)
            throw new InvalidOperationException("Согласовать может только назначенный на этап сотрудник");

        step.State = SubstitutionApprovalState.Approved;
        step.Comment = comment;
        step.DecidedAt = DateTime.UtcNow;
        step.DecidedByUserId = actorUserId;

        var next = entity.Approvals
            .Where(a => a.State == SubstitutionApprovalState.Pending).OrderBy(a => a.Order).FirstOrDefault();
        if (next is not null)
        {
            next.State = SubstitutionApprovalState.Active;
            next.ActivatedAt = DateTime.UtcNow;   // отсчёт SLA следующего этапа (ЗМ-SLA)
        }
        else
            entity.Status = SubstitutionStatus.OnExecution;   // маршрут пройден — УЧР исполняет

        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Substitution", id, "Approved", actorUserId, new { step = step.RoleLabel });
        return (await GetAsync(id, ct))!;
    }

    public async Task<SubstitutionDetails> RejectAsync(int id, int actorUserId, string? comment, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");
        if (entity.Status != SubstitutionStatus.OnApproval)
            throw new InvalidOperationException("Отклонить можно только заявку на согласовании");

        var step = entity.Approvals.FirstOrDefault(a => a.State == SubstitutionApprovalState.Active)
            ?? throw new InvalidOperationException("Нет активного этапа согласования");
        if (step.UserId != actorUserId)
            throw new InvalidOperationException("Отклонить может только назначенный на этап сотрудник");

        step.State = SubstitutionApprovalState.Rejected;
        step.Comment = comment;
        step.DecidedAt = DateTime.UtcNow;
        step.DecidedByUserId = actorUserId;
        entity.Status = SubstitutionStatus.Rejected;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Substitution", id, "Rejected", actorUserId, new { step = step.RoleLabel });
        return (await GetAsync(id, ct))!;
    }

    public async Task<SubstitutionDetails> WithdrawAsync(int id, int actorUserId, bool isPrivileged = false, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        // Отозвать заявку из работы вправе только её инициатор (или администратор).
        if (entity.InitiatorUserId != actorUserId && !isPrivileged)
            throw new UnauthorizedAccessException("Отозвать заявку может только инициатор");

        if (entity.Status is SubstitutionStatus.Executed or SubstitutionStatus.Withdrawn)
            throw new InvalidOperationException("Заявка уже завершена");

        entity.Status = SubstitutionStatus.Withdrawn;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Substitution", id, "Withdrawn", actorUserId);

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
        await _audit.LogAsync("Substitution", id, "Deleted", actorUserId);
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
            CommissionMembers = x.CommissionMembers
                .OrderBy(m => m.SortOrder)
                .Select(m => new CommissionMemberDto { UserId = m.UserId, FullName = m.FullName, Position = m.Position })
                .ToList(),
            Approvals = x.Approvals
                .OrderBy(a => a.Order)
                .Select(a => new ApprovalStepDto
                {
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
        SubstitutionStatus.OnExecution => "На исполнении",
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
