using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.ActivityLog.Models;
using delosfera_server.Modules.ActivityLog.Services;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Integrations.Mail;
using delosfera_server.Modules.Users.Models;
using Microsoft.EntityFrameworkCore;
using ActivityText = delosfera_server.Modules.ActivityLog.Models.ActivityText;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Когда Титульный лист пересобирается.</summary>
public enum TitlePageRefreshMode
{
    /// <summary>Всегда — кнопка «Сформировать» / «Сформировать заново» (в т.ч. поверх загруженного
    /// вручную файла).</summary>
    Force = 0,

    /// <summary>Новая редакция консолидирована (стала действующей): формируем, если титульного листа
    /// ещё нет или он сформирован системой; загруженный вручную не трогаем.</summary>
    CreateOrRefreshAuto = 1,

    /// <summary>Изменились данные уже существующего титульного листа (решение по редакции,
    /// архивация, правка реквизитов): пересобираем ТОЛЬКО ранее сформированный системой лист. Старые
    /// ВНД без титульного листа сами по себе его не получают — только по кнопке.</summary>
    RefreshAutoOnly = 2,
}

public interface IVndTitlePageService
{
    /// <summary>Фиксирует в журнале решений (VndRedactionDecision) консолидацию редакции — вызывать в
    /// VndActualizationService.PublishAsync ДО того, как дата/№ принятия редакции перезаписываются
    /// значениями из формы консолидации. Первая консолидация редакции — её утверждение; повторная
    /// (актуализация без изменений) — новая строка «Внесены изменения, дополнения», а исходный
    /// протокол утверждения редакции сохраняется в журнале. В БД не сохраняет — только добавляет в
    /// контекст. Возвращает true, если это утверждение НОВОЙ редакции.</summary>
    Task<bool> RecordConsolidationAsync(VndDocument vnd, VndRedaction redaction, DateOnly adoptionDate,
        string adoptionCode, DateOnly effectiveDate, int actorUserId);

    /// <summary>Правка даты/№ принятия и даты вступления в силу на вкладке «Реквизиты» — исправляет
    /// соответствующее решение в журнале (то, что совпадало со старыми значениями реквизитов), чтобы
    /// титульный лист показывал исправленные данные. В БД не сохраняет.</summary>
    Task SyncAfterRequisitesEditAsync(int redactionId,
        (DateOnly? Date, string? Code, DateOnly? Effective) before,
        (DateOnly? Date, string? Code, DateOnly? Effective) after);

    /// <summary>Пересобирает титульный лист редакции по режиму <paramref name="mode"/> и сохраняет.
    /// Возвращает true, если лист был сформирован.</summary>
    Task<bool> RefreshAsync(int redactionId, TitlePageRefreshMode mode, int userId);

    /// <summary>То же, что RefreshAsync, но для нескольких редакций и без исключений наружу: титульный
    /// лист — вспомогательный документ, сбой его генерации не должен срывать консолидацию,
    /// архивацию или сохранение реквизитов (тот же принцип, что у Листа согласования).</summary>
    Task RefreshQuietlyAsync(IEnumerable<int> redactionIds, TitlePageRefreshMode mode, int userId);

    /// <summary>Кнопка «Сформировать» на карточке ВНД (специальные вложения редакции).</summary>
    Task GenerateAsync(int vndId, int redactionId, int userId);

    /// <summary>Загрузка своего файла вместо сформированного системой.</summary>
    Task UploadAsync(int vndId, int redactionId, IFormFile file, int userId);

    /// <summary>Убрать титульный лист у редакции.</summary>
    Task RemoveAsync(int vndId, int redactionId, int userId);
}

/// <summary>Титульный лист редакции ВНД («шапка» с реквизитами текущей и предыдущей редакции) —
/// см. VndRedaction.TitlePageFileId. Собирает данные из реквизитов редакций, журнала решений
/// (VndRedactionDecision) и данных отмены/архивации документа; сам .docx строит TitlePageGenerator.
///
/// Правила заполнения колонки для редакции Рn:
/// • реквизиты (наименование, орган, разработчик, код, исполнители) — реквизиты самой Рn;
/// • «Дата и № протокола утверждения», «Дата вступления в силу» — утверждение Рn (первое решение
///   вида Adoption; для редакций до появления журнала — реквизиты редакции);
/// • «Разработчик (ФИО, должность)» — кто консолидировал Рn (снимок ФИО и должности);
/// • «Внесены изменения, дополнения» — все решения по документу от Р1 до Рn включительно
///   (утверждения редакций и актуализации без изменений), «Разработчик» блока — автор последнего;
/// • «Отменено действие» — заполняется у последней действующей редакции архивированного ВНД.
/// Колонка «Предыдущая редакция» — то же самое для Р(n-1); у Р1 она пустая.</summary>
public class VndTitlePageService : IVndTitlePageService
{
    private const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly DelosferaDbContext _db;
    private readonly ITitlePageGenerator _generator;
    private readonly IFileStorageService _fileService;
    private readonly ICurrentUserService _currentUser;
    private readonly IActivityLogService _activityLog;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VndTitlePageService> _logger;

    public VndTitlePageService(
        DelosferaDbContext db,
        ITitlePageGenerator generator,
        IFileStorageService fileService,
        ICurrentUserService currentUser,
        IActivityLogService activityLog,
        IConfiguration configuration,
        ILogger<VndTitlePageService> logger)
    {
        _db = db;
        _generator = generator;
        _fileService = fileService;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _configuration = configuration;
        _logger = logger;
    }

    // ───────────────────────── Журнал решений ─────────────────────────

    public async Task<bool> RecordConsolidationAsync(VndDocument vnd, VndRedaction redaction,
        DateOnly adoptionDate, string adoptionCode, DateOnly effectiveDate, int actorUserId)
    {
        var existing = await _db.Set<VndRedactionDecision>()
            .Where(d => d.VndRedactionId == redaction.Id)
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .ToListAsync();

        var (actorName, actorPosition) = await ActorSnapshotAsync(actorUserId);
        var now = DateTime.UtcNow;

        var hasAdoption = existing.Any(d => d.Kind == VndRedactionDecisionKind.Adoption);
        var hadLegacyAdoption = redaction.AdoptionDate is not null || !string.IsNullOrWhiteSpace(redaction.AdoptionCode);

        // Повторная консолидация уже утверждённой редакции: либо по журналу (утверждение в нём уже
        // есть), либо — для редакций, утверждённых до появления журнала, — цикл актуализации
        // «без изменений» по уже действующей редакции с заполненными реквизитами утверждения.
        var isReAdoption = hasAdoption || (vnd.ActualizationPlannedNoChanges && hadLegacyAdoption);

        if (!isReAdoption)
        {
            _db.Set<VndRedactionDecision>().Add(new VndRedactionDecision
            {
                VndRedactionId = redaction.Id,
                Kind = VndRedactionDecisionKind.Adoption,
                DecisionDate = adoptionDate,
                DecisionCode = adoptionCode.Trim(),
                EffectiveDate = effectiveDate,
                ActorUserId = actorUserId,
                ActorName = actorName,
                ActorPosition = actorPosition,
                CreatedAt = now,
            });
            return true;
        }

        VndRedactionDecision? last = existing.LastOrDefault();
        if (!hasAdoption)
        {
            // Исходное утверждение старой редакции — переносим из её реквизитов, пока они не
            // перезаписаны новым протоколом (иначе оно бы потерялось).
            var (fallbackName, fallbackPosition) = await FallbackDeveloperAsync(vnd, redaction);
            last = new VndRedactionDecision
            {
                VndRedactionId = redaction.Id,
                Kind = VndRedactionDecisionKind.Adoption,
                DecisionDate = redaction.AdoptionDate,
                DecisionCode = redaction.AdoptionCode,
                EffectiveDate = redaction.EffectiveDate,
                ActorName = fallbackName,
                ActorPosition = fallbackPosition,
                CreatedAt = now.AddTicks(-1),
            };
            _db.Set<VndRedactionDecision>().Add(last);
        }

        // Та же дата и тот же № — это не новое решение (например, повторная публикация с теми же
        // данными), строку не дублируем.
        if (last is not null && last.DecisionDate == adoptionDate
            && string.Equals(last.DecisionCode?.Trim(), adoptionCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            last.EffectiveDate = effectiveDate;
            return false;
        }

        _db.Set<VndRedactionDecision>().Add(new VndRedactionDecision
        {
            VndRedactionId = redaction.Id,
            Kind = VndRedactionDecisionKind.NoChangesActualization,
            DecisionDate = adoptionDate,
            DecisionCode = adoptionCode.Trim(),
            EffectiveDate = effectiveDate,
            ActorUserId = actorUserId,
            ActorName = actorName,
            ActorPosition = actorPosition,
            CreatedAt = now,
        });
        return false;
    }

    public async Task SyncAfterRequisitesEditAsync(int redactionId,
        (DateOnly? Date, string? Code, DateOnly? Effective) before,
        (DateOnly? Date, string? Code, DateOnly? Effective) after)
    {
        if (before == after) return;

        var decisions = await _db.Set<VndRedactionDecision>()
            .Where(d => d.VndRedactionId == redactionId)
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .ToListAsync();
        if (decisions.Count == 0) return; // старая редакция без журнала — данные берутся из реквизитов

        // Реквизиты редакции всегда равны ПОСЛЕДНЕМУ решению по ней (PublishAsync записывает в них
        // значения каждой консолидации) — исправляем именно его; если совпадения нет — последнее.
        var target = decisions.LastOrDefault(d =>
                         d.DecisionDate == before.Date
                         && string.Equals(d.DecisionCode?.Trim(), before.Code?.Trim(), StringComparison.OrdinalIgnoreCase))
                     ?? decisions.Last();

        target.DecisionDate = after.Date;
        target.DecisionCode = string.IsNullOrWhiteSpace(after.Code) ? null : after.Code.Trim();
        target.EffectiveDate = after.Effective;
    }

    // ───────────────────────── Формирование ─────────────────────────

    public async Task<bool> RefreshAsync(int redactionId, TitlePageRefreshMode mode, int userId)
    {
        var redaction = await _db.VndRedactions.FirstOrDefaultAsync(r => r.Id == redactionId)
                        ?? throw new KeyNotFoundException($"Редакция с id={redactionId} не найдена");

        var allowed = mode switch
        {
            TitlePageRefreshMode.Force => true,
            TitlePageRefreshMode.CreateOrRefreshAuto => !redaction.TitlePageIsManual || redaction.TitlePageFileId is null,
            TitlePageRefreshMode.RefreshAutoOnly => redaction.TitlePageFileId is not null && !redaction.TitlePageIsManual,
            _ => false,
        };
        if (!allowed) return false;

        var data = await BuildDataAsync(redaction.VndId, redaction.Id);
        var content = _generator.Generate(data);

        var saved = await _fileService.SaveGeneratedAsync(
            content, $"{redaction.Code}_Титульный_лист.docx", WordContentType, userId);

        redaction.TitlePageFileId = saved.Id;
        redaction.TitlePageIsManual = false;
        redaction.TitlePageUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task RefreshQuietlyAsync(IEnumerable<int> redactionIds, TitlePageRefreshMode mode, int userId)
    {
        foreach (var id in redactionIds.Distinct())
        {
            try
            {
                await RefreshAsync(id, mode, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось сформировать Титульный лист для редакции {RedactionId}", id);
            }
        }
    }

    public async Task GenerateAsync(int vndId, int redactionId, int userId)
    {
        var (vnd, redaction) = await LoadForEditAsync(vndId, redactionId);
        await RefreshAsync(redaction.Id, TitlePageRefreshMode.Force, userId);
        await LogAsync(vnd, redaction, userId, "сформировал(а)", "generated", "түздү");
    }

    public async Task UploadAsync(int vndId, int redactionId, IFormFile file, int userId)
    {
        var (vnd, redaction) = await LoadForEditAsync(vndId, redactionId);

        var saved = await _fileService.SaveAsync(file, userId);
        redaction.TitlePageFileId = saved.Id;
        redaction.TitlePageIsManual = true;
        redaction.TitlePageUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogAsync(vnd, redaction, userId, "загрузил(а)", "uploaded", "жүктөдү");
    }

    public async Task RemoveAsync(int vndId, int redactionId, int userId)
    {
        var (vnd, redaction) = await LoadForEditAsync(vndId, redactionId);
        if (redaction.TitlePageFileId is null) return;

        redaction.TitlePageFileId = null;
        redaction.TitlePageIsManual = false;
        redaction.TitlePageUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogAsync(vnd, redaction, userId, "убрал(а)", "removed", "алып салды");
    }

    /// <summary>Кто может вручную сформировать/загрузить/убрать титульный лист редакции:
    /// • главный редактор ВНД (право прямого редактирования редакций или правки маршрута любого
    ///   согласования) — у любого ВНД и любой редакции;
    /// • сотрудник с правом «Редактирование реквизитов ВНД» — лист собирается из реквизитов;
    /// • ответственный за ТЕКУЩИЙ цикл актуализации — только пока ВНД на актуализации/консолидации
    ///   и только у последней редакции (той, что он сейчас готовит). Прежние редакции и чужие ВНД
    ///   ему недоступны.
    /// Широкие права «Создание/Актуализация любых ВНД» сюда намеренно не входят: они есть и у
    /// рядовых редакторов ВНД, и давали бы доступ к титульным листам чужих документов.
    /// Единое правило для сервера и для флага CanManageTitlePage в ответе.</summary>
    public static bool CanManage(ICurrentUserService user, VndDocument vnd, VndRedaction redaction,
        int latestRedactionNumber)
    {
        if (user.HasPermission(PermissionCode.EditLastRevisionDirectly)
            || user.HasPermission(PermissionCode.EditAnyVndApprovalRoute)
            || user.HasPermission(PermissionCode.EditVndRequisites))
            return true;

        return vnd.ActualizationResponsibleUserId == user.UserId
               && vnd.Status is VndStatus.OnActualization or VndStatus.Consolidation
               && redaction.Number == latestRedactionNumber;
    }

    private async Task<(VndDocument Vnd, VndRedaction Redaction)> LoadForEditAsync(int vndId, int redactionId)
    {
        var vnd = await _db.VndDocuments.FirstOrDefaultAsync(x => x.Id == vndId)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");
        var redaction = await _db.VndRedactions.FirstOrDefaultAsync(r => r.Id == redactionId && r.VndId == vndId)
                        ?? throw new KeyNotFoundException($"Редакция с id={redactionId} не найдена");

        var latestNumber = await _db.VndRedactions.Where(r => r.VndId == vndId).MaxAsync(r => r.Number);
        if (!CanManage(_currentUser, vnd, redaction, latestNumber))
            throw new UnauthorizedAccessException(
                "Титульный лист может формировать или загружать главный редактор ВНД, сотрудник с правом " +
                "«Редактирование реквизитов ВНД» или ответственный за текущую актуализацию этого ВНД " +
                "(только для готовящейся редакции)");

        return (vnd, redaction);
    }

    private async Task LogAsync(VndDocument vnd, VndRedaction redaction, int userId, string ru, string en, string kg)
    {
        var actor = await _db.Users.FindAsync(userId);
        var actorName = actor?.FullName ?? "—";
        _activityLog.Log(
            ActivityModules.Vnd, ActivityEventKind.RedactionEdited, vnd.Id, vnd.Code, userId,
            new ActivityText(
                $"{actorName} {ru} Титульный лист редакции {redaction.Code} ВНД «{vnd.TitleRu}»",
                $"{actorName} {en} the title page of revision {redaction.Code} of VND \"{vnd.TitleRu}\"",
                $"{actorName} «{vnd.TitleRu}» ВНДисинин {redaction.Code} редакциясынын титулдук барагын {kg}"),
            $"/base-vnd/{vnd.Id}");
        await _db.SaveChangesAsync();
    }

    // ───────────────────────── Данные листа ─────────────────────────

    private async Task<TitlePageData> BuildDataAsync(int vndId, int redactionId)
    {
        var vnd = await _db.VndDocuments.AsNoTracking().FirstAsync(x => x.Id == vndId);

        var redactions = await _db.VndRedactions.AsNoTracking()
            .Where(r => r.VndId == vndId)
            .Include(r => r.Organ)
            .Include(r => r.ResponsibleExecutors)
            .Include(r => r.Decisions)
            .OrderBy(r => r.Number)
            .ToListAsync();

        var current = redactions.First(r => r.Id == redactionId);
        var previous = redactions
            .Where(r => r.Number < current.Number)
            .OrderByDescending(r => r.Number)
            .FirstOrDefault();

        // Решения по всем редакциям — с подставленным утверждением для старых редакций без журнала.
        var decisionsByRedaction = new Dictionary<int, List<DecisionView>>();
        foreach (var r in redactions)
            decisionsByRedaction[r.Id] = await DecisionsOfAsync(vnd, r);

        string? baseUrl = await BaseUrlAsync();
        string? changesLink = current.TidFileId is not null && baseUrl is not null
            ? $"{baseUrl}/base-vnd/{vnd.Id}"
            : null;

        return new TitlePageData(
            current.TitleRu,
            await ColumnAsync(vnd, current, redactions, decisionsByRedaction),
            previous is null ? null : await ColumnAsync(vnd, previous, redactions, decisionsByRedaction),
            changesLink);
    }

    private sealed record DecisionView(DateOnly? Date, string? Code, DateOnly? Effective, string? Actor,
        VndRedactionDecisionKind Kind);

    private async Task<List<DecisionView>> DecisionsOfAsync(VndDocument vnd, VndRedaction r)
    {
        var list = r.Decisions
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Id)
            .Select(d => new DecisionView(d.DecisionDate, d.DecisionCode, d.EffectiveDate,
                Person(d.ActorName, d.ActorPosition), d.Kind))
            .ToList();

        if (list.Count == 0 && (r.AdoptionDate is not null || !string.IsNullOrWhiteSpace(r.AdoptionCode)))
        {
            var (name, position) = await FallbackDeveloperAsync(vnd, r);
            list.Add(new DecisionView(r.AdoptionDate, r.AdoptionCode, r.EffectiveDate, Person(name, position),
                VndRedactionDecisionKind.Adoption));
        }

        return list;
    }

    private async Task<TitlePageColumn> ColumnAsync(VndDocument vnd, VndRedaction r,
        List<VndRedaction> all, Dictionary<int, List<DecisionView>> decisionsByRedaction)
    {
        var own = decisionsByRedaction[r.Id];
        var adoption = own.FirstOrDefault(d => d.Kind == VndRedactionDecisionKind.Adoption);

        string? developer = adoption?.Actor;
        if (developer is null)
        {
            var (name, position) = await FallbackDeveloperAsync(vnd, r);
            developer = Person(name, position);
        }

        // Решения по документу от первой редакции до этой включительно.
        var history = all
            .Where(x => x.Number <= r.Number)
            .OrderBy(x => x.Number)
            .SelectMany(x => decisionsByRedaction[x.Id])
            .ToList();

        var isCancelled = vnd.Status == VndStatus.Archived && vnd.CurrentRedactionId == r.Id;

        return new TitlePageColumn(
            Title: r.TitleRu,
            Organ: r.Organ?.TitleRu,
            AdoptionDate: adoption?.Date ?? r.AdoptionDate,
            AdoptionCode: adoption?.Code ?? r.AdoptionCode,
            EffectiveDate: adoption?.Effective ?? r.EffectiveDate,
            Developer: developer,
            Code: vnd.Code,
            ResponsibleExecutors: r.ResponsibleExecutors.Select(e => e.TitleRu).ToList(),
            Decisions: history.Select(d => new TitlePageDecision(d.Date, d.Code, d.Effective)).ToList(),
            DecisionsDeveloper: history.LastOrDefault(d => d.Actor is not null)?.Actor ?? developer,
            CancelDecisionDate: isCancelled ? vnd.CancelDate : null,
            CancelDecisionCode: isCancelled ? vnd.CancelCode : null,
            CancelDate: isCancelled ? vnd.ArchivedDate ?? vnd.CancelDate : null);
    }

    /// <summary>"Айбек Нуруев, Риск-менеджер".</summary>
    private static string? Person(string? name, string? position)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return string.IsNullOrWhiteSpace(position) ? name.Trim() : $"{name.Trim()}, {position.Trim()}";
    }

    private async Task<(string? Name, string? Position)> ActorSnapshotAsync(int userId)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(u => u.Position)
            .FirstOrDefaultAsync(u => u.Id == userId);
        return (user?.FullName, user?.Position?.TitleRu);
    }

    /// <summary>Кто «разработчик» редакции, если журнала решений по ней нет (редакции, утверждённые
    /// до его появления): инициатор последнего согласования редакции, для первой редакции без
    /// согласования — автор ВНД, иначе — куратор разработчика из реквизитов редакции.</summary>
    private async Task<(string? Name, string? Position)> FallbackDeveloperAsync(VndDocument vnd, VndRedaction r)
    {
        var initiatorId = await _db.VndApprovalProcesses.AsNoTracking()
            .Where(p => p.RedactionId == r.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => (int?)p.InitiatorUserId)
            .FirstOrDefaultAsync();

        var userId = initiatorId ?? (r.Number == 1 ? vnd.CreatedByUserId : null) ?? r.CuratorDeveloperId;
        return userId is null ? (null, null) : await ActorSnapshotAsync(userId.Value);
    }

    /// <summary>Адрес системы для ссылки с заголовка «Внесены изменения, дополнения» — тот же, что и
    /// для ссылок в письмах (настройки почты, раздел Mail конфигурации как запасной вариант).</summary>
    private async Task<string?> BaseUrlAsync()
    {
        var fromDb = await _db.Set<MailSettings>().AsNoTracking()
            .Select(s => s.BaseUrl)
            .FirstOrDefaultAsync();
        var url = !string.IsNullOrWhiteSpace(fromDb) ? fromDb : _configuration["Mail:BaseUrl"];
        return string.IsNullOrWhiteSpace(url) ? null : url.Trim().TrimEnd('/');
    }
}
