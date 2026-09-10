namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Извлекает "легаси"-гиперссылки вида db://documents/{code} и db://attachments/{n},
/// унаследованные из старой системы (isrib). Там вложения и связанные документы были прошиты
/// прямо в тело Word-файла такими URI-подобными гиперссылками — в исходной системе они
/// разворачивались в "открыть документ/вложение №N", а в делосфера/браузере ведут в никуда
/// (см. обсуждение с Пупуриком: документ 7985/ред5, ссылки db://documents/8041 и
/// db://attachments/1 открывали пустую страницу).</summary>
public interface IDocxLegacyLinkExtractor
{
    /// <summary>Разбирает .docx (это zip-архив) и возвращает все найденные легаси-ссылки:
    /// на другие документы (по коду vnd_document.code) и на вложения ЭТОГО ЖЕ документа
    /// (по порядковому номеру из ссылки, 1-based — см. LegacyLinkReferences.AttachmentIndexes).
    /// Не бросает исключений на битом/нестандартном файле или недоступном потоке — в этом случае
    /// просто ничего не находит (см. LegacyLinkReferences.Empty), чтобы не ронять показ вкладки
    /// "Связи" из-за одного проблемного файла.</summary>
    LegacyLinkReferences Extract(Stream docxStream);
}

/// <summary>DocumentCodes — уникальные коды документов (vnd_document.code) из
/// db://documents/{code}. AttachmentIndexes — уникальные 1-based порядковые номера из
/// db://attachments/{n}, отсортированные по возрастанию.</summary>
public record LegacyLinkReferences(IReadOnlyList<string> DocumentCodes, IReadOnlyList<int> AttachmentIndexes)
{
    public static readonly LegacyLinkReferences Empty = new([], []);
}
