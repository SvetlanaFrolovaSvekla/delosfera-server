using delosfera_server.Modules.Signing.Models;

namespace delosfera_server.Modules.Signing.Services;

/// <summary>
/// Подписание документов (раздел 7 ТЗ). ПЭП реализована; КЭП/ТУМАР — адаптер (Ф7).
/// </summary>
public interface ISignatureService
{
    Task<Signature> SignAsync(int documentAttachmentId, SignatureLevel level, int userId, string? stampMeta = null);

    /// <summary>Аннулировать подписи вложения при изменении файла (SIG-01).</summary>
    Task RevokeForAttachmentAsync(int documentAttachmentId, string reason);
}
