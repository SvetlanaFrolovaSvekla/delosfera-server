using System.Security.Cryptography;
using System.Text;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Три компонента, из которых собирается хэш резолюции (значения — уже нормализованные,
/// ровно в том виде, в каком входят в каноническую строку).</summary>
/// <param name="Comment">Комментарий резолюции (пустой → "Согласовано").</param>
/// <param name="Content">Отпечаток содержимого редакции — SHA-256 файлов текста по языкам,
/// см. <see cref="ResolutionSignatureHasher.BuildContentDigest"/>.</param>
/// <param name="Code">Код ВНД (VndDocument.Code).</param>
public sealed record ResolutionHashComponents(string Comment, string Content, string Code);

/// <summary>
/// Хэш резолюции "Согласовать" для ЭЦП ГП «Инфоком» (SIG-ECP) — ЕДИНСТВЕННОЕ место, где задан
/// его формат. Считается только на backend (ResolutionSignatureService): клиент получает готовый
/// хэш, подписывает его в 2.5 и присылает обратно; при сохранении резолюции backend пересчитывает
/// хэш из актуальных данных БД и сравнивает с присланным — подписать "не тот" документ/текст нельзя.
///
/// Каноническая строка (схема v1), по строке на поле, без неоднозначности склейки — у каждого
/// значения метка и длина в байтах UTF-8:
/// <code>
/// v1
/// comment:{len}:{value}
/// content:{len}:{value}
/// code:{len}:{value}
/// </code>
/// Нормализация: Unicode NFC, переводы строк CRLF/CR → LF; комментарий дополнительно обрезается
/// по краям и, если пуст, заменяется на "Согласовано" (именно этот текст и подписывает
/// согласующий, нажимая "Согласовать" без комментария). Хэш — SHA-256, hex upper-case (как в
/// примерах документации провайдера).
///
/// Версия схемы — первая строка: если формат когда-нибудь поменяется, это будет "v2", а старые
/// подписи продолжат проверяться по своей версии (VndResolutionSignature.HashSchemaVersion).
/// Менять формат v1 задним числом НЕЛЬЗЯ — все сохранённые подписи перестанут сходиться.
/// </summary>
public static class ResolutionSignatureHasher
{
    public const string SchemaVersion = "v1";

    /// <summary>Подставляется вместо пустого комментария резолюции "Согласовать".</summary>
    public const string EmptyCommentPlaceholder = "Согласовано";

    /// <summary>Значение языка без файла в <see cref="BuildContentDigest"/>.</summary>
    public const string MissingFileMarker = "-";

    /// <summary>NFC + CRLF/CR → LF. Без обрезки краёв — для полей, где пробелы значимы.</summary>
    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var lf = value.Replace("\r\n", "\n").Replace('\r', '\n');
        return lf.Normalize(NormalizationForm.FormC);
    }

    /// <summary>Комментарий в том виде, в каком он входит в хэш: NFC, LF, trim, пустой → "Согласовано".</summary>
    public static string NormalizeComment(string? comment)
    {
        var normalized = NormalizeText(comment).Trim();
        return normalized.Length == 0 ? EmptyCommentPlaceholder : normalized;
    }

    /// <summary>Код ВНД: NFC + trim (код — идентификатор, пробелы по краям незначимы).</summary>
    public static string NormalizeCode(string? code) => NormalizeText(code).Trim();

    /// <summary>
    /// Компонент "content": содержимое редакции — это файлы текста (DOC/DOCX) в MinIO, отдельного
    /// текстового поля у редакции нет. Русский файл обязателен, кыргызский/английский — нет;
    /// в отпечаток входят ВСЕ языковые версии, чтобы замена любой из них меняла хэш.
    /// Формат: "ru={HEX};kg={HEX|-};en={HEX|-}", HEX — SHA-256 байтов файла, upper-case.
    /// </summary>
    public static string BuildContentDigest(string ruSha256, string? kgSha256, string? enSha256)
    {
        if (string.IsNullOrWhiteSpace(ruSha256))
            throw new ArgumentException("SHA-256 русской версии документа обязателен", nameof(ruSha256));

        static string Part(string? hex) =>
            string.IsNullOrWhiteSpace(hex) ? MissingFileMarker : hex.Trim().ToUpperInvariant();

        return $"ru={Part(ruSha256)};kg={Part(kgSha256)};en={Part(enSha256)}";
    }

    /// <summary>Собрать компоненты с нормализацией (удобная точка входа для вызывающего кода).</summary>
    public static ResolutionHashComponents CreateComponents(string? comment, string contentDigest, string? vndCode) =>
        new(NormalizeComment(comment), NormalizeText(contentDigest), NormalizeCode(vndCode));

    /// <summary>Каноническая строка схемы v1 (см. описание класса).</summary>
    public static string BuildCanonical(ResolutionHashComponents components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var sb = new StringBuilder();
        sb.Append(SchemaVersion);
        AppendField(sb, "comment", components.Comment);
        AppendField(sb, "content", components.Content);
        AppendField(sb, "code", components.Code);
        return sb.ToString();
    }

    /// <summary>SHA-256 канонической строки, hex upper-case.</summary>
    public static string Compute(ResolutionHashComponents components) =>
        Sha256Hex(BuildCanonical(components));

    /// <summary>SHA-256 строки (UTF-8), hex upper-case.</summary>
    public static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>SHA-256 произвольных байтов, hex upper-case.</summary>
    public static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>Сравнение двух hex-хэшей за постоянное время (без учёта регистра). Пустое/не-hex
    /// значение — всегда несовпадение.</summary>
    public static bool HashesEqual(string? expectedHex, string? actualHex)
    {
        if (string.IsNullOrWhiteSpace(expectedHex) || string.IsNullOrWhiteSpace(actualHex)) return false;

        byte[] a, b;
        try
        {
            a = Convert.FromHexString(expectedHex.Trim());
            b = Convert.FromHexString(actualHex.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        // Длина хэша не секрет (SHA-256 — всегда 32 байта), поэтому ранний выход по длине безопасен.
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static void AppendField(StringBuilder sb, string label, string value)
    {
        var byteLength = Encoding.UTF8.GetByteCount(value);
        sb.Append('\n').Append(label).Append(':').Append(byteLength).Append(':').Append(value);
    }
}
