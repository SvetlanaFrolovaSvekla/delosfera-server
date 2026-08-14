using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Signing.Models;

namespace delosfera_server.Modules.Signing.Services;

public class SignatureService : ISignatureService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;
    private readonly IDocumentFingerprintService _fingerprints;

    public SignatureService(
        DelosferaDbContext db, IAuditService audit, IDocumentFingerprintService fingerprints)
    {
        _db = db;
        _audit = audit;
        _fingerprints = fingerprints;
    }

    public async Task<Signature> SignAsync(int documentAttachmentId, SignatureLevel level, int userId, string? stampMeta = null)
    {
        var hash = await _db.DocumentAttachments
            .Where(a => a.Id == documentAttachmentId)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync();

        // ПЭП: фиксируем факт подписи хеша версии. КЭП/ТУМАР — внешний адаптер (Ф7),
        // здесь та же запись; проверка криптопровайдера добавляется в интеграциях.
        var sig = new Signature
        {
            DocumentAttachmentId = documentAttachmentId,
            ContentHash = hash,
            UserId = userId,
            Level = level,
            At = DateTime.UtcNow,
            StampMeta = stampMeta ?? await BuildStampAsync(userId, level),
            Revoked = false
        };
        _db.Signatures.Add(sig);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("DocumentAttachment", documentAttachmentId, "Signed", userId,
            new { level = level.ToString(), signatureId = sig.Id });
        return sig;
    }

    /// <summary>
    /// Подпись карточки целиком. Нужна там, где подписывать нечего файлом: текст
    /// служебной записки, адресат и срок живут в полях, и виза согласующего должна
    /// фиксировать именно их состояние на момент решения.
    /// </summary>
    public async Task<Signature> SignDocumentAsync(
        int documentId, SignatureLevel level, int userId, string? stampMeta = null)
    {
        var sig = new Signature
        {
            DocumentId = documentId,
            ContentHash = await _fingerprints.ComputeAsync(documentId),
            UserId = userId,
            Level = level,
            At = DateTime.UtcNow,
            StampMeta = stampMeta ?? await BuildStampAsync(userId, level),
            Revoked = false
        };
        _db.Signatures.Add(sig);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("Document", documentId, "Signed", userId,
            new { level = level.ToString(), signatureId = sig.Id });
        return sig;
    }

    public async Task RevokeForAttachmentAsync(int documentAttachmentId, string reason)
    {
        var sigs = await _db.Signatures
            .Where(s => s.DocumentAttachmentId == documentAttachmentId && !s.Revoked)
            .ToListAsync();

        foreach (var s in sigs)
        {
            s.Revoked = true;
            s.RevokedReason = reason;
        }
        if (sigs.Count > 0)
        {
            await _db.SaveChangesAsync();
            await _audit.LogAsync("DocumentAttachment", documentAttachmentId, "SignaturesRevoked", null, new { reason });
        }
    }

    /// <summary>
    /// Реквизиты для визуального штампа. Сохраняются в самой подписи, а не берутся
    /// из справочника при показе: человек может сменить должность или уйти из банка,
    /// а штамп обязан остаться таким, каким был в момент подписания.
    /// </summary>
    private async Task<string> BuildStampAsync(int userId, SignatureLevel level)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.FullName,
                Position = u.Position != null ? u.Position.TitleRu : null,
            })
            .FirstOrDefaultAsync();

        return JsonSerializer.Serialize(new
        {
            fullName = user?.FullName,
            position = user?.Position,
            levelTitle = level == SignatureLevel.Qualified
                ? "Квалифицированная электронная подпись"
                : "Простая электронная подпись",
        });
    }
}
