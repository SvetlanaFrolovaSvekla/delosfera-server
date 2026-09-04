using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace delosfera_server.Modules.Documents.VND.Services;

/// <summary>Одна строка Листа согласования — согласующий, чьё ФИО и должность подставляются
/// в таблицу шаблона.</summary>
public record ApprovalSheetApproverInfo(string FullName, string? Position);

public interface IApprovalSheetGenerator
{
    /// <summary>Формирует DOCX "Лист согласования" по встроенному шаблону
    /// (ApprovalSheetTemplate.docx) — подставляет название ВНД в заголовок и по одной строке
    /// на каждого согласующего в таблицу, с единой датой окончательного согласования и
    /// результатом "Согласовано".</summary>
    byte[] Generate(string vndTitle, DateTime approvedAt, IReadOnlyList<ApprovalSheetApproverInfo> approvers);
}

/// <summary>Генератор Листа согласования — вызывается из VndApprovalService.FinalizeApprovalAsync
/// в момент, когда согласование редакции окончательно завершается (в т.ч. из фонового
/// таймаут-джоба ProcessTimeoutsAsync, без HTTP-запроса/браузера — поэтому генерация именно
/// на сервере через DocumentFormat.OpenXml, а не на клиенте).</summary>
public class ApprovalSheetGenerator : IApprovalSheetGenerator
{
    // LogicalName встроенного ресурса — см. EmbeddedResource в delosfera-server.csproj.
    private const string TemplateResourceName = "ApprovalSheetTemplate.docx";

    public byte[] Generate(string vndTitle, DateTime approvedAt, IReadOnlyList<ApprovalSheetApproverInfo> approvers)
    {
        using var templateStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException($"Встроенный ресурс {TemplateResourceName} не найден");

        using var output = new MemoryStream();
        templateStream.CopyTo(output);
        output.Position = 0;

        using (var doc = WordprocessingDocument.Open(output, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            FillTitle(body, vndTitle);
            FillTable(body, approvedAt, approvers);

            doc.MainDocumentPart.Document.Save();
        }

        return output.ToArray();
    }

    /// <summary>Заголовок шаблона содержит строку вида "К «»" одним текстовым узлом (подтверждено
    /// при разборе шаблона) — вставляем название ВНД между кавычками.</summary>
    private static void FillTitle(Body body, string vndTitle)
    {
        var target = body.Descendants<Text>().FirstOrDefault(t => t.Text.Contains("«»"));
        if (target is not null)
            target.Text = target.Text.Replace("«»", $"«{vndTitle}»");
    }

    private static void FillTable(Body body, DateTime approvedAt, IReadOnlyList<ApprovalSheetApproverInfo> approvers)
    {
        var table = body.Descendants<Table>().FirstOrDefault();
        if (table is null) return;

        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count < 2) return; // ожидается хотя бы шапка + один пример-ряд для форматирования

        // Второй ряд шаблона (первый ряд данных, с примером "1") - образец форматирования ячеек
        // (шрифты, границы, ширины колонок). Остальные пример-ряды шаблона (2..5) удаляем и
        // заполняем таблицу заново под фактическое число согласующих - число строк тем самым не
        // зависит от того, сколько пустых строк было в шаблоне.
        var templateRow = rows[1];
        for (var i = 1; i < rows.Count; i++)
            rows[i].Remove();

        var dateText = approvedAt.ToString("dd.MM.yyyy");

        var order = 1;
        foreach (var approver in approvers)
        {
            var row = (TableRow)templateRow.CloneNode(true);
            var cells = row.Elements<TableCell>().ToList();

            if (cells.Count >= 6)
            {
                SetCellText(cells[0], order.ToString());
                SetCellText(cells[1], approver.Position ?? "");
                SetCellText(cells[2], approver.FullName);
                SetCellText(cells[3], "Согласовано");
                SetCellText(cells[4], dateText);
                // cells[5] ("Подпись") остаётся пустой — лист формируется автоматически, без
                // физической подписи.
            }

            table.AppendChild(row);
            order++;
        }
    }

    /// <summary>Заменяет текст первого параграфа ячейки на заданный, сохраняя форматирование
    /// ячейки (границы/ширина не трогаются - меняется только содержимое параграфа). Работает как
    /// для изначально пустых ячеек шаблона (без единого run), так и для ячеек с run-заглушкой
    /// (напр. один пробел) - в обоих случаях старые runs удаляются и добавляется новый, с тем же
    /// шрифтом Arial, что и в шаблоне.</summary>
    private static void SetCellText(TableCell cell, string text)
    {
        var paragraph = cell.Elements<Paragraph>().FirstOrDefault();
        if (paragraph is null)
        {
            paragraph = new Paragraph();
            cell.AppendChild(paragraph);
        }

        foreach (var run in paragraph.Elements<Run>().ToList())
            run.Remove();

        var newRun = new Run(
            new RunProperties(new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" }),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        paragraph.AppendChild(newRun);
    }
}
