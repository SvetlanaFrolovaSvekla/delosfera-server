using System.Globalization;
using System.Text;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Workflow.Models;

namespace delosfera_server.Modules.Sz.Services;

/// <summary>Компоненты хэша резолюции по служебной записке — уже нормализованные, ровно в том виде,
/// в каком входят в каноническую строку.</summary>
/// <param name="Decision">"approved" / "approved_with_remarks" / "rejected".</param>
/// <param name="Comment">Комментарий резолюции (пустой → "Согласовано").</param>
/// <param name="Content">Отпечаток содержимого записки, см. <see cref="SzResolutionSignatureHasher.BuildContentDigest"/>.</param>
/// <param name="DocumentId">Карточка документа записки — подпись нельзя перенести на другую записку
/// с тем же текстом.</param>
public sealed record SzResolutionHashComponents(string Decision, string Comment, string Content, string DocumentId);

/// <summary>Текущий отпечаток содержимого записки по действующей схеме и по прежней (для старых подписей).</summary>
public sealed record SzContentDigests(string Current, string Legacy);

/// <summary>Материал отпечатка содержимого записки — существенные поля карточки и хэши вложений
/// (вложения входят только в прежнюю схему sz-content-v1).</summary>
public sealed record SzContentMaterial(
    int DocumentId,
    string? RegNumber,
    string Title,
    int KindId,
    int? HrKindId,
    int? AddresseeUserId,
    int? CorrespondentUnitId,
    string? Body,
    string? EmployeeName,
    int? EmployeeUnitId,
    int? TransferUnitId,
    decimal? Amount,
    bool? TravelExpenses,
    string? ExtraFieldsJson,
    IReadOnlyList<(int Id, string? Hash)> Attachments);

/// <summary>
/// Хэш резолюции согласующего служебной записки для ЭЦП ГП «Инфоком» — ЕДИНСТВЕННОЕ место, где задан
/// его формат (аналог ResolutionSignatureHasher у ВНД). Считается только на backend: клиент получает
/// готовый хэш, подписывает его в 2.5 и присылает обратно; при сохранении резолюции backend
/// пересчитывает хэш из актуальных данных БД и сравнивает с присланным.
///
/// Каноническая строка — по строке на поле, у каждого значения метка и длина в байтах UTF-8:
/// <code>
/// sz-v1
/// decision:{len}:{approved|approved_with_remarks|rejected}
/// comment:{len}:{value}
/// content:{len}:{value}
/// document:{len}:{id}
/// </code>
/// Менять формат sz-v1 задним числом НЕЛЬЗЯ — сохранённые подписи перестанут сходиться; новый
/// формат — новая версия схемы.
/// </summary>
public static class SzResolutionSignatureHasher
{
    public const string SchemaVersion = "sz-v1";

    /// <summary>Версия материала отпечатка содержимого (первая строка материала).
    ///
    /// sz-content-v2 — без вложений: подпись согласующего удостоверяет текст записки и её
    /// реквизиты (вид, адресат, суммы, кадровые поля и т.п.). Вложения и маршрут (согласующие,
    /// подписант) правятся по ходу согласования, не сбрасывая подписей и не отправляя записку
    /// на новый круг. Подписи, поставленные под sz-content-v1 (со вложениями), сверяются по
    /// своей версии — см. <see cref="BuildLegacyContentDigest"/>.</summary>
    public const string ContentSchemaVersion = "sz-content-v2";

    /// <summary>Прежняя версия материала — со вложениями. Только для сверки старых подписей.</summary>
    public const string LegacyContentSchemaVersion = "sz-content-v1";

    public static string DecisionCode(ResolutionType decision) => decision switch
    {
        ResolutionType.Approved => "approved",
        ResolutionType.ApprovedWithRemarks => "approved_with_remarks",
        ResolutionType.Rejected => "rejected",
        _ => throw new InvalidOperationException("Для этого решения подпись ЭЦП не предусмотрена"),
    };

    /// <summary>Подписываются только решения согласующего.</summary>
    public static bool IsSignable(ResolutionType decision) =>
        decision is ResolutionType.Approved or ResolutionType.ApprovedWithRemarks or ResolutionType.Rejected;

    public static SzResolutionHashComponents CreateComponents(
        ResolutionType decision, string? comment, string contentDigest, int documentId) =>
        new(DecisionCode(decision),
            ResolutionSignatureHasher.NormalizeComment(comment),
            ResolutionSignatureHasher.NormalizeText(contentDigest).Trim(),
            documentId.ToString(CultureInfo.InvariantCulture));

    public static string BuildCanonical(SzResolutionHashComponents c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var sb = new StringBuilder(SchemaVersion);
        AppendField(sb, "decision", c.Decision);
        AppendField(sb, "comment", c.Comment);
        AppendField(sb, "content", c.Content);
        AppendField(sb, "document", c.DocumentId);
        return sb.ToString();
    }

    /// <summary>SHA-256 канонической строки, hex upper-case.</summary>
    public static string Compute(SzResolutionHashComponents c) =>
        ResolutionSignatureHasher.Sha256Hex(BuildCanonical(c));

    /// <summary>
    /// Отпечаток содержимого записки (sz-content-v2): SHA-256 (hex upper-case) материала из
    /// текста и существенных полей карточки. В материал НЕ входят вложения, маршрут, статус,
    /// даты, бюджетная отметка УЧР и прочее, что меняется по ходу маршрута: иначе подпись
    /// первого согласующего "устаревала" бы от действий следующего, хотя текст записки никто
    /// не трогал.
    /// </summary>
    public static string BuildContentDigest(SzContentMaterial m) =>
        BuildDigest(m, ContentSchemaVersion, withAttachments: false);

    /// <summary>Отпечаток по прежней схеме sz-content-v1 (со вложениями) — чтобы подписи,
    /// поставленные до перехода на v2, сверялись честно. Новые подписи под ним не ставятся.</summary>
    public static string BuildLegacyContentDigest(SzContentMaterial m) =>
        BuildDigest(m, LegacyContentSchemaVersion, withAttachments: true);

    /// <summary>Совпадает ли отпечаток, сохранённый при подписи, с текущим содержимым — по
    /// любой из поддерживаемых схем.</summary>
    public static bool DigestMatches(string? stored, SzContentDigests current) =>
        !string.IsNullOrWhiteSpace(stored)
        && (string.Equals(stored, current.Current, StringComparison.OrdinalIgnoreCase)
            || string.Equals(stored, current.Legacy, StringComparison.OrdinalIgnoreCase));

    private static string BuildDigest(SzContentMaterial m, string version, bool withAttachments)
    {
        ArgumentNullException.ThrowIfNull(m);
        static string N(string? v) => ResolutionSignatureHasher.NormalizeText(v);
        static string I(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "-";

        var sb = new StringBuilder(version);
        AppendField(sb, "document", m.DocumentId.ToString(CultureInfo.InvariantCulture));
        AppendField(sb, "reg", N(m.RegNumber).Trim());
        AppendField(sb, "title", N(m.Title).Trim());
        AppendField(sb, "kind", I(m.KindId));
        AppendField(sb, "hrkind", I(m.HrKindId));
        AppendField(sb, "addressee", I(m.AddresseeUserId));
        AppendField(sb, "correspondent", I(m.CorrespondentUnitId));
        AppendField(sb, "body", N(m.Body));
        AppendField(sb, "employee", N(m.EmployeeName).Trim());
        AppendField(sb, "employee_unit", I(m.EmployeeUnitId));
        AppendField(sb, "transfer_unit", I(m.TransferUnitId));
        // Сумма из numeric может прийти как 100 или 100.00 — нормализуем незначащие нули.
        AppendField(sb, "amount", m.Amount?.ToString("0.############", CultureInfo.InvariantCulture) ?? "-");
        AppendField(sb, "travel", m.TravelExpenses is { } t ? (t ? "1" : "0") : "-");
        AppendField(sb, "extra", N(m.ExtraFieldsJson));
        if (withAttachments)
            AppendField(sb, "attachments", string.Join(";",
                m.Attachments.OrderBy(a => a.Id).Select(a =>
                    $"{a.Id.ToString(CultureInfo.InvariantCulture)}:{(a.Hash ?? "").Trim().ToUpperInvariant()}")));

        return ResolutionSignatureHasher.Sha256Hex(sb.ToString());
    }

    private static void AppendField(StringBuilder sb, string label, string value)
    {
        var byteLength = Encoding.UTF8.GetByteCount(value);
        sb.Append('\n').Append(label).Append(':').Append(byteLength).Append(':').Append(value);
    }
}
