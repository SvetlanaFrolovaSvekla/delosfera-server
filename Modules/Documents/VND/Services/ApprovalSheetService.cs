using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Итоговая версия, под которой должны стоять все подписи Листа согласования: отпечаток текста
/// редакции (компонент "content" хэша резолюции — SHA-256 файлов RU/KG/EN) и код ВНД — те же две
/// части подписанного хэша, что общие для всех согласующих. Остальные части хэша — решение и
/// комментарий — у каждого свои: каждый подписывает итоговый текст вместе со своими словами.</summary>
public sealed record ApprovalSheetReference(string ContentDigest, string VndCode);

/// <summary>Строки Листа согласования из этапов процесса: фактическое (последнее принятое) решение
/// каждого согласующего и, в режиме с подписями, штамп ЭЦП из журнала VndResolutionSignature.
/// Чистая функция — используется и при просмотре/скачивании листа (ApprovalSheetService), и при
/// сохранении листа в момент завершения согласования (VndApprovalService.GenerateApprovalSheetAsync).</summary>
public static class ApprovalSheetRowBuilder
{
    /// <summary>Решения, проставленные системой, а не человеком: инициатор-согласующий
    /// (VndApprovalService.InitiatorAutoApprovedComment) и перенос чистого согласования на финальную
    /// выдержку (VndApprovalService.ResetFinalHoldDecisionsAsync) — оба текста начинаются так.</summary>
    internal const string AutoCommentPrefix = "Согласовано автоматически";

    /// <summary>Пояснение в колонке "Подпись", когда ЭЦП под этим решением есть, но поставлена под
    /// другой версией текста (или другим кодом ВНД), чем итоговая. Штамп в таком случае не ставится:
    /// лист подтверждает подписи только под итоговым текстом.</summary>
    public const string OtherVersionNote = "ЭЦП стоит под другой версией текста — под итоговой не подписано";

    /// <summary>Пояснение в колонке "Подпись" для этапа, где согласующий — сам инициатор согласования.</summary>
    public const string InitiatorAutoNote = "Без ЭЦП — согласовано автоматически (инициатор согласования)";

    /// <param name="reference">Итоговая версия (только режим с подписями). Задана — штамп ставится
    /// лишь для подписи под этой версией; null — сверка не выполняется (как раньше).</param>
    public static List<ApprovalSheetRow> Build(
        VndApprovalProcess process,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, List<VndResolutionSignature>>? signaturesByStage,
        TimeZoneInfo zone,
        ApprovalSheetReference? reference = null)
    {
        var rows = new List<ApprovalSheetRow>();

        // Убранные главным редактором этапы не значатся согласующими.
        foreach (var stage in process.Stages.Where(s => !s.IsRemovedByEditor).OrderBy(s => s.Order))
        {
            usersById.TryGetValue(stage.ApproverUserId, out var user);
            var (phase, decision, decidedAt, comment) = Effective(stage);
            // Автоакцепт по истечении норматива тоже проставлен системой — в листе он выглядит как
            // остальные автоматические согласования ("Согласовано", "автоматически").
            var auto = comment?.StartsWith(AutoCommentPrefix, StringComparison.Ordinal) == true
                       || decision == ApprovalStageDecision.AutoApprovedByTimeout;

            // Перенос чистого согласования на финальную выдержку — дата реального решения, а не
            // момент входа в выдержку.
            if (auto && phase == ApprovalStagePhase.FinalHold && decision == ApprovalStageDecision.Approved)
                decidedAt = stage.RepeatDecidedAt ?? stage.PrimaryDecidedAt ?? decidedAt;

            var detail = new List<string>();
            if (decision != ApprovalStageDecision.Pending || phase != ApprovalStagePhase.Primary)
            {
                var phaseText = PhaseText(phase);
                if (phaseText is not null) detail.Add(phaseText);
            }
            if (auto) detail.Add("автоматически");

            ApprovalSheetStamp? stamp = null;
            string? signatureNote = null;
            if (signaturesByStage is not null)
            {
                // Подписи под ЭТИМ решением: то же решение и та же фаза (для перенесённого на финальную
                // выдержку согласования — подпись под исходным "Согласовать" из более ранней фазы).
                signaturesByStage.TryGetValue(stage.Id, out var stageSignatures);
                var forDecision = (stageSignatures ?? new List<VndResolutionSignature>())
                    .Where(s => s.Decision == decision && (s.Phase == phase || auto))
                    .OrderByDescending(s => s.SignedAt)
                    .ToList();

                // Штамп — только под итоговой версией текста: подпись под предыдущей версией не
                // подтверждает согласие с тем, что утверждается.
                var signature = reference is null
                    ? forDecision.FirstOrDefault()
                    : forDecision.FirstOrDefault(s => IsUnder(s, reference));

                if (signature is not null)
                {
                    // Перенесённое согласование — дата реальной подписи, а не входа в выдержку.
                    if (auto && phase == ApprovalStagePhase.FinalHold)
                        decidedAt = signature.SignedAt;

                    stamp = new ApprovalSheetStamp(
                        signature.PersonFio ?? signature.CertCommonName ?? user?.FullName,
                        signature.OrganizationName,
                        signature.CertValidNotAfter,
                        signature.CertIssuerCommonName,
                        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(signature.SignedAt, DateTimeKind.Utc), zone),
                        signature.Hash,
                        signature.SignatureSha256);
                }
                else if (forDecision.Count > 0)
                {
                    signatureNote = OtherVersionNote;
                }
                else
                {
                    signatureNote = decision switch
                    {
                        ApprovalStageDecision.Pending => "—",
                        // Инициатор согласования сам числится согласующим (например, обязательный этап его
                        // подразделения) — его решение проставляет система (VndApprovalService
                        // .AutoApproveInitiatorStages), подписи под ним нет.
                        ApprovalStageDecision.Approved when auto && stage.ApproverUserId == process.InitiatorUserId =>
                            InitiatorAutoNote,
                        ApprovalStageDecision.Approved when auto => "Без ЭЦП — согласовано автоматически",
                        // Автоакцепт по истечении норматива — та же формулировка, что и у прочих
                        // автоматических согласований.
                        ApprovalStageDecision.AutoApprovedByTimeout => "Без ЭЦП — согласовано автоматически",
                        _ => "Без ЭЦП",
                    };
                }
            }

            rows.Add(new ApprovalSheetRow(
                user?.FullName ?? "",
                user?.Position?.TitleRu,
                DecisionText(decision),
                detail.Count > 0 ? string.Join(", ", detail) : null,
                decidedAt is { } at ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(at, DateTimeKind.Utc), zone) : null,
                stamp,
                signatureNote));
        }

        return rows;
    }

    /// <summary>Подпись поставлена под итоговой версией: тот же отпечаток текста и тот же код ВНД.</summary>
    private static bool IsUnder(VndResolutionSignature signature, ApprovalSheetReference reference) =>
        string.Equals(signature.ContentDigest, reference.ContentDigest, StringComparison.Ordinal)
        && string.Equals(signature.VndCode, reference.VndCode, StringComparison.Ordinal);

    /// <summary>Последнее решение по этапу: финальная выдержка → повторное согласование → первичное
    /// (то же правило, что VndApprovalService.LatestDecisionBeforeFinalHold).</summary>
    internal static (ApprovalStagePhase Phase, ApprovalStageDecision Decision, DateTime? At, string? Comment)
        Effective(VndApprovalStage s)
    {
        if (s.FinalHoldDecision is { } fh)
            return (ApprovalStagePhase.FinalHold, fh, s.FinalHoldDecidedAt, s.FinalHoldComment);
        if (s.RepeatDecision is { } rd)
            return (ApprovalStagePhase.Repeat, rd, s.RepeatDecidedAt, s.RepeatComment);
        return (ApprovalStagePhase.Primary, s.PrimaryDecision, s.PrimaryDecidedAt, s.PrimaryComment);
    }

    private static string DecisionText(ApprovalStageDecision decision) => decision switch
    {
        ApprovalStageDecision.Approved => "Согласовано",
        ApprovalStageDecision.ApprovedWithComment => "Согласовано с замечаниями",
        ApprovalStageDecision.Rejected => "Отклонено",
        ApprovalStageDecision.AutoApprovedByTimeout => "Согласовано",
        ApprovalStageDecision.RemovedByEditor => "Исключён из маршрута",
        _ => "Ожидает решения",
    };

    private static string? PhaseText(ApprovalStagePhase phase) => phase switch
    {
        ApprovalStagePhase.Repeat => "повторное согласование",
        ApprovalStagePhase.FinalHold => "финальная выдержка",
        _ => null,
    };
}

public interface IApprovalSheetService
{
    /// <summary>Лист согласования процесса по его ТЕКУЩЕМУ состоянию — формируется при каждом запросе,
    /// поэтому всегда отражает последние решения. <paramref name="processId"/> — процесс этого ВНД.</summary>
    Task<(byte[] Content, string FileName)> BuildAsync(
        int vndId, int processId, ApprovalSheetMode mode, int currentUserId, CancellationToken ct = default);
}

public sealed class ApprovalSheetService : IApprovalSheetService
{
    private readonly DelosferaDbContext _db;
    private readonly IApprovalSheetGenerator _generator;
    private readonly ICurrentUserService _currentUser;
    private readonly IBankClock _clock;
    private readonly IResolutionSignatureService _resolutionSignatures;

    public ApprovalSheetService(DelosferaDbContext db, IApprovalSheetGenerator generator,
        ICurrentUserService currentUser, IBankClock clock, IResolutionSignatureService resolutionSignatures)
    {
        _db = db;
        _generator = generator;
        _currentUser = currentUser;
        _clock = clock;
        _resolutionSignatures = resolutionSignatures;
    }

    public async Task<(byte[] Content, string FileName)> BuildAsync(
        int vndId, int processId, ApprovalSheetMode mode, int currentUserId, CancellationToken ct = default)
    {
        await EnsureCanViewAsync(vndId, currentUserId, ct);

        var process = await _db.VndApprovalProcesses
                          .AsNoTracking()
                          .Include(p => p.Stages)
                          .Include(p => p.Vnd)
                          .Include(p => p.Redaction)
                          .FirstOrDefaultAsync(p => p.Id == processId && p.VndId == vndId, ct)
                      ?? throw new KeyNotFoundException("Процесс согласования не найден");

        var approverIds = process.Stages.Select(s => s.ApproverUserId).Distinct().ToList();
        var usersById = await _db.Users.AsNoTracking()
            .Include(u => u.Position)
            .Where(u => approverIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        Dictionary<int, List<VndResolutionSignature>>? signatures = null;
        ApprovalSheetReference? reference = null;
        string? integrityWarning = null;
        if (mode == ApprovalSheetMode.Signed)
        {
            var all = await _db.VndResolutionSignatures.AsNoTracking()
                .Where(s => s.ApprovalProcessId == process.Id)
                .ToListAsync(ct);
            signatures = all
                .GroupBy(s => s.StageId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Итоговая версия — текст редакции, как он лежит в хранилище сейчас, и текущий код ВНД.
            // Лист с ЭЦП подтверждает подписи только под ней: если текст заменили после подписи
            // (в т.ч. прямым редактированием уже согласованной редакции), это будет видно в листе.
            var vndCode = ResolutionSignatureHasher.NormalizeCode(process.Vnd!.Code);
            try
            {
                reference = new ApprovalSheetReference(
                    await _resolutionSignatures.GetContentDigestAsync(process.Redaction!, ct), vndCode);
            }
            catch (ResolutionSignatureException ex) when (ex.Code == ResolutionSignatureErrorCodes.StorageIntegrity)
            {
                // Файл в хранилище подменён — сопоставить подписи с текстом нельзя: ни одна не
                // засчитывается (пустой отпечаток ни с чем не совпадёт), в шапке — предупреждение.
                reference = new ApprovalSheetReference(string.Empty, vndCode);
                integrityWarning = "файл текста в хранилище не совпадает с контрольной суммой, зафиксированной " +
                                   "при загрузке, — подписи нельзя сопоставить с текстом";
            }
        }

        var rows = ApprovalSheetRowBuilder.Build(process, usersById, signatures, _clock.Zone, reference);

        var finished = process.Status is ApprovalProcessStatus.Approved
            or ApprovalProcessStatus.Rejected or ApprovalProcessStatus.Cancelled;
        var note = $"Состояние на {_clock.Now:dd.MM.yyyy HH:mm}" +
                   (finished ? "" : " · согласование не завершено, лист промежуточный") +
                   (process.IsNoChangesActualization ? " · актуализация без изменений" : "");

        if (reference is not null)
            note += SignedVersionNote(rows, reference, integrityWarning);

        var content = _generator.Generate(process.Vnd!.TitleRu, rows, mode, note);
        var code = process.Redaction?.Code ?? process.Vnd.Code;
        var fileName = mode == ApprovalSheetMode.Signed
            ? $"{code}_Лист_согласования_ЭЦП.docx"
            : $"{code}_Лист_согласования.docx";
        return (content, fileName);
    }

    /// <summary>Строка в шапке листа с ЭЦП: под какой версией текста стоят подписи. Отпечаток —
    /// тот же компонент "content", что входит в хэш каждой подписи, поэтому его можно сверить с
    /// журналом подписей и пересчитать по самим файлам.</summary>
    private static string SignedVersionNote(
        IReadOnlyList<ApprovalSheetRow> rows, ApprovalSheetReference reference, string? integrityWarning)
    {
        if (integrityWarning is not null)
            return $" · Внимание: {integrityWarning}";

        var stamped = rows.Count(r => r.Stamp is not null);
        var otherVersion = rows.Count(r => r.SignatureNote == ApprovalSheetRowBuilder.OtherVersionNote);

        var summary = otherVersion > 0
            ? $" · Внимание: согласующих с ЭЦП под другой версией текста, а не под итоговой: {otherVersion}"
            : stamped > 0
                ? $" · Подписей ЭЦП в листе: {stamped}, все — под одной итоговой версией текста"
                : "";

        return summary + $" · Отпечаток итогового текста (SHA-256): {reference.ContentDigest}";
    }

    /// <summary>Видимость — как у самого документа и журнала подписей (ResolutionSignatureService):
    /// ViewVnd проверяет контроллер; чужой черновик — только создателю, участникам согласования или
    /// с правом ViewOtherUsersDrafts.</summary>
    private async Task EnsureCanViewAsync(int vndId, int currentUserId, CancellationToken ct)
    {
        var vnd = await _db.VndDocuments.AsNoTracking()
                      .Where(v => v.Id == vndId)
                      .Select(v => new { v.Status, v.CreatedByUserId })
                      .FirstOrDefaultAsync(ct)
                  ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        if (vnd.Status != VndStatus.Draft
            || vnd.CreatedByUserId == currentUserId
            || _currentUser.HasPermission(PermissionCode.ViewOtherUsersDrafts))
            return;

        var isParticipant = await _db.VndApprovalStages
            .AnyAsync(s => s.ApprovalProcess!.VndId == vndId && s.ApproverUserId == currentUserId, ct);
        if (!isParticipant)
            throw new UnauthorizedAccessException("Нет доступа к документу");
    }
}
