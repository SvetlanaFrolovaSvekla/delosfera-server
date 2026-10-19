using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.DTO.Request;
using delosfera_server.Modules.Files.Services;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Ошибка сборки, которую нужно показать пользователю как есть (400): неподдерживаемый
/// формат, файл не из этого ВНД, пустой список и т.п.</summary>
public class VndPrintBundleException(string message) : Exception(message);

/// <summary>
/// "Сформировать документ для печати" на странице ВНД: склеивает выбранные файлы редакции
/// (тексты RU/KG/EN, ТИД, листы согласования, матрицу разногласий, вложения) в ОДИН .docx в
/// порядке, который пользователь выставил в модалке (см. VndPrintBundleModal на фронте).
/// </summary>
public interface IVndPrintBundleService
{
    Task<(byte[] Content, string FileName)> BuildAsync(int vndId, VndPrintBundleRequest request, CancellationToken ct = default);
}

public class VndPrintBundleService : IVndPrintBundleService
{
    /// <summary>Сколько файлов можно склеить за раз — защита от случайной "сборки всего".</summary>
    private const int MaxFiles = 60;

    /// <summary>Суммарный объём исходных файлов (сборка идёт в памяти).</summary>
    private const long MaxTotalBytes = 200L * 1024 * 1024;

    private readonly DelosferaDbContext _db;
    private readonly IFileStorageService _files;
    private readonly IFileAccessAuthorizer _access;

    public VndPrintBundleService(DelosferaDbContext db, IFileStorageService files, IFileAccessAuthorizer access)
    {
        _db = db;
        _files = files;
        _access = access;
    }

    public async Task<(byte[] Content, string FileName)> BuildAsync(
        int vndId, VndPrintBundleRequest request, CancellationToken ct = default)
    {
        var fileIds = (request.FileIds ?? []).Distinct().ToList();
        if (fileIds.Count == 0)
            throw new VndPrintBundleException("Не выбрано ни одного файла");
        if (fileIds.Count > MaxFiles)
            throw new VndPrintBundleException($"За один раз можно собрать не больше {MaxFiles} файлов");

        var vndCode = await _db.VndDocuments.AsNoTracking()
            .Where(x => x.Id == vndId)
            .Select(x => x.Code)
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"ВНД с id={vndId} не найден");

        // Все файлы всех редакций этого ВНД — чтобы через эндпоинт нельзя было подсунуть чужой файл
        // (права на сами файлы проверяются ниже отдельно, тем же авторизатором, что и /api/files).
        var redactions = await _db.VndRedactions.AsNoTracking()
            .Where(r => r.VndId == vndId)
            .Select(r => new
            {
                r.Id,
                r.Code,
                r.DocFileRuId,
                r.DocFileKgId,
                r.DocFileEnId,
                r.TidFileId,
                r.ApprovalSheetFileId,
                r.DisagreementMatrixFileId,
                r.TitlePageFileId,
                Sheets = r.ApprovalSheets.Select(s => s.FileAttachmentId).ToList(),
                Attachments = r.Attachments.Select(a => a.FileAttachmentId).ToList(),
            })
            .ToListAsync(ct);

        var vndFileIds = new HashSet<int>();
        foreach (var r in redactions)
        {
            vndFileIds.Add(r.DocFileRuId);
            if (r.DocFileKgId is { } kg) vndFileIds.Add(kg);
            if (r.DocFileEnId is { } en) vndFileIds.Add(en);
            if (r.TidFileId is { } tid) vndFileIds.Add(tid);
            if (r.ApprovalSheetFileId is { } sheet) vndFileIds.Add(sheet);
            if (r.DisagreementMatrixFileId is { } matrix) vndFileIds.Add(matrix);
            if (r.TitlePageFileId is { } titlePage) vndFileIds.Add(titlePage);
            vndFileIds.UnionWith(r.Sheets);
            vndFileIds.UnionWith(r.Attachments);
        }

        if (fileIds.Any(id => !vndFileIds.Contains(id)))
            throw new VndPrintBundleException("Часть выбранных файлов не относится к этому документу");

        foreach (var id in fileIds)
        {
            if (!await _access.CanCurrentUserAccessAsync(id, ct) || !await _access.CanCurrentUserExportAsync(id, ct))
                throw new UnauthorizedAccessException("Нет права на скачивание документов ВНД (право «Экспорт ВНД»)");
        }

        // Скачиваем и определяем формат по содержимому, а не по расширению.
        var sources = new List<BundleSource>(fileIds.Count);
        var unsupported = new List<string>();
        long total = 0;
        foreach (var id in fileIds)
        {
            var (stream, _, fileName) = await _files.DownloadAsync(id, ct);
            byte[] bytes;
            await using (stream)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }

            total += bytes.LongLength;
            if (total > MaxTotalBytes)
                throw new VndPrintBundleException("Выбранные файлы слишком большие для сборки в один документ");

            var kind = DetectKind(bytes);
            if (kind == BundleSourceKind.Unsupported) unsupported.Add(fileName);
            else sources.Add(new BundleSource(kind, bytes));
        }

        if (unsupported.Count > 0)
            throw new VndPrintBundleException(
                "В один Word-документ можно вставить только файлы .docx и изображения (PNG, JPEG). " +
                $"Уберите из списка: {string.Join(", ", unsupported)}");

        var content = DocxBundleBuilder.Build(sources);

        var redactionCode = request.RedactionId is { } rid
            ? redactions.FirstOrDefault(r => r.Id == rid)?.Code
            : null;
        var name = SanitizeFileName(redactionCode is null
            ? $"{vndCode}_для_печати.docx"
            : $"{redactionCode}_для_печати.docx");

        return (content, name);
    }

    private static BundleSourceKind DetectKind(byte[] bytes)
    {
        if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return BundleSourceKind.Png;
        if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return BundleSourceKind.Jpeg;

        // .docx — это ZIP (PK\3\4) с word/document.xml. Старый бинарный .doc и прочие форматы
        // (PDF, Excel, PowerPoint) altChunk-ом в Word не вставляются.
        if (bytes.Length > 4 && bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04)
        {
            try
            {
                using var ms = new MemoryStream(bytes, writable: false);
                using var doc = WordprocessingDocument.Open(ms, false);
                if (doc.MainDocumentPart?.Document?.Body is not null) return BundleSourceKind.Docx;
            }
            catch (Exception)
            {
                // не Word-документ (xlsx/pptx/битый архив)
            }
        }

        return BundleSourceKind.Unsupported;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['\\', '/', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
    }
}

internal enum BundleSourceKind
{
    Docx,
    Png,
    Jpeg,
    Unsupported,
}

internal sealed record BundleSource(BundleSourceKind Kind, byte[] Bytes);

/// <summary>
/// Склейка в один .docx. Каждый Word-файл вставляется целиком через altChunk (Word сам
/// импортирует его со своими стилями, нумерацией, таблицами и картинками при открытии) и
/// заканчивается разрывом раздела "со следующей страницы" с параметрами страницы исходного файла
/// — так каждый документ начинается с новой страницы и сохраняет свою ориентацию и поля
/// (например, альбомный лист согласования). Изображения — по одному на страницу, вписаны в поля.
/// </summary>
internal static class DocxBundleBuilder
{
    // A4 книжная, поля 2 см — для изображений и для файлов без собственных параметров страницы.
    private const uint A4Width = 11906;
    private const uint A4Height = 16838;
    private const int Margin = 1134;

    private const long EmuPerTwip = 635;
    private const long EmuPerPixel = 9525; // 96 dpi

    public static byte[] Build(IReadOnlyList<BundleSource> sources)
    {
        using var output = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            var body = main.Document.Body!;

            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                SectionProperties sectPr;

                if (source.Kind == BundleSourceKind.Docx)
                {
                    var chunkId = $"printBundleChunk{i + 1}";
                    var chunk = main.AddAlternativeFormatImportPart(AlternativeFormatImportPartType.WordprocessingML, chunkId);
                    using (var data = new MemoryStream(source.Bytes, writable: false))
                        chunk.FeedData(data);

                    body.Append(new AltChunk { Id = chunkId });
                    sectPr = ReadSectionProperties(source.Bytes) ?? DefaultSectionProperties();
                }
                else
                {
                    body.Append(BuildImageParagraph(main, source, i + 1));
                    sectPr = DefaultSectionProperties();
                }

                if (i < sources.Count - 1)
                {
                    // Разрыв раздела: параграф с sectPr закрывает раздел с этим документом.
                    body.Append(new Paragraph(new ParagraphProperties(sectPr)));
                }
                else
                {
                    // Параметры последнего раздела — последним элементом body.
                    body.Append(sectPr);
                }
            }

            main.Document.Save();
        }

        return output.ToArray();
    }

    /// <summary>Параметры страницы исходного документа (последний раздел) без ссылок на его
    /// колонтитулы и настройки принтера — их части в итоговом пакете нет, ссылка на них сломала бы файл.</summary>
    private static SectionProperties? ReadSectionProperties(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var src = WordprocessingDocument.Open(ms, false);
            var original = src.MainDocumentPart?.Document?.Body?.Elements<SectionProperties>().LastOrDefault();
            if (original is null) return null;

            var clone = (SectionProperties)original.CloneNode(true);
            foreach (var el in clone.Elements<HeaderReference>().ToList()) el.Remove();
            foreach (var el in clone.Elements<FooterReference>().ToList()) el.Remove();
            foreach (var el in clone.Elements<PrinterSettingsReference>().ToList()) el.Remove();
            // Тип раздела по умолчанию — "со следующей страницы"; "непрерывный" из исходника не берём.
            foreach (var el in clone.Elements<SectionType>().ToList()) el.Remove();
            clone.RsidR = null;
            clone.RsidRPr = null;
            clone.RsidDel = null;
            clone.RsidSect = null;
            return clone;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static SectionProperties DefaultSectionProperties() =>
        new(
            new PageSize { Width = A4Width, Height = A4Height },
            new PageMargin
            {
                Top = Margin, Bottom = Margin, Left = (uint)Margin, Right = (uint)Margin,
                Header = 709U, Footer = 709U, Gutter = 0U,
            });

    private static Paragraph BuildImageParagraph(MainDocumentPart main, BundleSource source, int index)
    {
        var imagePart = main.AddImagePart(source.Kind == BundleSourceKind.Png ? ImagePartType.Png : ImagePartType.Jpeg);
        using (var data = new MemoryStream(source.Bytes, writable: false))
            imagePart.FeedData(data);
        var relId = main.GetIdOfPart(imagePart);

        var (pxW, pxH) = ReadImageSize(source) ?? (800, 1100);
        long cx = pxW * EmuPerPixel;
        long cy = pxH * EmuPerPixel;

        // Вписываем в поля страницы, не увеличивая (немного запаса по высоте на сам параграф).
        long maxW = (A4Width - 2 * Margin) * EmuPerTwip;
        long maxH = (A4Height - 2 * Margin - 400) * EmuPerTwip;
        var scale = Math.Min(1.0, Math.Min((double)maxW / cx, (double)maxH / cy));
        cx = (long)(cx * scale);
        cy = (long)(cy * scale);

        var name = $"Изображение {index}";
        var inline = new DW.Inline(
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = (UInt32Value)(uint)index, Name = name },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(
                new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = name },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        {
            DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U,
        };

        return new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new Run(new Drawing(inline)));
    }

    /// <summary>Размер изображения в пикселях по заголовку PNG (IHDR) или JPEG (маркер SOFn).</summary>
    private static (long W, long H)? ReadImageSize(BundleSource source)
    {
        var b = source.Bytes;
        try
        {
            if (source.Kind == BundleSourceKind.Png && b.Length >= 24)
            {
                long w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                long h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                return w > 0 && h > 0 ? (w, h) : null;
            }

            if (source.Kind == BundleSourceKind.Jpeg)
            {
                var i = 2;
                while (i + 9 < b.Length)
                {
                    if (b[i] != 0xFF) { i++; continue; }
                    var marker = b[i + 1];
                    if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
                    var len = (b[i + 2] << 8) | b[i + 3];
                    var isSof = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                    if (isSof)
                    {
                        long h = (b[i + 5] << 8) | b[i + 6];
                        long w = (b[i + 7] << 8) | b[i + 8];
                        return w > 0 && h > 0 ? (w, h) : null;
                    }
                    i += 2 + len;
                }
            }
        }
        catch (IndexOutOfRangeException)
        {
            // битый заголовок — возьмём размер по умолчанию
        }

        return null;
    }
}
