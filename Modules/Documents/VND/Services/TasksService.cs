using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public class TasksService : ITasksService
{
    private readonly DelosferaDbContext _db;

    public TasksService(DelosferaDbContext db)
    {
        _db = db;
    }

    /// <summary>Задачи вкладки "Ждущие моего согласования" — все три фазы, на которых
    /// решение сейчас за текущим пользователем: первичное, повторное согласование и
    /// финальная выдержка. Раньше финальная выдержка сюда не попадала — по переходу в этот
    /// статус согласующим уходило только уведомление (см. WorkflowNotifier/VndApprovalService),
    /// а сама задача в "Мои задачи" не появлялась.</summary>
    public async Task<List<VndTaskResponse>> GetCoordinationTasksAsync(int userId)
    {
        var stages = await _db.Set<VndApprovalStage>()
            .Include(s => s.ApprovalProcess).ThenInclude(p => p!.Vnd)
            .Include(s => s.ApprovalProcess).ThenInclude(p => p!.Redaction)
            .Where(s => s.ApproverUserId == userId)
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

        var initiators = await GetInitiatorNamesAsync(stages.Select(s => s.ApprovalProcess!.InitiatorUserId));

        return stages.Select(s =>
        {
            var process = s.ApprovalProcess!;
            var phase = MapProcessPhase(process.Status);

            return new VndTaskResponse
            {
                VndId = process.VndId,
                VndCode = process.Vnd!.Code,
                VndTitle = process.Vnd!.TitleRu,
                Scope = "coordination",
                VndStatus = MapVndStatus(process.Vnd!.Status),
                RedactionId = process.RedactionId,
                RedactionCode = process.Redaction!.Code,
                StageId = s.Id,
                StagePhase = phase,
                StageKind = MapStageKind(s.Kind),
                StageTitle = s.Title ?? MapLegacyStageTitle(s.Kind),
                DeadlineAt = phase switch
                {
                    "primary" => process.PrimaryDeadlineAt,
                    "repeat" => process.RepeatDeadlineAt,
                    "final" => process.FinalHoldDeadlineAt,
                    _ => null
                },
                InitiatorName = initiators.GetValueOrDefault(process.InitiatorUserId, "—"),
                DeadlineMinutes = phase switch
                {
                    "primary" => process.PrimaryDeadlineMinutes,
                    "repeat" => process.RepeatDeadlineMinutes,
                    "final" => process.FinalHoldDeadlineMinutes,
                    _ => null
                },
                InitiatorComment = phase == "primary" ? null : process.RepeatInitiatorComment,
                ActualizationPlannedNoChanges = process.Vnd!.ActualizationPlannedNoChanges,
                CreatedAt = phase switch
                {
                    "primary" => process.PrimaryStartedAt,
                    "repeat" => process.RepeatStartedAt ?? process.CreatedAt,
                    "final" => process.FinalHoldStartedAt ?? process.CreatedAt,
                    _ => process.CreatedAt
                }
            };
        })
        .OrderBy(t => t.DeadlineAt)
        .ToList();
    }

    /// <summary>Видит ответственный за актуализацию этого конкретного цикла
    /// (VndDocument.ActualizationResponsibleUserId) — раньше здесь ошибочно фильтровалось по
    /// CreatedByUserId (создателю ВНД), из-за чего назначенный ответственный (если это не он
    /// сам создавал документ) вообще не видел задачу о необходимости актуализировать.
    ///
    /// Исключает документы с уже пройденным шагом "Выполнить актуализацию"
    /// (ActualizationPerformed == true) — для ответственного его собственная часть работы там
    /// уже сделана, и такие карточки теперь показываются в GetActualizationDoneTasksAsync,
    /// а не одновременно висят и здесь, и там.</summary>
    public async Task<List<VndTaskResponse>> GetActualizationTasksAsync(int userId)
    {
        var openVndIds = await GetOpenActualizationVndIdsAsync();
        if (openVndIds.Count == 0) return new List<VndTaskResponse>();

        // Статус тоже фильтруем, а не только "цикл ещё не опубликован" (PublishedAt == null) —
        // иначе документ, дошедший до Consolidation в рамках того же открытого цикла, продолжает
        // висеть здесь ОДНОВРЕМЕННО с задачей в GetConsolidationTasksAsync: пользователь видит
        // два "дубликата" одной и той же работы, причём актуализационная карточка выглядит
        // "свежее" из-за собственной сортировки/CreatedAt, хотя по факту документ уже ушёл дальше.
        // Если последний процесс согласования по документу закончился отклонением - это уже
        // отдельная, более конкретная задача во вкладке "Отклонено" (см.
        // GetRejectedTasksAsync). Не дублируем её здесь безликим "На актуализации" - тем же
        // приёмом, каким выше исключается пересечение с GetConsolidationTasksAsync.
        var rejectedVndIds = (await GetLatestRejectedProcessesAsync()).Select(p => p.VndId).ToHashSet();

        var docs = await _db.VndDocuments
            .Where(x => openVndIds.Contains(x.Id)
                        && x.ActualizationResponsibleUserId == userId
                        && x.Status == VndStatus.OnActualization
                        && !x.ActualizationPerformed
                        && !rejectedVndIds.Contains(x.Id))
            .ToListAsync();

        return docs.Select(x => new VndTaskResponse
            {
                VndId = x.Id,
                VndCode = x.Code,
                VndTitle = x.TitleRu,
                Scope = "actualization",
                VndStatus = MapVndStatus(x.Status),
                DueActualizationDate = x.DueActualizationDate,
                ActualizationPlannedNoChanges = x.ActualizationPlannedNoChanges,
                ActualizationPerformed = x.ActualizationPerformed,
                CreatedAt = x.UpdatedAt
            })
            .OrderBy(t => t.DueActualizationDate)
            .ToList();
    }

    /// <summary>Видит ответственный за актуализацию этого цикла
    /// (VndDocument.ActualizationResponsibleUserId) — та же поправка, что и для актуализации.
    /// Если консолидация не связана с циклом актуализации (обычное согласование первой редакции,
    /// ActualizationResponsibleUserId == null), задачу по-прежнему видит инициатор.</summary>
    public async Task<List<VndTaskResponse>> GetConsolidationTasksAsync(int userId)
    {
        // Кому показываем задачу — ровно тем, кто может её выполнить. Правило
        // одно на всю консолидацию и живёт в VndActualizationService.PublishAsync:
        //
        //   есть цикл актуализации → назначенный ответственный;
        //   цикла нет             → инициатор согласования последней редакции.
        //
        // Раньше здесь во втором случае стоял создатель документа. Создатель
        // и инициатор согласования — разные люди, и задача приходила одному,
        // а выполнить её мог другой: в списке она висела, а на карточке кнопки
        // не было. Именно на это пожаловалась Эсенова 24 августа.
        //
        // Главного редактора здесь нет намеренно: он может опубликовать любой
        // документ, и если добавить его сюда, ему в задачи посыпались бы все
        // консолидации банка, а не его собственные.
        var candidates = await _db.VndDocuments
            .Where(x => x.Status == VndStatus.Consolidation)
            .Select(x => new
            {
                Doc = x,
                InitiatorUserId = x.Redactions
                    .OrderByDescending(r => r.Number)
                    .Take(1)
                    .SelectMany(r => _db.VndApprovalProcesses
                        .Where(p => p.RedactionId == r.Id)
                        .Select(p => (int?)p.InitiatorUserId))
                    .FirstOrDefault(),
            })
            .ToListAsync();

        var docs = candidates
            .Where(x => x.Doc.ActualizationResponsibleUserId == userId
                        || (x.Doc.ActualizationResponsibleUserId == null
                            && x.InitiatorUserId == userId))
            .Select(x => x.Doc)
            .ToList();

        if (docs.Count == 0) return new List<VndTaskResponse>();

        return docs.Select(x => new VndTaskResponse
            {
                VndId = x.Id,
                VndCode = x.Code,
                VndTitle = x.TitleRu,
                Scope = "consolidation",
                VndStatus = MapVndStatus(x.Status),
                StatusLabel = "В процессе консолидации",
                DueActualizationDate = x.DueActualizationDate,
                ActualizationPlannedNoChanges = x.ActualizationPlannedNoChanges,
                ActualizationPerformed = x.ActualizationPerformed,
                CreatedAt = x.UpdatedAt
            })
            .OrderBy(t => t.DueActualizationDate)
            .ToList();
    }

    public async Task<List<VndTaskResponse>> GetMyVndApprovalTasksAsync(int userId)
    {
        var vnds = await _db.VndDocuments
            .Where(x => x.Status == VndStatus.Review)
            .ToListAsync();

        if (vnds.Count == 0) return new List<VndTaskResponse>();

        var vndIds = vnds.Select(x => x.Id).ToList();
        var processByVndId = await GetCurrentApprovalProcessesByVndIdAsync(vndIds);
        var openActualizationVndIds = await GetOpenActualizationVndIdsAsync(vndIds);

        var result = new List<VndTaskResponse>();
        foreach (var vnd in vnds)
        {
            if (!processByVndId.TryGetValue(vnd.Id, out var process)) continue;

            var isRelevant = process.InitiatorUserId == userId || vnd.ActualizationResponsibleUserId == userId;
            if (!isRelevant) continue;

            var statusLabel = openActualizationVndIds.Contains(vnd.Id)
                ? "В процессе согласования по актуализации ВНД"
                : "В процессе согласования первой редакции ВНД";

            result.Add(new VndTaskResponse
            {
                VndId = vnd.Id,
                VndCode = vnd.Code,
                VndTitle = vnd.TitleRu,
                Scope = "myVndApproval",
                VndStatus = MapVndStatus(vnd.Status),
                RedactionId = process.RedactionId,
                RedactionCode = process.Redaction?.Code,
                // Текущий этап согласования — тот же смысл, что и у "Ждущих моего согласования",
                // только с точки зрения инициатора: на каком именно круге сейчас его редакция.
                // Для RevisionNeeded (на доработке у самого инициатора) фазы нет ни в одном из
                // трёх согласованных значений — фильтр по этапу её не подхватит, "Все этапы" покажет.
                StagePhase = MapProcessPhase(process.Status),
                StatusLabel = statusLabel,
                ActualizationPlannedNoChanges = vnd.ActualizationPlannedNoChanges,
                CreatedAt = vnd.UpdatedAt
            });
        }

        return result.OrderBy(t => t.CreatedAt).ToList();
    }

    /// <summary>"Отклонено" — см. ITasksService.GetRejectedTasksAsync. Показываем инициатору
    /// того процесса, который закончился отклонением, при условии, что это последний процесс
    /// по документу (иначе инициатор уже отправил редакцию заново, и актуальна другая задача —
    /// "Ждущие моего согласования"/"Мои ВНД на согласовании" по новому процессу).</summary>
    public async Task<List<VndTaskResponse>> GetRejectedTasksAsync(int userId)
    {
        var rejectedProcesses = await GetLatestRejectedProcessesAsync();
        var mine = rejectedProcesses.Where(p => p.InitiatorUserId == userId).ToList();
        if (mine.Count == 0) return new List<VndTaskResponse>();

        var rejecterIds = mine
            .Select(p => GetRejectionDecision(p)?.ApproverUserId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var rejecterNames = await _db.Users
            .Where(u => rejecterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return mine.Select(p =>
            {
                var rejection = GetRejectionDecision(p);
                return new VndTaskResponse
                {
                    VndId = p.VndId,
                    VndCode = p.Vnd!.Code,
                    VndTitle = p.Vnd!.TitleRu,
                    Scope = "rejected",
                    VndStatus = MapVndStatus(p.Vnd!.Status),
                    RedactionId = p.RedactionId,
                    RedactionCode = p.Redaction!.Code,
                    StatusLabel = "Редакция отклонена — необходимо внести правки и отправить заново",
                    RejectedByName = rejection is not null
                        ? rejecterNames.GetValueOrDefault(rejection.Value.ApproverUserId, "—")
                        : null,
                    RejectionComment = rejection?.Comment,
                    ActualizationPlannedNoChanges = p.Vnd!.ActualizationPlannedNoChanges,
                    CreatedAt = p.CompletedAt ?? p.UpdatedAt,
                };
            })
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    // ── История "Выполнено" по каждому разделу ─────────────────────────────
    //
    // Критерий "выполнено" разный для каждого раздела — это не текущий статус документа
    // (он общий и переходный), а завершение именно ТОЙ работы, которую раздел отслеживает:
    //   Согласование        — по этапу принято решение (согласовано/отклонено/зачтено
    //                          по таймауту), может быть несколько записей на этап, если
    //                          согласующий участвовал в нескольких турах одного процесса.
    //   Мои ВНД на согласовании — процесс инициатора завершился согласованием.
    //   Актуализация        — шаг "Выполнить актуализацию" пройден (см. правку выше в
    //                          GetActualizationTasksAsync).
    //   Консолидация        — документ опубликован (VndActualizationRecord.PublishedAt).
    //     Не покрывает консолидацию самой первой редакции документа вне цикла актуализации
    //     (для такого документа VndActualizationRecord не заводится вовсе) — это узкий и
    //     редкий случай, для него сейчас нет постоянной записи "кто опубликовал", отдельно
    //     обсудим, если понадобится.
    //   Отклонено           — редакция была отклонена, но по документу с тех пор запущен
    //                          новый процесс согласования (значит, отклонение уже не последнее).

    private static string MapDecisionLabel(ApprovalStageDecision decision) => decision switch
    {
        ApprovalStageDecision.Approved => "Согласовано",
        ApprovalStageDecision.ApprovedWithComment => "Согласовано с замечаниями",
        ApprovalStageDecision.Rejected => "Отклонено",
        ApprovalStageDecision.AutoApprovedByTimeout => "Согласовано по истечении срока (автоматически)",
        _ => "Решение принято"
    };

    private static PagedResult<T> Paginate<T>(List<T> items, int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        return new PagedResult<T>
        {
            Items = items.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList(),
            TotalCount = items.Count,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    public async Task<PagedResult<VndTaskResponse>> GetCoordinationDoneTasksAsync(int userId, int page, int pageSize)
    {
        var stages = await _db.Set<VndApprovalStage>()
            .Include(s => s.ApprovalProcess).ThenInclude(p => p!.Vnd)
            .Include(s => s.ApprovalProcess).ThenInclude(p => p!.Redaction)
            .Where(s => s.ApproverUserId == userId)
            .Where(s =>
                (s.PrimaryDecision != ApprovalStageDecision.Pending)
                || (s.RepeatDecision != null && s.RepeatDecision != ApprovalStageDecision.Pending)
                || (s.FinalHoldDecision != null && s.FinalHoldDecision != ApprovalStageDecision.Pending))
            .ToListAsync();

        var initiators = await GetInitiatorNamesAsync(stages.Select(s => s.ApprovalProcess!.InitiatorUserId));

        var items = new List<VndTaskResponse>();
        foreach (var s in stages)
        {
            var process = s.ApprovalProcess!;

            void AddIfDecided(ApprovalStageDecision? decision, string phase, DateTime? decidedAt)
            {
                if (decision is null || decision == ApprovalStageDecision.Pending) return;

                items.Add(new VndTaskResponse
                {
                    VndId = process.VndId,
                    VndCode = process.Vnd!.Code,
                    VndTitle = process.Vnd!.TitleRu,
                    Scope = "coordination",
                    VndStatus = MapVndStatus(process.Vnd!.Status),
                    RedactionId = process.RedactionId,
                    RedactionCode = process.Redaction!.Code,
                    StageId = s.Id,
                    StagePhase = phase,
                    StageKind = MapStageKind(s.Kind),
                    StageTitle = s.Title ?? MapLegacyStageTitle(s.Kind),
                    InitiatorName = initiators.GetValueOrDefault(process.InitiatorUserId, "—"),
                    StatusLabel = MapDecisionLabel(decision.Value),
                    ActualizationPlannedNoChanges = process.Vnd!.ActualizationPlannedNoChanges,
                    IsCompleted = true,
                    CompletedAt = decidedAt,
                    CreatedAt = decidedAt ?? process.CreatedAt
                });
            }

            AddIfDecided(s.PrimaryDecision, "primary", s.PrimaryDecidedAt);
            AddIfDecided(s.RepeatDecision, "repeat", s.RepeatDecidedAt);
            AddIfDecided(s.FinalHoldDecision, "final", s.FinalHoldDecidedAt);
        }

        return Paginate(items.OrderByDescending(t => t.CompletedAt).ToList(), page, pageSize);
    }

    public async Task<PagedResult<VndTaskResponse>> GetMyVndApprovalDoneTasksAsync(int userId, int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.VndApprovalProcesses
            .Include(p => p.Vnd)
            .Include(p => p.Redaction)
            .Where(p => p.InitiatorUserId == userId && p.Status == ApprovalProcessStatus.Approved)
            .OrderByDescending(p => p.CompletedAt);

        var totalCount = await query.CountAsync();
        var processes = await query.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync();

        return new PagedResult<VndTaskResponse>
        {
            Items = processes.Select(p => new VndTaskResponse
            {
                VndId = p.VndId,
                VndCode = p.Vnd!.Code,
                VndTitle = p.Vnd!.TitleRu,
                Scope = "myVndApproval",
                VndStatus = MapVndStatus(p.Vnd!.Status),
                RedactionId = p.RedactionId,
                RedactionCode = p.Redaction?.Code,
                StatusLabel = "Редакция согласована",
                IsCompleted = true,
                CompletedAt = p.CompletedAt,
                CreatedAt = p.CompletedAt ?? p.UpdatedAt
            }).ToList(),
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    public async Task<PagedResult<VndTaskResponse>> GetActualizationDoneTasksAsync(int userId, int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var openVndIds = await GetOpenActualizationVndIdsAsync();
        if (openVndIds.Count == 0)
            return new PagedResult<VndTaskResponse> { Page = safePage, PageSize = safePageSize };

        var rejectedVndIds = (await GetLatestRejectedProcessesAsync()).Select(p => p.VndId).ToHashSet();

        var query = _db.VndDocuments
            .Where(x => openVndIds.Contains(x.Id)
                        && x.ActualizationResponsibleUserId == userId
                        && x.Status == VndStatus.OnActualization
                        && x.ActualizationPerformed
                        && !rejectedVndIds.Contains(x.Id))
            .OrderByDescending(x => x.UpdatedAt);

        var totalCount = await query.CountAsync();
        var docs = await query.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync();

        return new PagedResult<VndTaskResponse>
        {
            Items = docs.Select(x => new VndTaskResponse
            {
                VndId = x.Id,
                VndCode = x.Code,
                VndTitle = x.TitleRu,
                Scope = "actualization",
                VndStatus = MapVndStatus(x.Status),
                StatusLabel = x.ActualizationPlannedNoChanges
                    ? "Актуализация выполнена — без изменений"
                    : "Актуализация выполнена",
                DueActualizationDate = x.DueActualizationDate,
                ActualizationPlannedNoChanges = x.ActualizationPlannedNoChanges,
                ActualizationPerformed = x.ActualizationPerformed,
                IsCompleted = true,
                CompletedAt = x.UpdatedAt,
                CreatedAt = x.UpdatedAt
            }).ToList(),
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    /// <summary>"Выполнено" для консолидации — на данный момент только та её часть, что прошла
    /// через цикл актуализации (VndActualizationRecord.PublishedAt). Консолидация самой первой
    /// редакции документа (без цикла актуализации) сюда пока не попадает — см. комментарий
    /// в блоке "История Выполнено" выше.</summary>
    public async Task<PagedResult<VndTaskResponse>> GetConsolidationDoneTasksAsync(int userId, int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Set<VndActualizationRecord>()
            .Include(r => r.Vnd)
            .Where(r => r.PublishedAt != null && r.ResponsibleUserId == userId)
            .OrderByDescending(r => r.PublishedAt);

        var totalCount = await query.CountAsync();
        var records = await query.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToListAsync();

        return new PagedResult<VndTaskResponse>
        {
            Items = records.Select(r => new VndTaskResponse
            {
                VndId = r.VndId,
                VndCode = r.Vnd!.Code,
                VndTitle = r.Vnd!.TitleRu,
                Scope = "consolidation",
                VndStatus = MapVndStatus(r.Vnd!.Status),
                StatusLabel = "Документ опубликован",
                ActualizationPlannedNoChanges = r.PlannedNoChanges,
                IsCompleted = true,
                CompletedAt = r.PublishedAt,
                CreatedAt = r.PublishedAt ?? r.UpdatedAt
            }).ToList(),
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    public async Task<PagedResult<VndTaskResponse>> GetRejectedDoneTasksAsync(int userId, int page, int pageSize)
    {
        var myProcesses = await _db.VndApprovalProcesses
            .Include(p => p.Vnd)
            .Include(p => p.Redaction)
            .Include(p => p.Stages)
            .Where(p => p.InitiatorUserId == userId)
            .ToListAsync();

        var latestIdByVnd = myProcesses
            .GroupBy(p => p.VndId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First().Id);

        // Отклонения, которые с тех пор уже перекрыты новым процессом по этому же документу —
        // значит, редакция была доработана и отправлена заново, "Отклонено" для неё в прошлом.
        var superseded = myProcesses
            .Where(p => p.Status == ApprovalProcessStatus.Rejected && latestIdByVnd[p.VndId] != p.Id)
            .OrderByDescending(p => p.UpdatedAt)
            .ToList();

        var rejecterIds = superseded
            .Select(p => GetRejectionDecision(p)?.ApproverUserId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var rejecterNames = await _db.Users
            .Where(u => rejecterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var items = superseded.Select(p =>
        {
            var rejection = GetRejectionDecision(p);
            return new VndTaskResponse
            {
                VndId = p.VndId,
                VndCode = p.Vnd!.Code,
                VndTitle = p.Vnd!.TitleRu,
                Scope = "rejected",
                VndStatus = MapVndStatus(p.Vnd!.Status),
                RedactionId = p.RedactionId,
                RedactionCode = p.Redaction!.Code,
                StatusLabel = "Отправлено повторно на согласование",
                RejectedByName = rejection is not null
                    ? rejecterNames.GetValueOrDefault(rejection.Value.ApproverUserId, "—")
                    : null,
                RejectionComment = rejection?.Comment,
                ActualizationPlannedNoChanges = p.Vnd!.ActualizationPlannedNoChanges,
                IsCompleted = true,
                CompletedAt = p.UpdatedAt,
                CreatedAt = p.UpdatedAt
            };
        }).ToList();

        return Paginate(items, page, pageSize);
    }

    /// <summary>Последний (по CreatedAt) процесс согласования на каждый ВНД из числа тех, что
    /// сейчас закончился отклонением - т.е. документов, которые отклонение вернуло в
    /// "Черновик"/"На актуализации" (см. VndApprovalService.RejectApprovalAsync) и по которым
    /// ещё не запускался новый цикл согласования. Ограничиваемся документами в этих двух
    /// статусах, чтобы не поднимать в память процессы по всем ВНД банка - отклонение больше
    /// ни в каком другом статусе документа появиться не может.</summary>
    private async Task<List<VndApprovalProcess>> GetLatestRejectedProcessesAsync()
    {
        var candidateVndIds = await _db.VndDocuments
            .Where(v => v.Status == VndStatus.Draft || v.Status == VndStatus.OnActualization)
            .Select(v => v.Id)
            .ToListAsync();
        if (candidateVndIds.Count == 0) return new List<VndApprovalProcess>();

        var processes = await _db.VndApprovalProcesses
            .Include(p => p.Vnd)
            .Include(p => p.Redaction)
            .Include(p => p.Stages)
            .Where(p => candidateVndIds.Contains(p.VndId))
            .ToListAsync();

        return processes
            .GroupBy(p => p.VndId)
            .Select(g => g.OrderByDescending(p => p.CreatedAt).First())
            .Where(p => p.Status == ApprovalProcessStatus.Rejected)
            .ToList();
    }

    /// <summary>Этап и решение (согласующий + комментарий-причина), которым процесс был
    /// отклонён - ровно один из трёх, т.к. отклонение сразу и необратимо прекращает процесс
    /// (см. RejectApprovalAsync). Null, если процесс не был отклонён вовсе - вызывающий код
    /// сам гарантирует Status == Rejected, но метод остаётся защищённым на случай неполных
    /// данных (например, старой записи без Stages).</summary>
    private static (int ApproverUserId, string? Comment)? GetRejectionDecision(VndApprovalProcess process)
    {
        foreach (var stage in process.Stages)
        {
            if (stage.PrimaryDecision == ApprovalStageDecision.Rejected)
                return (stage.ApproverUserId, stage.PrimaryComment);
            if (stage.RepeatDecision == ApprovalStageDecision.Rejected)
                return (stage.ApproverUserId, stage.RepeatComment);
            if (stage.FinalHoldDecision == ApprovalStageDecision.Rejected)
                return (stage.ApproverUserId, stage.FinalHoldComment);
        }

        return null;
    }

    private async Task<Dictionary<int, string>> GetInitiatorNamesAsync(IEnumerable<int> initiatorUserIds)
    {
        var ids = initiatorUserIds.Distinct().ToList();
        return await _db.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    private async Task<HashSet<int>> GetOpenActualizationVndIdsAsync(List<int>? restrictToVndIds = null)
    {
        var query = _db.Set<VndActualizationRecord>().Where(r => r.PublishedAt == null);
        if (restrictToVndIds is not null)
            query = query.Where(r => restrictToVndIds.Contains(r.VndId));

        var ids = await query.Select(r => r.VndId).Distinct().ToListAsync();
        return ids.ToHashSet();
    }

    private async Task<Dictionary<int, VndApprovalProcess>> GetCurrentApprovalProcessesByVndIdAsync(
        List<int> vndIds)
    {
        var processes = await _db.VndApprovalProcesses
            .Include(p => p.Redaction)
            .Where(p => vndIds.Contains(p.VndId)
                        && p.Status != ApprovalProcessStatus.Approved
                        && p.Status != ApprovalProcessStatus.Cancelled
                        && p.Status != ApprovalProcessStatus.Rejected)
            .ToListAsync();

        return processes
            .GroupBy(p => p.VndId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First());
    }

    public async Task<VndTaskCountsResponse> GetCountsAsync(int userId)
    {
        var coordinationCount = await _db.Set<VndApprovalStage>()
            .Where(s => s.ApproverUserId == userId)
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
            .CountAsync();

        var actualizationCount = (await GetActualizationTasksAsync(userId)).Count;
        var consolidationCount = (await GetConsolidationTasksAsync(userId)).Count;
        var myVndApprovalCount = (await GetMyVndApprovalTasksAsync(userId)).Count;
        var rejectedCount = (await GetRejectedTasksAsync(userId)).Count;

        return new VndTaskCountsResponse
        {
            Coordination = coordinationCount,
            Actualization = actualizationCount,
            Consolidation = consolidationCount,
            MyVndApproval = myVndApprovalCount,
            Rejected = rejectedCount
        };
    }

    /// <summary>Сводка персональных KPI для карточек на главной странице</summary>
    public async Task<VndHomeSummaryResponse> GetHomeSummaryAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Карточка 1: открытые циклы актуализации под моей ответственностью
        var myResponsibleActualizations = await _db.Set<VndActualizationRecord>()
            .CountAsync(r => r.ResponsibleUserId == userId && r.PublishedAt == null);

        // Карточка 2: решения, зачтённые мне по тайм-ауту, в текущем месяце
        // (Primary/Repeat/FinalHold — любая из трёх фаз, где я был согласующим)
        var myStages = await _db.Set<VndApprovalStage>()
            .Where(s => s.ApproverUserId == userId)
            .Select(s => new
            {
                s.PrimaryDecision, s.PrimaryDecidedAt,
                s.RepeatDecision, s.RepeatDecidedAt,
                s.FinalHoldDecision, s.FinalHoldDecidedAt
            })
            .ToListAsync();

        var myTimeoutApprovalsThisMonth = myStages.Count(s =>
            (s.PrimaryDecision == ApprovalStageDecision.AutoApprovedByTimeout
                && s.PrimaryDecidedAt.HasValue && s.PrimaryDecidedAt.Value >= monthStart)
            || (s.RepeatDecision == ApprovalStageDecision.AutoApprovedByTimeout
                && s.RepeatDecidedAt.HasValue && s.RepeatDecidedAt.Value >= monthStart)
            || (s.FinalHoldDecision == ApprovalStageDecision.AutoApprovedByTimeout
                && s.FinalHoldDecidedAt.HasValue && s.FinalHoldDecidedAt.Value >= monthStart));

        // Карточка 3: мои ВНД (я — инициатор согласования), процесс ещё не завершён
        var myVndAwaitingApproval = await _db.VndApprovalProcesses
            .CountAsync(p => p.InitiatorUserId == userId &&
                (p.Status == ApprovalProcessStatus.Primary
                 || p.Status == ApprovalProcessStatus.Repeated
                 || p.Status == ApprovalProcessStatus.RevisionNeeded
                 || p.Status == ApprovalProcessStatus.FinalHold));

        // Карточка 4: этапы, ожидающие решения именно меня прямо сейчас
        // (первичное/повторное согласование + финальная выдержка — все три уже внутри
        // GetCoordinationTasksAsync)
        var pendingMyApproval = await GetCoordinationTasksAsync(userId);

        return new VndHomeSummaryResponse
        {
            MyResponsibleActualizations = myResponsibleActualizations,
            MyTimeoutApprovalsThisMonth = myTimeoutApprovalsThisMonth,
            MyVndAwaitingApproval = myVndAwaitingApproval,
            PendingMyApproval = pendingMyApproval.Count
        };
    }

    // ── маппинг enum → строковые ключи для фронта ──────────────────────────

    /// <summary>"primary" | "repeat" | "final" — фаза согласования, под которую заточен и
    /// фильтр "Этап согласования" на странице "Мои задачи", и бейдж на карточке.
    /// RevisionNeeded (доработка у инициатора) не сопоставляется ни с одной из трёх фаз.</summary>
    private static string? MapProcessPhase(ApprovalProcessStatus status) => status switch
    {
        ApprovalProcessStatus.Primary => "primary",
        ApprovalProcessStatus.Repeated => "repeat",
        ApprovalProcessStatus.FinalHold => "final",
        _ => null
    };

    private static string MapStageKind(ApprovalStageKind kind) => kind switch
    {
        ApprovalStageKind.Legal => "legal",
        ApprovalStageKind.RiskManagement => "risk_management",
        ApprovalStageKind.Compliance => "compliance",
        ApprovalStageKind.Custom => "custom",
        ApprovalStageKind.Methodology => "methodology",
        ApprovalStageKind.Fixed => "fixed",
        _ => "custom"
    };

    /// <summary>Название этапа для маршрутов, построенных ДО перехода на динамический
    /// справочник (VndApprovalStage.Title тогда ещё не заполнялся) - см. аналогичный метод
    /// в VndApprovalService.</summary>
    private static string MapLegacyStageTitle(ApprovalStageKind kind) => kind switch
    {
        ApprovalStageKind.Legal => "Юридическое управление",
        ApprovalStageKind.RiskManagement => "Риск-менеджмент",
        ApprovalStageKind.Compliance => "Комплаенс-контроль",
        ApprovalStageKind.Methodology => "Методология",
        _ => "Доп. согласующий"
    };

    private static string MapVndStatus(VndStatus status) => status switch
    {
        VndStatus.Active => "active",
        VndStatus.OnActualization => "onact",
        VndStatus.Review => "review",
        VndStatus.Consolidation => "consol",
        VndStatus.Archived => "arch",
        VndStatus.Draft => "draft",
        _ => "active"
    };
}
