using System.IO.Compression;
using System.Text.RegularExpressions;

namespace delosfera_server.Modules.Documents.VND.Services;

public class DocxLegacyLinkExtractor : IDocxLegacyLinkExtractor
{
    // Ссылки живут не в самом word/document.xml, а в соответствующих *.rels рядом с частями,
    // которые на них ссылаются (word/_rels/document.xml.rels, а также header/footer при
    // наличии) — Word хранит гиперссылки как внешние отношения (Target, TargetMode="External"),
    // а в document.xml остаётся только r:id, указывающий на запись в .rels.
    private static readonly Regex DocumentRefRegex =
        new(@"db://documents/0*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AttachmentRefRegex =
        new(@"db://attachments/0*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public LegacyLinkReferences Extract(Stream docxStream)
    {
        MemoryStream? buffer = null;
        try
        {
            // ZipArchive в режиме Read читает центральный каталог с конца файла, поэтому нужен
            // seekable-поток — а поток из MinIO/сетевого хранилища таким может не быть.
            var seekable = docxStream;
            if (!docxStream.CanSeek)
            {
                buffer = new MemoryStream();
                docxStream.CopyTo(buffer);
                buffer.Position = 0;
                seekable = buffer;
            }

            var documentCodes = new List<string>();
            var attachmentIndexes = new List<int>();

            using var archive = new ZipArchive(seekable, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)) continue;

                using var reader = new StreamReader(entry.Open());
                var xml = reader.ReadToEnd();

                foreach (Match m in DocumentRefRegex.Matches(xml))
                    documentCodes.Add(m.Groups[1].Value);
                foreach (Match m in AttachmentRefRegex.Matches(xml))
                    attachmentIndexes.Add(int.Parse(m.Groups[1].Value));
            }

            return new LegacyLinkReferences(
                documentCodes.Distinct().ToList(),
                attachmentIndexes.Distinct().OrderBy(x => x).ToList());
        }
        catch
        {
            // Битый/не-zip файл, недоступный поток и т.п. — не должно ронять показ "Связей".
            return LegacyLinkReferences.Empty;
        }
        finally
        {
            buffer?.Dispose();
        }
    }
}
