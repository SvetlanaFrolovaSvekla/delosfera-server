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
    Task<SubstitutionDetails> UpdateAsync(int id, SubstitutionSaveRequest request, int actorUserId, CancellationToken ct = default);
    Task<SubstitutionDetails> SubmitAsync(int id, int actorUserId, CancellationToken ct = default);
    Task<SubstitutionDetails> ExecuteAsync(int id, int actorUserId, CancellationToken ct = default);
    Task<SubstitutionDetails> WithdrawAsync(int id, int actorUserId, CancellationToken ct = default);
    Task DeleteAsync(int id, int actorUserId, CancellationToken ct = default);

    /// <summary>Печатная форма: form = "order" (приказ) или "liability" (договор МО).</summary>
    Task<(byte[] Bytes, string FileName)> PrintAsync(int id, string form, CancellationToken ct = default);
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
            _ => throw new InvalidOperationException("Неизвестная форма печати"),
        };
    }

    private IQueryable<SubstitutionRequest> BaseQuery() =>
        _db.SubstitutionRequests.AsNoTracking()
            .Include(x => x.InitiatorUser)
            .Include(x => x.AbsentUnit)
            .Include(x => x.SubstituteUnit)
            .Include(x => x.CommissionMembers.OrderBy(m => m.SortOrder));

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

    public async Task<SubstitutionDetails> UpdateAsync(int id, SubstitutionSaveRequest request, int actorUserId, CancellationToken ct = default)
    {
        Validate(request);

        var entity = await _db.SubstitutionRequests.Include(x => x.CommissionMembers)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        if (entity.Status is not (SubstitutionStatus.Draft or SubstitutionStatus.Rejected))
            throw new InvalidOperationException("Изменять можно только черновик или отклонённую заявку");

        Apply(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Substitution", id, "Updated", actorUserId);

        return (await GetAsync(id, ct))!;
    }

    public async Task<SubstitutionDetails> SubmitAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        if (entity.Status is not (SubstitutionStatus.Draft or SubstitutionStatus.Rejected))
            throw new InvalidOperationException("Отправить можно только черновик или отклонённую заявку");

        if (string.IsNullOrWhiteSpace(entity.SubstituteName) || entity.StartsOn is null || entity.EndsOn is null)
            throw new InvalidOperationException("Заполните замещающего и период замещения");

        if (string.IsNullOrWhiteSpace(entity.RegNumber))
            entity.RegNumber = await _numerator.NextAsync(
                DocumentType.Custom, "Substitution", entity.Year.ToString(), "ЗМ-{seq}/{year}");

        // По ТР §5.4 заявка после формирования направляется в УЧР на исполнение.
        entity.Status = SubstitutionStatus.OnExecution;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Substitution", id, "Submitted", actorUserId, new { regNumber = entity.RegNumber });

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

    public async Task<SubstitutionDetails> WithdrawAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        if (entity.Status is SubstitutionStatus.Executed or SubstitutionStatus.Withdrawn)
            throw new InvalidOperationException("Заявка уже завершена");

        entity.Status = SubstitutionStatus.Withdrawn;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Substitution", id, "Withdrawn", actorUserId);

        return (await GetAsync(id, ct))!;
    }

    public async Task DeleteAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var entity = await _db.SubstitutionRequests.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        if (entity.Status != SubstitutionStatus.Draft)
            throw new InvalidOperationException("Удалить можно только черновик");

        _db.SubstitutionRequests.Remove(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("Substitution", id, "Deleted", actorUserId);
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
