using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.Services;
using delosfera_server.Modules.Signing.Models;

namespace delosfera_server.Modules.Signing.Services;

public class SignatureService : ISignatureService
{
    private readonly DelosferaDbContext _db;
    private readonly IAuditService _audit;

    public SignatureService(DelosferaDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Signature> SignAsync(int documentAttachmentId, SignatureLevel level, int userId, string? stampMeta = null)
    {
        // ПЭП: фиксируем факт подписи хеша версии. КЭП/ТУМАР — внешний адаптер (Ф7),
        // здесь та же запись; проверка криптопровайдера добавляется в интеграциях.
        var sig = new Signature
        {
            DocumentAttachmentId = documentAttachmentId,
            UserId = userId,
            Level = level,
            At = DateTime.UtcNow,
            StampMeta = stampMeta,
            Revoked = false
        };
        _db.Signatures.Add(sig);
        await _db.SaveChangesAsync();

        await _audit.LogAsync("DocumentAttachment", documentAttachmentId, "Signed", userId,
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
}
