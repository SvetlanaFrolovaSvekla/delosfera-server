using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Common.Services.Authorization;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Строки Листа согласования из этапов процесса: фактическое (последнее принятое) решение
/// каждого согласующего и, в режиме с подписями, штамп ЭЦП из журнала VndResolutionSignature.
/// Чистая функция — используется и при просмотре/скачивании листа (ApprovalSheetService), и при
/// сохранении листа в момент завершения согласования (VndApprovalService.GenerateApprovalSheetAsync).</summary>
public static class ApprovalSheetRowBuilder
{
    /// <summary>Решения, проставленные системой, а не человеком: инициатор-согласующий
    /// (VndApprovalService.InitiatorAutoApprovedComment) и перенос чистого согласования на финальную
    /// выдержку (VndApprovalService.ResetFinalHoldDecisionsAsync) — оба текста начинаются так.</summary>
    private const string AutoCommentPrefix = "Согласовано автоматически";

    public static List<ApprovalSheetRow> Build(
        VndApprovalProcess process,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<int, List<VndResolutionSignature>>? signaturesByStage,
        TimeZoneInfo zone)
    {
        var rows = new List<ApprovalSheetRow>();

        // Убранные главным редактором этапы не значатся согласующими.
        foreach (var stage in process.Stages.Where(s => !s.IsRemovedByEditor).OrderBy(s => s.Order))
        {
            usersById.TryGetValue(stage.ApproverUserId, out var user);
            var (phase, decision, decidedAt, comment) = Effective(stage);
            var auto = comment?.StartsWith(AutoCommentPrefix, StringComparison.Ordinal) == true;

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
                // Подпись под ЭТИМ решением: то же решение и та же фаза (для перенесённого на финальную
                // выдержку согласования — подпись под исходным "Согласовать" из более ранней фазы).
                var signature = signaturesByStage.TryGetValue(stage.Id, out var stageSignatures)
                    ? stageSignatures
                        .Where(s => s.Decision == decision && (s.Phase == phase || auto))
                        .OrderByDescending(s => s.SignedAt)
                        .FirstOrDefault()
                    : null;
                if (signature is not null)
                {
                    stamp = new ApprovalSheetStamp(
                        signature.PersonFio ?? signature.CertCommonName ?? user?.FullName,
                        signature.OrganizationName,
                        signature.CertValidNotAfter,
                        signature.CertIssuerCommonName,
                        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(signature.SignedAt, DateTimeKind.Utc), zone),
                        signature.Hash,
                        signature.SignatureSha256);
                }
                else
                {
                    signatureNote = decision switch
                    {
                        ApprovalStageDecision.Pending => "—",
                        ApprovalStageDecision.Approved when auto => "Без ЭЦП — согласовано автоматически",
                        ApprovalStageDecision.AutoApprovedByTimeout => "Без ЭЦП — засчитано по истечении срока",
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

    /// <summary>Последнее решение по этапу: финальная выдержка → повторное согласование → первичное
    /// (то же правило, что VndApprovalService.LatestDecisionBeforeFinalHold).</summary>
    private static (ApprovalStagePhase Phase, ApprovalStageDecision Decision, DateTime? At, string? Comment)
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
        ApprovalStageDecision.AutoApprovedByTimeout => "Согласовано (истёк срок)",
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

    public ApprovalSheetService(DelosferaDbContext db, IApprovalSheetGenerator generator,
        ICurrentUserService currentUser, IBankClock clock)
    {
        _db = db;
        _generator = generator;
        _currentUser = currentUser;
        _clock = clock;
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
        if (mode == ApprovalSheetMode.Signed)
        {
            var all = await _db.VndResolutionSignatures.AsNoTracking()
                .Where(s => s.ApprovalProcessId == process.Id)
                .ToListAsync(ct);
            signatures = all
                .GroupBy(s => s.StageId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        var rows = ApprovalSheetRowBuilder.Build(process, usersById, signatures, _clock.Zone);

        var finished = process.Status is ApprovalProcessStatus.Approved
            or ApprovalProcessStatus.Rejected or ApprovalProcessStatus.Cancelled;
        var note = $"Состояние на {_clock.Now:dd.MM.yyyy HH:mm}" +
                   (finished ? "" : " · согласование не завершено, лист промежуточный") +
                   (process.IsNoChangesActualization ? " · актуализация без изменений" : "");

        var content = _generator.Generate(process.Vnd!.TitleRu, rows, mode, note);
        var code = process.Redaction?.Code ?? process.Vnd.Code;
        var fileName = mode == ApprovalSheetMode.Signed
            ? $"{code}_Лист_согласования_ЭЦП.docx"
            : $"{code}_Лист_согласования.docx";
        return (content, fileName);
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
