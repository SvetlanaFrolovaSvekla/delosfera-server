using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;

namespace delosfera_server.Modules.Documents.Services;

public interface IDocumentFingerprintService
{
    /// <summary>Отпечаток карточки на текущий момент — то, что фиксирует подпись.</summary>
    Task<string> ComputeAsync(int documentId, CancellationToken ct = default);
}

/// <summary>
/// Отпечаток документа для простой электронной подписи (SIG-01).
///
/// Подпись должна отвечать на вопрос «что именно подписано», иначе виза сводится
/// к отметке в журнале. У вложения такой ответ есть — хеш версии файла. У карточки
/// его не было: текст записки, адресат и срок живут в полях, а не в файле.
///
/// Поэтому отпечаток собирается из существенных реквизитов карточки и хешей всех
/// её вложений. Правка текста, замена файла или подмена адресата меняют отпечаток,
/// и расхождение с сохранённым в подписи становится видно сразу.
///
/// Оформление, порядок сортировки и служебные метки в свёртку не идут: подпись
/// должна пережить переименование поля в интерфейсе, но не подмену содержания.
/// </summary>
public class DocumentFingerprintService : IDocumentFingerprintService
{
    private readonly DelosferaDbContext _db;

    public DocumentFingerprintService(DelosferaDbContext db) => _db = db;

    public async Task<string> ComputeAsync(int documentId, CancellationToken ct = default)
    {
        var document = await _db.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new
            {
                d.Id,
                d.Type,
                d.RegNumber,
                d.Title,
                d.StatusCode,
                d.AuthorId,
                d.ConcurrencyToken,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Документ {documentId} не найден");

        // Вложения берём по возрастанию идентификатора: порядок выдачи из базы
        // не гарантирован, а отпечаток обязан быть воспроизводимым.
        var attachments = await _db.DocumentAttachments
            .AsNoTracking()
            .Where(a => a.DocumentId == documentId)
            .OrderBy(a => a.Id)
            .Select(a => $"{a.Id}:{a.Hash}")
            .ToListAsync(ct);

        var material = string.Join("|", new[]
        {
            document.Id.ToString(),
            document.Type.ToString(),
            document.RegNumber ?? string.Empty,
            document.Title,
            document.StatusCode,
            document.AuthorId.ToString(),
            document.ConcurrencyToken.ToString(),
            string.Join(";", attachments),
        });

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}
