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
/// <see cref="PermissionCode.ViewOtherUsersDrafts"/>).
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
        return await _db.Set<VndApprovalStageAttachment>()
            .Where(a => a.FileAttachmentId == fileId)
            .AnyAsync(a => a.VndApprovalStage!.ApprovalProcess!.InitiatorUserId == userId
                           || a.VndApprovalStage.ApprovalProcess.Stages.Any(s => s.ApproverUserId == userId)
                           || canViewOtherDrafts, ct);
    }
}
