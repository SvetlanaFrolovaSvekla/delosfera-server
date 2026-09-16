using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using delosfera_server.Modules.Substitutions.Models;

namespace delosfera_server.Modules.Substitutions.Services;

public interface ISubstitutionPrintService
{
    /// <summary>Приказ «О временном возложении обязанностей» (форма 41-29-01/ЛС).</summary>
    byte[] Order(SubstitutionRequest r);
    /// <summary>Договор о полной индивидуальной материальной ответственности (форма 41-15-01/МО).</summary>
    byte[] Liability(SubstitutionRequest r);
}

/// <summary>
/// Печатные формы заявки на замещение. Собираются на сервере через OpenXml, чтобы
/// формировались и из фоновых задач, и без браузера. Текст воспроизводит образцы
/// форм ЛС (приказ) и МО (договор о матответственности) с подстановкой данных заявки.
/// </summary>
public class SubstitutionPrintService : ISubstitutionPrintService
{
    private static string D(DateOnly? d) => d?.ToString("dd.MM.yyyy") ?? "«___»____________ ____ г.";
    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

    private static string ReasonPhrase(SubstitutionReason r) => r switch
    {
        SubstitutionReason.Sick => "В связи с временной нетрудоспособностью",
        SubstitutionReason.Vacation => "В связи с очередным трудовым отпуском",
        SubstitutionReason.Dismissal => "В связи с увольнением",
        _ => "В связи с временным отсутствием",
    };

    private static string HandoverPhrase(HandoverMoment m) =>
        m == HandoverMoment.EndOfDay ? "на конец рабочего дня" : "на начало рабочего дня";

    public byte[] Order(SubstitutionRequest r)
    {
        var b = new Docx();
        b.P("ПРИКАЗ", bold: true, center: true);
        b.P($"№ {Dash(r.RegNumber)} от {D(r.StartsOn)} года", center: true);
        b.P("«О временном возложении обязанностей»", bold: true, center: true);
        b.P("");
        b.P($"{ReasonPhrase(r.Reason)} {Dash(r.AbsentName)} – {Dash(r.AbsentPosition)}" +
            $"{(string.IsNullOrWhiteSpace(r.AbsentBranch) ? "" : " " + r.AbsentBranch.Trim())} с {D(r.StartsOn)} года,");
        b.P("ПРИКАЗЫВАЮ:", bold: true);
        b.P("1. Временное исполнение обязанностей:");
        b.P($"- {Dash(r.AbsentName)}");
        b.P("2. Возложить на сотрудника:");
        b.P($"- {Dash(r.SubstituteName)}");
        b.P($"3. {Short(r.AbsentName)} передать {Short(r.SubstituteName)} по акту приёма-передачи " +
            $"ключи, печать, штампы, денежную наличность и прочие ценности по состоянию " +
            $"{HandoverPhrase(r.HandoverMoment)} {D(r.HandoverOn ?? r.StartsOn)} г.");
        b.P("Для исполнения п.3 создать комиссию в составе:");
        if (!string.IsNullOrWhiteSpace(r.CommissionChairName))
            b.P($"{r.CommissionChairName.Trim()} – {Dash(r.CommissionChairPosition)}, председатель комиссии;");
        foreach (var m in r.CommissionMembers.OrderBy(x => x.SortOrder))
            b.P($"{m.FullName} – {Dash(m.Position)}, член комиссии;");
        b.P($"{Short(r.AbsentName)} – передаёт;");
        b.P($"{Short(r.SubstituteName)} – принимает.");
        b.P($"На период исполнения обязанностей временно отсутствующего сотрудника назначить " +
            $"доплату за совмещение должностей к должностному окладу {Short(r.SubstituteName)} " +
            $"в соответствии со ст. 151 ТК КР.");
        b.P("");
        b.P($"ОСНОВАНИЕ: Электронная заявка № «{Dash(r.RegNumber)}»");
        b.P("");
        b.P("Директор \t\t\t\t\t_______________________");
        b.P("");
        b.P("Ознакомлены и согласны:");
        b.P($"{Short(r.AbsentName)} \t\t\t____________ «____»__________ ____ г.");
        b.P($"{Short(r.SubstituteName)} \t\t\t____________ «____»__________ ____ г.");
        return b.Build();
    }

    public byte[] Liability(SubstitutionRequest r)
    {
        var b = new Docx();
        b.P("ДОГОВОР № МО", bold: true, center: true);
        b.P("о полной индивидуальной материальной ответственности", bold: true, center: true);
        b.P($"{Dash(r.SubstituteBranch)}                                        {D(r.StartsOn)}");
        b.P("");
        b.P($"ОАО «Керемет Банк», именуемое в дальнейшем «Банк», в лице руководителя, с одной стороны, " +
            $"и {Dash(r.SubstituteName)}, проживающий(ая) по адресу: {Dash(r.AddressResidence)}, " +
            $"зарегистрированный(ая) по адресу: {Dash(r.AddressRegistration)}, паспорт серия/№: " +
            $"{Dash(r.PassportSeriesNumber)}, выдан: {D(r.PassportIssuedOn)}{(string.IsNullOrWhiteSpace(r.PassportIssuedBy) ? "" : ", " + r.PassportIssuedBy.Trim())}, " +
            $"действителен до: {D(r.PassportValidUntil)}, ИНН: {Dash(r.Inn)}, именуемый(ая) в дальнейшем «Работник», " +
            $"с другой стороны, заключили настоящий договор о нижеследующем:");
        b.P($"1. Работник приступает к исполнению обязанностей {Dash(r.SubstitutePosition)}" +
            $"{(string.IsNullOrWhiteSpace(r.SubstituteBranch) ? "" : " " + r.SubstituteBranch.Trim())} и принимает на себя " +
            "полную материальную ответственность за обеспечение сохранности вверенных ему Банком " +
            "товарно-материальных ценностей, бухгалтерских документов и печатей.");
        foreach (var clause in LiabilityClauses)
            b.P(clause);
        b.P("");
        b.P("Реквизиты и подписи сторон:");
        b.P("Банк: ОАО «Керемет Банк»                    Работник: " + Dash(r.SubstituteName));
        b.P("Подпись ____________________                Подпись ____________________");
        return b.Build();
    }

    private static string Short(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "—";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3) return $"{parts[0]} {parts[1][0]}.{parts[2][0]}.";
        if (parts.Length == 2) return $"{parts[0]} {parts[1][0]}.";
        return fullName.Trim();
    }

    private static readonly string[] LiabilityClauses =
    [
        "2. Работник обязуется бережно относиться к переданным ему материальным ценностям и принимать меры к предотвращению ущерба; своевременно сообщать руководству обо всех обстоятельствах, угрожающих сохранности ценностей.",
        "3. Работник несёт полную материальную ответственность за сохранность ценностей и ущерб, причинённый Банку, с момента фактического приёма им ценностей.",
        "4. Банк обязуется создать Работнику условия, необходимые для нормальной работы и обеспечения полной сохранности вверенных ему ценностей, и проводить в установленном порядке инвентаризацию.",
        "5. Определение размера ущерба и его возмещение производятся в соответствии с действующим законодательством Кыргызской Республики.",
        "6. Работник не несёт материальной ответственности, если ущерб причинён не по его вине.",
        "7. Действие настоящего Договора распространяется на всё время работы с вверенными Работнику ценностями Банка.",
        "8. При прекращении трудовых отношений Работник обязуется передать все вверенные ему ценности по акту приёма-передачи Банку.",
        "9. Настоящий Договор составлен в двух экземплярах, имеющих одинаковую юридическую силу, по одному для каждой из Сторон.",
    ];
}

/// <summary>Минимальный конструктор .docx поверх OpenXml: абзацы с жирностью и выравниванием.</summary>
internal sealed class Docx
{
    private readonly Body _body = new();

    public void P(string text, bool bold = false, bool center = false)
    {
        var rPr = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" },
            new FontSize { Val = "24" },
            new FontSizeComplexScript { Val = "24" });
        if (bold) rPr.AppendChild(new Bold());

        var run = new Run();
        run.AppendChild(rPr);
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

        var pPr = new ParagraphProperties();
        if (center) pPr.AppendChild(new Justification { Val = JustificationValues.Center });

        var p = new Paragraph();
        p.AppendChild(pPr);
        p.AppendChild(run);
        _body.AppendChild(p);
    }

    public byte[] Build()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(_body);
            main.Document.Save();
        }
        return ms.ToArray();
    }
}
