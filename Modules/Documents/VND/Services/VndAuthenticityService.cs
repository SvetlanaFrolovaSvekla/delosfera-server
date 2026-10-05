using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Response;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

public interface IVndAuthenticityService
{
    /// <summary>Проверка подлинности согласования по файлу редакции: SHA-256 файла → редакции ВНД,
    /// текстом которых он был, и подписи ЭЦП, поставленные под этим текстом.</summary>
    Task<VndAuthenticityCheckResponse> CheckAsync(IFormFile file, int currentUserId, CancellationToken ct = default);
}

/// <summary>Чистые функции проверки подлинности (без БД) — сопоставление файла с отпечатком текста
/// подписи и состояние подписи итогового решения согласующего.</summary>
public static class VndAuthenticityMatcher
{
    /// <summary>Под этим ли файлом стоит подпись: SHA-256 файла входит в отпечаток текста подписи
    /// ("ru=HEX;kg=HEX|-;en=HEX|-", см. ResolutionSignatureHasher.BuildContentDigest). Возвращает
    /// языковую версию ("ru"/"kg"/"en"), которой был файл, или null — подпись под другим текстом.</summary>
    public static string? LanguageOf(string? contentDigest, string fileSha256)
    {
        if (string.IsNullOrEmpty(contentDigest) || string.IsNullOrWhiteSpace(fileSha256)) return null;

        var sha = fileSha256.Trim();
        // Только настоящий SHA-256: отметка "-" (языковой версии нет) ни с каким файлом не совпадает.
        if (sha.Length != 64 || sha == ResolutionSignatureHasher.MissingFileMarker) return null;

        foreach (var part in contentDigest.Split(';'))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            if (string.Equals(part[(eq + 1)..], sha, StringComparison.OrdinalIgnoreCase))
                return part[..eq];
        }

        return null;
    }

    /// <summary>
    /// Состояние подписи итогового решения согласующего относительно файла. Подпись ищется под тем же
    /// решением и в той же фазе, что и итоговое решение (для перенесённого на финальную выдержку
    /// согласования — в любой фазе), — то же правило, что в Листе согласования
    /// (ApprovalSheetRowBuilder).
    /// </summary>
    public static (string State, VndResolutionSignature? Signature) SignatureState(
        VndApprovalStage stage, int initiatorUserId, IEnumerable<VndResolutionSignature> stageSignatures,
        string fileSha256)
    {
        var (phase, decision, _, comment) = ApprovalSheetRowBuilder.Effective(stage);
        if (decision == ApprovalStageDecision.Pending) return ("pending", null);

        var auto = comment?.StartsWith(ApprovalSheetRowBuilder.AutoCommentPrefix, StringComparison.Ordinal) == true
                   || decision == ApprovalStageDecision.AutoApprovedByTimeout;

        var forDecision = stageSignatures
            .Where(s => s.Decision == decision && (s.Phase == phase || auto))
            .OrderByDescending(s => s.SignedAt)
            .ToList();

        var underThisText = forDecision.FirstOrDefault(s => LanguageOf(s.ContentDigest, fileSha256) is not null);
        if (underThisText is not null) return ("signed_this_text", underThisText);
        if (forDecision.Count > 0) return ("signed_other_text", null);

        if (auto && decision == ApprovalStageDecision.Approved && stage.ApproverUserId == initiatorUserId)
            return ("auto_initiator", null);
        if (auto) return ("auto", null);
        return ("no_signature", null);
    }

    /// <summary>Состояние подписи отдельного решения истории: подпись под ним (под этим файлом или
    /// под другой версией текста) либо почему её нет.</summary>
    public static string EntrySignatureState(
        ApprovalStageDecision decision, string? comment, bool approverIsInitiator,
        VndResolutionSignature? signature, string fileSha256)
    {
        if (signature is not null)
            return LanguageOf(signature.ContentDigest, fileSha256) is not null ? "signed_this_text" : "signed_other_text";

        var auto = comment?.StartsWith(ApprovalSheetRowBuilder.AutoCommentPrefix, StringComparison.Ordinal) == true
                   || decision == ApprovalStageDecision.AutoApprovedByTimeout;
        if (auto && decision == ApprovalStageDecision.Approved && approverIsInitiator) return "auto_initiator";
        if (auto) return "auto";
        return "no_signature";
    }

    /// <summary>
    /// История согласования процесса: решения первичного этапа, всех сохранённых кругов повторного
    /// согласования и финальной выдержки (VndApprovalPhaseRound) и текущего круга, плюс комментарии
    /// инициатора к исправлениям. У каждого решения — подпись ЭЦП из журнала, найденная по
    /// (этап, фаза, круг, решение): ровно так подпись и записывается (см. VndResolutionSignature).
    /// Нерешённые (Pending) и снятые главным редактором решения в историю не входят.
    /// </summary>
    public static List<VndAuthenticityHistoryEntry> BuildHistory(
        VndApprovalProcess process,
        IEnumerable<VndApprovalPhaseRound> rounds,
        IReadOnlyList<VndResolutionSignature> processSignatures,
        IReadOnlyDictionary<int, User> usersById,
        string fileSha256)
    {
        var entries = new List<VndAuthenticityHistoryEntry>();
        var stagesById = process.Stages.ToDictionary(s => s.Id);

        void AddDecision(
            VndApprovalStage stage, ApprovalStagePhase phase, int round,
            ApprovalStageDecision? decision, string? comment, DateTime? at)
        {
            if (decision is not { } d || d is ApprovalStageDecision.Pending or ApprovalStageDecision.RemovedByEditor)
                return;

            var signature = processSignatures
                .Where(s => s.StageId == stage.Id && s.Phase == phase && s.RoundNumber == round && s.Decision == d)
                .OrderByDescending(s => s.SignedAt)
                .FirstOrDefault();
            usersById.TryGetValue(stage.ApproverUserId, out var user);

            entries.Add(new VndAuthenticityHistoryEntry
            {
                Kind = "decision",
                Phase = PhaseCode(phase),
                RoundNumber = round,
                At = signature?.SignedAt ?? at,
                StageTitle = stage.Title,
                FullName = user?.FullName ?? "—",
                Position = user?.Position?.TitleRu,
                Decision = DecisionCode(d),
                Comment = comment,
                SignatureState = EntrySignatureState(
                    d, comment, stage.ApproverUserId == process.InitiatorUserId, signature, fileSha256),
                Signature = signature is null ? null : ToSignatureDto(signature, fileSha256),
            });
        }

        void AddInitiatorComment(int round, string? comment, DateTime? at)
        {
            if (string.IsNullOrWhiteSpace(comment)) return;
            usersById.TryGetValue(process.InitiatorUserId, out var initiator);
            entries.Add(new VndAuthenticityHistoryEntry
            {
                Kind = "initiator_comment",
                Phase = PhaseCode(ApprovalStagePhase.Repeat),
                RoundNumber = round,
                At = at,
                FullName = initiator?.FullName,
                Position = initiator?.Position?.TitleRu,
                Comment = comment,
            });
        }

        // --- Первичное согласование: у него всегда один круг, поля Primary* не перезаписываются.
        foreach (var stage in process.Stages)
            AddDecision(stage, ApprovalStagePhase.Primary, 1, stage.PrimaryDecision, stage.PrimaryComment,
                stage.PrimaryDecidedAt);

        // --- Сохранённые (завершённые) круги повторного согласования и финальной выдержки.
        var roundList = rounds.Where(r => r.ApprovalProcessId == process.Id).ToList();
        foreach (var phaseRound in roundList.OrderBy(r => r.Phase).ThenBy(r => r.RoundNumber))
        {
            if (phaseRound.Phase == ApprovalStagePhase.Repeat)
                AddInitiatorComment(phaseRound.RoundNumber, phaseRound.InitiatorComment, phaseRound.StartedAt);

            foreach (var stageDecision in phaseRound.StageDecisions)
            {
                if (!stagesById.TryGetValue(stageDecision.VndApprovalStageId, out var roundStage)) continue;
                AddDecision(roundStage, phaseRound.Phase, phaseRound.RoundNumber, stageDecision.Decision,
                    stageDecision.Comment, stageDecision.DecidedAt);
            }
        }

        // --- Текущий (ещё не сохранённый в VndApprovalPhaseRound) круг каждой фазы.
        var currentRepeatRound = roundList.Count(r => r.Phase == ApprovalStagePhase.Repeat) + 1;
        var currentFinalHoldRound = roundList.Count(r => r.Phase == ApprovalStagePhase.FinalHold) + 1;

        if (process.RepeatStartedAt is not null)
            AddInitiatorComment(currentRepeatRound, process.RepeatInitiatorComment, process.RepeatStartedAt);

        foreach (var stage in process.Stages)
        {
            AddDecision(stage, ApprovalStagePhase.Repeat, currentRepeatRound, stage.RepeatDecision,
                stage.RepeatComment, stage.RepeatDecidedAt);
            AddDecision(stage, ApprovalStagePhase.FinalHold, currentFinalHoldRound, stage.FinalHoldDecision,
                stage.FinalHoldComment, stage.FinalHoldDecidedAt);
        }

        return entries
            .OrderBy(e => e.At ?? DateTime.MaxValue)
            .ThenBy(e => e.Kind == "initiator_comment" ? 0 : 1)
            .ToList();
    }

    /// <summary>Запись журнала подписей → ответ (без ПИН владельца и без самой CMS-подписи).</summary>
    public static VndAuthenticitySignature ToSignatureDto(VndResolutionSignature s, string fileSha256) => new()
    {
        Id = s.Id,
        StageTitle = s.Stage?.Title,
        Phase = PhaseCode(s.Phase),
        RoundNumber = s.RoundNumber,
        Decision = DecisionCode(s.Decision),
        CommentText = s.CommentText,
        AccountFullName = s.User?.FullName,
        PersonFio = s.PersonFio,
        OrganizationName = s.OrganizationName,
        CertCommonName = s.CertCommonName,
        CertIssuerCommonName = s.CertIssuerCommonName,
        CertValidNotAfter = s.CertValidNotAfter,
        SignedAt = s.SignedAt,
        CmsSigningTime = s.CmsSigningTime,
        Verified = s.Verified,
        HashSchemaVersion = s.HashSchemaVersion,
        Hash = s.Hash,
        ContentDigest = s.ContentDigest,
        VndCode = s.VndCode,
        SignatureSha256 = s.SignatureSha256,
        UnderThisText = LanguageOf(s.ContentDigest, fileSha256) is not null,
    };

    public static string DecisionCode(ApprovalStageDecision decision) => decision switch
    {
        ApprovalStageDecision.Pending => "pending",
        ApprovalStageDecision.Approved => "approved",
        ApprovalStageDecision.ApprovedWithComment => "approved_with_comment",
        ApprovalStageDecision.Rejected => "rejected",
        ApprovalStageDecision.AutoApprovedByTimeout => "auto_approved",
        ApprovalStageDecision.RemovedByEditor => "removed",
        _ => "pending",
    };

    public static string PhaseCode(ApprovalStagePhase phase) => phase switch
    {
        ApprovalStagePhase.Repeat => "repeat",
        ApprovalStagePhase.FinalHold => "finalHold",
        _ => "primary",
    };

    public static string StatusCode(ApprovalProcessStatus status) => status switch
    {
        ApprovalProcessStatus.Primary => "primary",
        ApprovalProcessStatus.RevisionNeeded => "revision_needed",
        ApprovalProcessStatus.Repeated => "repeated",
        ApprovalProcessStatus.FinalHold => "final_hold",
        ApprovalProcessStatus.Approved => "approved",
        ApprovalProcessStatus.Cancelled => "cancelled",
        ApprovalProcessStatus.Rejected => "rejected",
        _ => "primary",
    };
}

/// <summary>
/// Проверка подлинности согласования ВНД по файлу редакции.
///
/// Каждая подпись ЭЦП под резолюцией хранит отпечаток текста, под которым она поставлена, —
/// SHA-256 файлов редакции по языкам (VndResolutionSignature.ContentDigest). Поэтому по самому
/// файлу можно найти, кто, когда, с каким решением и комментарием его подписал: SHA-256
/// загруженного файла ищется в отпечатках подписей. Дополнительно — среди файлов редакций
/// (текущих и промежуточных версий доработки), чтобы найти и согласования без ЭЦП.
///
/// Совпадение по SHA-256 — побайтное: файл, открытый и пересохранённый в Word, даёт другой хэш, даже
/// если текст не менялся. Проверять нужно тот файл, который скачан из системы.
///
/// Видимость — как у самого документа и журнала подписей: чужой черновик ВНД виден только создателю,
/// участникам согласования и с правом ViewOtherUsersDrafts. ПИН владельца подписи не отдаётся.
/// </summary>
public sealed class VndAuthenticityService : IVndAuthenticityService
{
    /// <summary>Больше редакций в ответе не показываем — один и тот же файл едва ли бывает текстом
    /// десятков редакций; ограничение — защита от тяжёлых ответов.</summary>
    public const int MaxRedactions = 20;

    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public VndAuthenticityService(DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<VndAuthenticityCheckResponse> CheckAsync(
        IFormFile file, int currentUserId, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new InvalidOperationException("Загрузите файл редакции");

        string sha256;
        await using (var stream = file.OpenReadStream())
        {
            sha256 = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)); // upper-case, как в отпечатках
        }
        var sha256Lower = sha256.ToLowerInvariant(); // FileAttachment.Hash — нижний регистр

        var response = new VndAuthenticityCheckResponse
        {
            FileName = Path.GetFileName(file.FileName),
            FileSize = file.Length,
            FileSha256 = sha256,
        };

        // --- 1. Подписи, в отпечатке текста которых есть этот файл (любой язык).
        var marker = "=" + sha256;
        var signatureHits = await _db.VndResolutionSignatures.AsNoTracking()
            .Where(s => s.ContentDigest.Contains(marker))
            .Select(s => new { s.RedactionId, s.ApprovalProcessId, s.ContentDigest })
            .ToListAsync(ct);

        // --- 2. Файлы с тем же содержимым — текущие тексты редакций и промежуточные версии доработки.
        var fileIds = await _db.FileAttachments.AsNoTracking()
            .Where(f => f.Hash == sha256Lower)
            .Select(f => f.Id)
            .ToListAsync(ct);

        var currentHits = fileIds.Count == 0
            ? []
            : await _db.VndRedactions.AsNoTracking()
                .Where(r => fileIds.Contains(r.DocFileRuId)
                            || (r.DocFileKgId != null && fileIds.Contains(r.DocFileKgId.Value))
                            || (r.DocFileEnId != null && fileIds.Contains(r.DocFileEnId.Value)))
                .Select(r => new FileHit(r.Id, null, null, r.DocFileRuId, r.DocFileKgId, r.DocFileEnId))
                .ToListAsync(ct);

        var snapshotHits = fileIds.Count == 0
            ? []
            : await _db.Set<VndRedactionRevisionSnapshot>().AsNoTracking()
                .Where(s => fileIds.Contains(s.DocFileRuId)
                            || (s.DocFileKgId != null && fileIds.Contains(s.DocFileKgId.Value))
                            || (s.DocFileEnId != null && fileIds.Contains(s.DocFileEnId.Value)))
                .Select(s => new FileHit(s.VndRedactionId, s.ApprovalProcessId, s.SnapshotNumber,
                    s.DocFileRuId, s.DocFileKgId, s.DocFileEnId))
                .ToListAsync(ct);

        var redactionIds = signatureHits.Select(h => h.RedactionId)
            .Concat(currentHits.Select(h => h.RedactionId))
            .Concat(snapshotHits.Select(h => h.RedactionId))
            .Distinct()
            .ToList();

        if (redactionIds.Count == 0) return response;

        // --- 3. Редакции и видимость документов.
        var redactions = await _db.VndRedactions.AsNoTracking()
            .Include(r => r.Vnd)
            .Where(r => redactionIds.Contains(r.Id))
            .ToListAsync(ct);

        var vndIds = redactions.Select(r => r.VndId).Distinct().ToList();
        var participantVndIds = await _db.VndApprovalStages.AsNoTracking()
            .Where(s => s.ApproverUserId == currentUserId && vndIds.Contains(s.ApprovalProcess!.VndId))
            .Select(s => s.ApprovalProcess!.VndId)
            .Distinct()
            .ToListAsync(ct);
        var canSeeDrafts = _currentUser.HasPermission(PermissionCode.ViewOtherUsersDrafts);

        bool Visible(VndDocument? vnd) =>
            vnd is not null
            && (vnd.Status != VndStatus.Draft
                || vnd.CreatedByUserId == currentUserId
                || canSeeDrafts
                || participantVndIds.Contains(vnd.Id));

        var visible = redactions.Where(r => Visible(r.Vnd)).ToList();
        response.HasHiddenMatches = visible.Count < redactions.Count;

        var shown = visible
            .OrderBy(r => r.Vnd!.Code)
            .ThenByDescending(r => r.Number)
            .Take(MaxRedactions)
            .ToList();
        response.Truncated = visible.Count > shown.Count;

        if (shown.Count == 0) return response;

        // --- 4. Процессы согласования этих редакций, их подписи и согласующие.
        var shownIds = shown.Select(r => r.Id).ToList();
        var processes = await _db.VndApprovalProcesses.AsNoTracking()
            .Include(p => p.Stages)
            .Where(p => shownIds.Contains(p.RedactionId))
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        var processIds = processes.Select(p => p.Id).ToList();
        var signatures = await _db.VndResolutionSignatures.AsNoTracking()
            .Include(s => s.Stage)
            .Include(s => s.User)
            .Where(s => processIds.Contains(s.ApprovalProcessId))
            .ToListAsync(ct);

        var rounds = await _db.VndApprovalPhaseRounds.AsNoTracking()
            .Include(r => r.StageDecisions)
            .Where(r => processIds.Contains(r.ApprovalProcessId))
            .ToListAsync(ct);

        var approverIds = processes.SelectMany(p => p.Stages).Select(s => s.ApproverUserId)
            .Concat(processes.Select(p => p.InitiatorUserId))
            .Distinct()
            .ToList();
        var usersById = await _db.Users.AsNoTracking()
            .Include(u => u.Position)
            .Where(u => approverIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        foreach (var redaction in shown)
        {
            var current = currentHits.FirstOrDefault(h => h.RedactionId == redaction.Id);
            var snapshots = snapshotHits.Where(h => h.RedactionId == redaction.Id).ToList();
            var redactionSignatureHits = signatureHits.Where(h => h.RedactionId == redaction.Id).ToList();

            var language = current?.LanguageFor(fileIds)
                           ?? snapshots.Select(s => s.LanguageFor(fileIds)).FirstOrDefault(l => l is not null)
                           ?? redactionSignatureHits
                               .Select(h => VndAuthenticityMatcher.LanguageOf(h.ContentDigest, sha256))
                               .FirstOrDefault(l => l is not null)
                           ?? "ru";

            var match = new VndAuthenticityRedactionMatch
            {
                VndId = redaction.VndId,
                VndCode = redaction.Vnd!.Code,
                TitleRu = redaction.TitleRu,
                TitleEn = redaction.TitleEn,
                TitleKg = redaction.TitleKg,
                RedactionId = redaction.Id,
                RedactionCode = redaction.Code,
                RedactionNumber = redaction.Number,
                Language = language,
                IsCurrentText = current is not null,
                SnapshotNumbers = snapshots.Select(s => s.SnapshotNumber!.Value).Distinct().Order().ToList(),
            };

            // Процесс относится к этому файлу, если в нём есть подписи под файлом, файл был в нём
            // промежуточной версией или это текущий текст редакции (тогда — все её процессы).
            var relevantProcessIds = redactionSignatureHits.Select(h => h.ApprovalProcessId)
                .Concat(snapshots.Select(s => s.ApprovalProcessId!.Value))
                .ToHashSet();

            foreach (var process in processes.Where(p => p.RedactionId == redaction.Id))
            {
                if (current is null && !relevantProcessIds.Contains(process.Id)) continue;
                match.Processes.Add(BuildProcess(process, signatures, rounds, usersById, sha256));
            }

            response.Matches.Add(match);
        }

        return response;
    }

    private static VndAuthenticityProcess BuildProcess(
        VndApprovalProcess process, IReadOnlyList<VndResolutionSignature> allSignatures,
        IReadOnlyList<VndApprovalPhaseRound> rounds, IReadOnlyDictionary<int, User> usersById, string sha256)
    {
        var processSignatures = allSignatures.Where(s => s.ApprovalProcessId == process.Id).ToList();
        var byStage = processSignatures.GroupBy(s => s.StageId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new VndAuthenticityProcess
        {
            ApprovalProcessId = process.Id,
            Status = VndAuthenticityMatcher.StatusCode(process.Status),
            StartedAt = process.PrimaryStartedAt,
            CompletedAt = process.CompletedAt,
            IsNoChangesActualization = process.IsNoChangesActualization,
        };

        // Как в Листе согласования: убранные главным редактором этапы согласующими не значатся.
        foreach (var stage in process.Stages.Where(s => !s.IsRemovedByEditor).OrderBy(s => s.Order))
        {
            var (phase, decision, decidedAt, _) = ApprovalSheetRowBuilder.Effective(stage);
            var (state, signature) = VndAuthenticityMatcher.SignatureState(
                stage, process.InitiatorUserId,
                byStage.TryGetValue(stage.Id, out var stageSignatures) ? stageSignatures : [],
                sha256);
            usersById.TryGetValue(stage.ApproverUserId, out var user);

            result.Approvers.Add(new VndAuthenticityApprover
            {
                StageTitle = stage.Title,
                FullName = user?.FullName ?? "—",
                Position = user?.Position?.TitleRu,
                Decision = VndAuthenticityMatcher.DecisionCode(decision),
                Phase = VndAuthenticityMatcher.PhaseCode(phase),
                // Подписанное решение — момент подписи; иначе — момент решения по этапу.
                DecidedAt = signature?.SignedAt ?? decidedAt,
                SignatureState = state,
                SignatureId = signature?.Id,
            });
        }

        result.ApproversTotal = result.Approvers.Count;
        result.SignedUnderThisText = result.Approvers.Count(a => a.SignatureState == "signed_this_text");

        result.Signatures = processSignatures
            .Where(s => VndAuthenticityMatcher.LanguageOf(s.ContentDigest, sha256) is not null)
            .OrderBy(s => s.SignedAt)
            .Select(s => VndAuthenticityMatcher.ToSignatureDto(s, sha256))
            .ToList();

        result.History = VndAuthenticityMatcher.BuildHistory(process, rounds, processSignatures, usersById, sha256);

        return result;
    }

    /// <summary>Файл найден среди файлов редакции (текущих — ProcessId/SnapshotNumber null — или
    /// промежуточной версии доработки).</summary>
    private sealed record FileHit(
        int RedactionId, int? ApprovalProcessId, int? SnapshotNumber, int RuFileId, int? KgFileId, int? EnFileId)
    {
        public string? LanguageFor(IReadOnlyCollection<int> fileIds) =>
            fileIds.Contains(RuFileId) ? "ru"
            : KgFileId is { } kg && fileIds.Contains(kg) ? "kg"
            : EnFileId is { } en && fileIds.Contains(en) ? "en"
            : null;
    }
}
