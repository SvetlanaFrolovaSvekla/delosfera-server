using Microsoft.EntityFrameworkCore;
using delosfera_server.Common.Services;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Models;
using delosfera_server.Modules.Files.Services;
using delosfera_server.Modules.Users.Models;
using delosfera_server.Common.Services.Authorization;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>
/// Правила доступа к файлам ВНД. Пользователь может получить файл, если:
/// он его загрузил, либо файл привязан к редакции ВНД, который пользователю виден
/// (опубликованные документы видны всем; чужие черновики — только с правом
/// <see cref="PermissionCode.ViewOtherUsersDrafts"/>), либо файл приложен к системному
/// уведомлению (см. Notification.AttachmentFileId), которое адресовано этому пользователю —
/// например, Excel-план единоразовой рассылки (см.
/// ActualizationNotificationService.SendOneTimeMailingAsync).
/// Закрывает IDOR: раньше любой аутентифицированный пользователь мог скачать любой
/// файл по порядковому id.
/// </summary>
public class VndFileAccessAuthorizer : IFileAccessAuthorizer
{
    private readonly DelosferaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public VndFileAccessAuthorizer(DelosferaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<bool> CanCurrentUserAccessAsync(int fileId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;

        // Загрузивший файл всегда имеет к нему доступ (например, вложения ещё не привязанного черновика).
        var isUploader = await _db.FileAttachments
            .AnyAsync(f => f.Id == fileId && f.UploadedByUserId == userId, ct);
        if (isUploader) return true;

        var canViewOtherDrafts = _currentUser.HasPermission(PermissionCode.ViewOtherUsersDrafts);

        var canAccessRedactionFile = await _db.VndRedactions
            .Where(r => r.DocFileRuId == fileId
                        || r.DocFileKgId == fileId
                        || r.DocFileEnId == fileId
                        || r.TidFileId == fileId
                        // Лист согласования (в т.ч. все прежние листы редакции - см.
                        // VndRedactionApprovalSheet) и матрица разногласий - такие же файлы
                        // редакции, их показывают в блоке "Специальные вложения". Раньше они сюда
                        // не входили, и скачать их мог только тот, кто их сформировал/загрузил
                        // (для автоматического листа - инициатор согласования).
                        || r.ApprovalSheetFileId == fileId
                        || r.DisagreementMatrixFileId == fileId
                        || r.ApprovalSheets.Any(s => s.FileAttachmentId == fileId)
                        || r.Attachments.Any(a => a.FileAttachmentId == fileId))
            .AnyAsync(r => r.Vnd!.Status != VndStatus.Draft
                           || canViewOtherDrafts
                           || r.Vnd.CreatedByUserId == userId, ct);
        if (canAccessRedactionFile) return true;

        // Вложения к резолюциям согласующих (VndApprovalStageAttachment): доступны инициатору
        // процесса и всем согласующим на маршруте этого же процесса — пока идёт согласование,
        // им нужно видеть, что именно приложили друг другу. Как только редакция становится
        // согласованной, вложения физически удаляются (см. VndApprovalService.CleanupStageAttachmentsAsync),
        // так что этот доступ актуален лишь на время самого согласования.
        var canAccessApprovalAttachment = await _db.Set<VndApprovalStageAttachment>()
            .Where(a => a.FileAttachmentId == fileId)
            .AnyAsync(a => a.VndApprovalStage!.ApprovalProcess!.InitiatorUserId == userId
                           || a.VndApprovalStage.ApprovalProcess.Stages.Any(s => s.ApproverUserId == userId)
                           || canViewOtherDrafts, ct);
        if (canAccessApprovalAttachment) return true;

        // Снимки файлов редакции по кругам согласования (VndRedactionRevisionSnapshot) -
        // в отличие от вложений к резолюциям выше, остаются доступны БЕССРОЧНО, в т.ч. после
        // завершения согласования - это часть истории версий документа, к ней нужно
        // возвращаться и после того, как ВНД стал действующим. Доступ - тем же, кому виден сам
        // ВНД/редакция (см. canAccessRedactionFile выше), плюс инициатору и согласующим
        // процесса, которому принадлежит снимок, даже пока сам ВНД ещё черновик/на актуализации.
        var canAccessRedactionSnapshot = await _db.Set<VndRedactionRevisionSnapshot>()
            .Where(s => s.DocFileRuId == fileId
                        || s.DocFileKgId == fileId
                        || s.DocFileEnId == fileId
                        || s.TidFileId == fileId
                        || s.DisagreementMatrixFileId == fileId)
            .AnyAsync(s => s.ApprovalProcess!.InitiatorUserId == userId
                           || s.ApprovalProcess.Stages.Any(st => st.ApproverUserId == userId)
                           || s.VndRedaction!.Vnd!.Status != VndStatus.Draft
                           || canViewOtherDrafts
                           || s.VndRedaction.Vnd.CreatedByUserId == userId, ct);
        if (canAccessRedactionSnapshot) return true;

        // Вложения к предложениям по ВНД (VndProposalAttachment): автору доступны как
        // загрузившему (см. isUploader выше), получателям предложений - по праву
        // ManageVndProposals (главный редактор ВНД).
        if (_currentUser.HasPermission(PermissionCode.ManageVndProposals))
        {
            var isProposalAttachment = await _db.VndProposalAttachments
                .AnyAsync(a => a.FileAttachmentId == fileId, ct);
            if (isProposalAttachment) return true;
        }

        // Файл приложен к системному уведомлению (Notification.AttachmentFileId) — например,
        // Excel-план единоразовой рассылки актуализации. Доступ только фактическим получателям
        // этого уведомления (через UserNotification), не всем подряд.
        return await _db.UserNotifications
            .Where(un => un.UserId == userId)
            .AnyAsync(un => un.Notification!.AttachmentFileId == fileId, ct);
    }
}
