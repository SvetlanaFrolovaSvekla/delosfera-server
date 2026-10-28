using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using delosfera_server.Modules.Substitutions.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace delosfera_server.Modules.Substitutions.Services;

public interface ISubstitutionPrintService
{
    /// <summary>
    /// Приказ «О временном возложении обязанностей» (форма Ф-82-01/ЛС). <paramref name="chairmanName"/> —
    /// кто подписывает приказ («Председатель Правления», от руки); <paramref name="visas"/> — визирующие
    /// («Завизировали:»). Подписи ЭЦП под приказом — только отсутствующего и замещающего.
    /// </summary>
    byte[] Order(SubstitutionRequest r, string? chairmanName = null, IReadOnlyList<string>? visas = null);
    /// <summary>
    /// Договор о полной индивидуальной материальной ответственности (форма 41-15-01/МО).
    /// <paramref name="bankSigners"/> — кто подписывает от имени Банка: основной (Председатель
    /// Правления из настроек маршрута УЧР) и дополнительные, добавленные в заявке; их ФИО — в
    /// «в лице …» и в строках подписей. <paramref name="blank"/> — версия с пустыми полями: номер
    /// договора, «в лице …» и ФИО подписантов от Банка — черты, вписываются от руки.
    /// </summary>
    byte[] Liability(SubstitutionRequest r, IReadOnlyList<SubstitutionBankSigner>? bankSigners = null, bool blank = false);
    /// <summary>Выгрузка карточки заявки со всеми полями (для передачи/архива).</summary>
    byte[] Card(SubstitutionRequest r);
}

/// <summary>Подписант договора от имени Банка: ФИО и должность (для «в лице …» и строки подписи).</summary>
public sealed record SubstitutionBankSigner(string FullName, string? Position, int UserId = 0);

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

    public byte[] Order(SubstitutionRequest r, string? chairmanName = null, IReadOnlyList<string>? visas = null)
    {
        // Приказ по образцу Ф-82-01/ЛС: Segoe UI 10, логотип Банка сверху. Номер УЧР ставит вручную
        // при регистрации — поле пустое. Подписи ЭЦП под приказом ставят только отсутствующий и
        // замещающий («Ознакомлены и согласны»); Председатель Правления и визирующие — от руки.
        var b = new Docx { Logo = true };
        b.Add(MoDocx.Pi(MoDocx.Left, [MoDocx.R("ПРИКАЗ", bold: true, size: 24)], after: 60));
        b.Add(MoDocx.Pi(MoDocx.Left, [MoDocx.R($"№ ____________ от {D(r.StartsOn)} года", bold: true)], after: 60));
        b.Add(MoDocx.Pi(MoDocx.Left, [MoDocx.R("«О временном возложении обязанностей»", bold: true, italic: true)], after: 60));
        b.Add(MoDocx.P(MoDocx.Both));

        var absentWho = string.Join(" ", new[] { r.AbsentPosition, r.AbsentBranch }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        b.Add(MoDocx.Pi(MoDocx.Both,
            [MoDocx.R($"{ReasonPhrase(r.Reason)} {Dash(r.AbsentName)}{(absentWho.Length == 0 ? "" : " – " + absentWho)} с {D(r.StartsOn)} года,", italic: true)],
            firstLine: 567, after: 60));
        b.Add(MoDocx.P(MoDocx.Both));
        b.Add(MoDocx.Pi(MoDocx.Both, [MoDocx.R("ПРИКАЗЫВАЮ:", bold: true)], after: 60));

        b.Add(OrderLine("1. Временное исполнение обязанностей:"));
        Person(b, r.AbsentName, r.AbsentPosition, r.AbsentUnit?.TitleRu, r.AbsentBranch);
        b.Add(OrderLine("2. Возложить на сотрудника:"));
        Person(b, r.SubstituteName, r.SubstitutePosition, r.SubstituteUnit?.TitleRu, r.SubstituteBranch);

        b.Add(OrderLine($"3. {Short(r.AbsentName)} передать {Short(r.SubstituteName)} по акту приёма-передачи " +
                        "ключи, печать, штампы, денежную наличность и прочие ценности по состоянию " +
                        $"{HandoverPhrase(r.HandoverMoment)} {D(r.HandoverOn ?? r.StartsOn)} г."));
        b.Add(OrderLine("4. Для исполнения п.3 создать комиссию в составе:"));
        if (!string.IsNullOrWhiteSpace(r.CommissionChairName))
            b.Add(OrderLine($"{r.CommissionChairName.Trim()} – {Dash(r.CommissionChairPosition)}, председатель комиссии;"));
        foreach (var m in r.CommissionMembers.OrderBy(x => x.SortOrder))
            b.Add(OrderLine($"{m.FullName} – {Dash(m.Position)}, член комиссии;"));
        b.Add(OrderLine($"{Short(r.AbsentName)} – передаёт;"));
        b.Add(OrderLine($"{Short(r.SubstituteName)} – принимает."));
        b.Add(OrderLine("5. На период исполнения обязанностей временно отсутствующего сотрудника назначить доплату за " +
                        $"совмещение должностей к должностному окладу {Short(r.SubstituteName)}, в размере 30 % от " +
                        "должностного оклада по совмещаемой должности в соответствии со ст. 101 ТК КР."));
        b.Add(MoDocx.P(MoDocx.Both));
        b.Add(MoDocx.Pi(MoDocx.Both, [
            MoDocx.R("ОСНОВАНИЕ:", bold: true),
            MoDocx.R($" Электронная заявка № «{Dash(r.RegNumber)}»"),
        ], after: 60));
        b.Add(MoDocx.P(MoDocx.Both));

        // Подписи от руки: Председатель Правления, визирующие. Маркер «Ознакомлены и согласны:» —
        // начало блока, который в ЭЦП-версии заменяют штампы отсутствующего и замещающего
        // (SubstitutionDocuments.SplitSignatureBlock).
        var year = (r.StartsOn ?? DateOnly.FromDateTime(DateTime.UtcNow)).Year;
        b.Add(MoDocx.Tabbed([MoDocx.R("Председатель Правления", bold: true), MoDocx.R("\t", bold: true),
            MoDocx.R(string.IsNullOrWhiteSpace(chairmanName) ? "________________" : Short(chairmanName), bold: true)], 6237));
        b.Add(MoDocx.P(MoDocx.Both));
        var visaNames = (visas ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (visaNames.Count > 0)
        {
            b.Add(MoDocx.Pi(MoDocx.Left, [MoDocx.R("Завизировали:", bold: true)], after: 60));
            foreach (var v in visaNames) b.Add(SignLine(Short(v), year));
            b.Add(MoDocx.P(MoDocx.Both));
        }
        b.Add(MoDocx.Pi(MoDocx.Left, [MoDocx.R("Ознакомлены и согласны:", bold: true)], after: 60));
        b.Add(SignLine(Short(r.AbsentName), year));
        b.Add(SignLine(Short(r.SubstituteName), year));
        return b.Build();
    }

    private static Paragraph OrderLine(string text) => MoDocx.Pi(MoDocx.Both, [MoDocx.R(text)], after: 60);

    /// <summary>«- ФИО» (жирный курсив), ниже «должность, структурное подразделение: …» и «организация: …».</summary>
    private static void Person(Docx b, string? name, string? position, string? unit, string? branch)
    {
        b.Add(MoDocx.Pi(MoDocx.Both, [MoDocx.R($"- {Dash(name)}", bold: true, italic: true)], after: 20));
        var post = string.Join(", ", new[] { position, unit }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        b.Add(MoDocx.Pi(MoDocx.Left, [
            MoDocx.R("должность, структурное подразделение: "),
            MoDocx.R(post.Length == 0 ? "—" : post, bold: true),
        ], left: 567, after: 20));
        b.Add(MoDocx.Pi(MoDocx.Left, [
            MoDocx.R("организация: "),
            MoDocx.R(Dash(branch), bold: true),
        ], left: 567, after: 60));
    }

    /// <summary>«Фамилия И.О. ____________ «____»__________ 2026 г.» — строка подписи от руки.</summary>
    private static Paragraph SignLine(string name, int year) =>
        MoDocx.Tabbed([MoDocx.R(name), MoDocx.R($"\t___________________________ «_____»____________ {year} г.")], 3402);

    public byte[] Liability(SubstitutionRequest r, IReadOnlyList<SubstitutionBankSigner>? bankSigners = null, bool blank = false)
    {
        var signers = blank
            ? new List<SubstitutionBankSigner>()
            : (bankSigners ?? Array.Empty<SubstitutionBankSigner>()).Where(s => !string.IsNullOrWhiteSpace(s.FullName)).ToList();
        // Договор по образцу 41-15-01/МО (Ф-94-01/МО): Segoe UI 10, поля А4 как у бланка.
        var b = new Docx();
        const string Blank = "________________________________________";

        // Номер — регистрационный номер заявки; в пустой версии — черта (вписывают при регистрации).
        var number = blank || string.IsNullOrWhiteSpace(r.RegNumber) ? "____________" : r.RegNumber.Trim();
        b.Add(MoDocx.P(MoDocx.Center, MoDocx.R($"ДОГОВОР № {number}", bold: true)));
        b.Add(MoDocx.P(MoDocx.Center, MoDocx.R("о полной индивидуальной материальной ответственности", bold: true)));
        b.Add(MoDocx.P(MoDocx.Center));
        b.Add(MoDocx.CityDate("г. Бишкек", r.StartsOn is { } date ? $"{date:dd.MM.yyyy} года" : "«___»____________ ____ года"));

        // Стороны: от Банка — подписанты из настроек и заявки («ФИО (должность)», несколько — через
        // запятую); в пустой версии — черта для ответственного лица (Председатель Правления или
        // Исполнительный директор вписывается от руки). Работник — замещающий, данные из заявки.
        var bankSide = signers.Count == 0
            ? $"{Blank}, действующего"
            : string.Join(", ", signers.Select(s => string.IsNullOrWhiteSpace(s.Position)
                  ? s.FullName.Trim()
                  : $"{s.FullName.Trim()} ({s.Position.Trim()})"))
              + (signers.Count > 1 ? ", действующих" : ", действующего");
        b.Add(MoDocx.Pi(MoDocx.Both, [
            MoDocx.R("ОАО «Керемет Банк»,", bold: true),
            MoDocx.R(" именуемый в дальнейшем "),
            MoDocx.R("«Банк»,", bold: true),
            MoDocx.R($" в лице {bankSide} на основании устава с одной стороны, и {WorkerDetails(r)}, именуемый в дальнейшем "),
            MoDocx.R("«Работник»,", bold: true),
            MoDocx.R(" с другой стороны, в целях обеспечения сохранности материальных ценностей, принадлежащих ОАО \"Керемет Банк\", заключили настоящий договор о нижеследующем:"),
        ], firstLine: 567));
        b.Add(MoDocx.P(MoDocx.Both));

        // Должность и филиал того, кого замещают.
        var duty = string.Join(" ", new[] { r.AbsentPosition, r.AbsentBranch }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        b.Add(MoDocx.P(MoDocx.Both, MoDocx.R(
            $"1. Работник, приступает к исполнению обязанностям {(duty.Length == 0 ? Blank : duty)}, принимает на себя " +
            "полную материальную ответственность по обеспечению сохранности, вверенных ему Банком товарно-материальных " +
            "ценностей, бухгалтерских документов, печатей, в связи с чем, обязуется:")));
        Letters(b, LiabilityWorkerDuties);
        b.Add(MoDocx.P(MoDocx.Both, MoDocx.R("2. Банк обязуется:")));
        Letters(b, LiabilityBankDuties);
        foreach (var clause in LiabilityClauses)
        {
            b.Add(MoDocx.P(MoDocx.Both, MoDocx.R(clause)));
            b.Add(MoDocx.P(MoDocx.Both));
        }
        b.Add(MoDocx.P(MoDocx.Center, MoDocx.R("12. Реквизиты и подписи сторон")));
        b.Add(MoDocx.P(MoDocx.Both));

        // Реквизиты: слева Банк, справа Работник (данные из заявки).
        b.Add(MoDocx.TwoColumns(
            [
                MoDocx.P(MoDocx.Center, MoDocx.R("Банк", bold: true)),
                MoDocx.P(MoDocx.Left, MoDocx.R("ОАО \"Керемет Банк\"")),
                MoDocx.P(MoDocx.Left, MoDocx.R("Адрес:")),
                MoDocx.P(MoDocx.Left, MoDocx.R("Кыргызстан, г. Бишкек, ул. Тоголок Молдо, дом № 40/4")),
                MoDocx.P(MoDocx.Left, MoDocx.R("Свидетельство о государственной перерегистрации:")),
                MoDocx.P(MoDocx.Left, MoDocx.R("№ 114488-3300-ОАО от 09.03.2026г.")),
            ],
            [
                MoDocx.P(MoDocx.Center, MoDocx.R("Работник", bold: true)),
                MoDocx.P(MoDocx.Left, MoDocx.R(WorkerDetails(r))),
            ]));

        // Рукописный блок подписей (в ЭЦП-версии его место занимают штампы — см.
        // SubstitutionDocuments.SplitSignatureBlock, маркер — строка «Подпись:»). От Банка — строка на
        // каждого подписанта; в пустой версии — одна строка с чертой вместо ФИО.
        var signLines = signers.Count == 0
            ? new[] { "________________" }
            : signers.Select(s => Short(s.FullName)).ToArray();
        b.Add(MoDocx.TwoColumns(
            signLines.Select((name, i) =>
                MoDocx.Pi(MoDocx.Left, [MoDocx.R($"Подпись: ______________ {name}", bold: true)], before: i == 0 ? 240 : 160)).ToArray(),
            [MoDocx.Pi(MoDocx.Left, [MoDocx.R("Подпись ____________________", bold: true)], before: 240)]));
        b.Add(MoDocx.Pi(MoDocx.Left, [MoDocx.R("М.П.", bold: true)], before: 120));
        return b.Build();
    }

    /// <summary>Данные Работника (замещающего) одной фразой — как в преамбуле и реквизитах договора.</summary>
    private static string WorkerDetails(SubstitutionRequest r)
    {
        var parts = new List<string>
        {
            $"{Dash(r.SubstituteName)} проживающий по адресу: {Dash(r.AddressResidence)}, зарегистрированный по адресу: {Dash(r.AddressRegistration)}.",
        };
        var passport = $"Паспорт, серия/№: {Dash(r.PassportSeriesNumber)}, выдан: {LongDate(r.PassportIssuedOn)}";
        if (!string.IsNullOrWhiteSpace(r.PassportIssuedBy)) passport += $", {r.PassportIssuedBy.Trim()}";
        passport += $", действителен до: {(r.PassportValidUntil is null ? "—" : LongDate(r.PassportValidUntil))}";
        passport += $", идентификационный номер удостоверения социальной защиты: {Dash(r.Inn)}";
        parts.Add(passport);
        return string.Join(" ", parts);
    }

    private static readonly string[] MonthsGenitive =
    [
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря",
    ];

    /// <summary>«10 сентября 2020 года».</summary>
    private static string LongDate(DateOnly? d) =>
        d is { } x ? $"{x.Day} {MonthsGenitive[x.Month - 1]} {x.Year} года" : "«___»____________ ____ года";

    /// <summary>Подпункты а), б), … с висячим отступом.</summary>
    private static void Letters(Docx b, string[] items)
    {
        const string letters = "абвгдежзиклмнопрстуф";
        for (var i = 0; i < items.Length; i++)
            b.Add(MoDocx.Pi(MoDocx.Both, [MoDocx.R($"{letters[i]})\t{items[i]}")], left: 567, hanging: 425));
    }

    /// <summary>
    /// Листок заявки на замещение — в виде таблицы, как выгрузка заявки из Jira (HR-…):
    /// шапка с номером и темой, блоки «подпись — значение» с серыми ячейками подписей,
    /// ниже — описание. Шрифт Segoe UI 10. Поля, которых в заявке нет (приоритет, голоса,
    /// метки, оценки времени и т.п.), не выводятся.
    /// </summary>
    public byte[] Card(SubstitutionRequest r)
    {
        var b = new Docx();

        // ── Шапка: [номер] тема, даты создания/изменения, статус ─────────────────
        var created = CardDocx.Local(r.CreatedAt).ToString("dd.MM.yyyy");
        var updated = CardDocx.Local(r.UpdatedAt).ToString("dd.MM.yyyy");
        var head = CardDocx.Grid(35, 65);
        head.AppendChild(CardDocx.Row(
            CardDocx.TitleCell($"[{Dash(r.RegNumber)}] {Dash(r.Subject)}", $"Создано: {created}   Обновлено: {updated}", span: 2)));
        head.AppendChild(CardDocx.Row(CardDocx.Cell("Статус:", 35, label: true, plain: true), CardDocx.Cell(StatusText(r.Status), 65, plain: true)));
        head.AppendChild(CardDocx.Row(CardDocx.Cell("Тип:", 35, label: true, plain: true), CardDocx.Cell("Заявка на замещение", 65, plain: true)));
        b.Add(head);
        b.Add(CardDocx.Spacer());

        // ── Автор / инициатор ───────────────────────────────────────────────────
        var people = CardDocx.Grid(15, 35, 15, 35);
        people.AppendChild(CardDocx.Row(
            CardDocx.Cell("Автор:", 15, label: true), CardDocx.Cell(Dash(r.AuthorUser?.FullName), 35),
            CardDocx.Cell("Инициатор:", 15, label: true), CardDocx.Cell(Dash(r.InitiatorUser?.FullName), 35)));
        b.Add(people);
        b.Add(CardDocx.Spacer());

        // ── Поля заявки ─────────────────────────────────────────────────────────
        var rows = new List<(string Label, string? Value)>
        {
            ("Категория", ReasonText(r.Reason)),
            ("Сотрудник отсутствующий", r.AbsentName),
            ("Должность отсутствующего сотрудника", r.AbsentPosition),
            ("Филиал отсутствующего сотрудника", r.AbsentBranch),
            ("Структурное подразделение", r.AbsentUnit?.TitleRu),
            ("Сотрудник замещающий", r.SubstituteName),
            ("Должность замещающего сотрудника", r.SubstitutePosition),
            ("Филиал замещающего сотрудника", r.SubstituteBranch),
            ("Структурное подразделение замещающего", r.SubstituteUnit?.TitleRu),
            ("Серия и номер паспорта", r.PassportSeriesNumber),
            ("Кем выдан паспорт", r.PassportIssuedBy),
            ("Дата выдачи паспорта", DOrNull(r.PassportIssuedOn)),
            ("Действителен до", DOrNull(r.PassportValidUntil)),
            ("Адрес", r.AddressRegistration),
            ("Адрес проживания", r.AddressResidence),
            ("ИНН замещающего сотрудника", r.Inn),
            ("Количество календарных дней", r.DaysCount?.ToString()),
            ("Дата начала", DOrNull(r.StartsOn)),
            ("Дата по какое", DOrNull(r.EndsOn)),
            ("Председатель комиссии", r.CommissionChairName),
            ("Должность Председателя комиссии", r.CommissionChairPosition),
        };
        var n = 0;
        foreach (var m in r.CommissionMembers.OrderBy(x => x.SortOrder))
        {
            n++;
            rows.Add(($"Член комиссии №{n}", m.FullName));
            rows.Add(($"Должность Члена комиссии №{n}", m.Position));
        }
        rows.Add(("Создать комиссию приема передачи", HandoverPhrase(r.HandoverMoment)));
        rows.Add(("День приема-передачи", DOrNull(r.HandoverOn)));

        // Согласующие из маршрута (последний круг): директор филиала, Опер. управление, УЧР.
        var lastRound = r.Approvals.Count > 0 ? r.Approvals.Max(a => a.Round) : 0;
        foreach (var a in r.Approvals.Where(a => a.Round == lastRound).OrderBy(a => a.Order))
            rows.Add((a.RoleLabel == "Директор филиала" ? "Директор" : a.RoleLabel, a.User?.FullName));

        var fields = CardDocx.Grid(35, 65);
        foreach (var (label, value) in rows)
            fields.AppendChild(CardDocx.Row(CardDocx.Cell(label + ":", 35, label: true), CardDocx.Cell(Dash(value), 65)));
        b.Add(fields);
        b.Add(CardDocx.Spacer());

        // ── Описание ────────────────────────────────────────────────────────────
        b.Add(CardDocx.SectionTitle("Описание"));
        var description = (r.Description ?? "").Replace("\r\n", "\n").Split('\n');
        if (string.IsNullOrWhiteSpace(r.Description)) description = ["—"];
        foreach (var line in description)
            b.Add(CardDocx.Para(line));

        b.Add(CardDocx.Spacer());
        b.Add(CardDocx.Para($"Сформировано {CardDocx.Local(DateTime.UtcNow):dd.MM.yyyy HH:mm} в системе «Делосфера».", sizeHalfPt: 16, color: "808080"));

        return b.Build();
    }

    private static string? DOrNull(DateOnly? d) => d?.ToString("dd.MM.yyyy");

    private static string StatusText(SubstitutionStatus s) => s switch
    {
        SubstitutionStatus.Draft => "Черновик",
        SubstitutionStatus.OnApproval => "На согласовании",
        SubstitutionStatus.OnExecution => "На исполнении",
        SubstitutionStatus.Executed => "Исполнено",
        SubstitutionStatus.Rejected => "Отклонено",
        SubstitutionStatus.Withdrawn => "Отозвано",
        _ => s.ToString(),
    };

    private static string ReasonText(SubstitutionReason r) => r switch
    {
        SubstitutionReason.Sick => "Больничный",
        SubstitutionReason.Vacation => "Отпуск",
        SubstitutionReason.Dismissal => "Увольнение",
        _ => "Другое",
    };

    /// <summary>«Фамилия И.О.» — как в строках подписей бланка.</summary>
    internal static string Short(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "—";
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3) return $"{parts[0]} {parts[1][0]}.{parts[2][0]}.";
        if (parts.Length == 2) return $"{parts[0]} {parts[1][0]}.";
        return fullName.Trim();
    }

    private static readonly string[] LiabilityWorkerDuties =
    [
        "обеспечивать точный учет ценностей и результатов финансово-хозяйственной деятельности;",
        "обеспечивать правильное ведение учета исполнения смет расходов, начисления и своевременного перечисления налогов и других платежей в государственный бюджет;",
        "контролировать составление достоверной бухгалтерской отчетности, правильность расходования фонда заработной платы, соблюдение финансовой и кассовой дисциплины;",
        "не допускать разглашения сведений об операциях, ставших известными при исполнении должностных обязанностей;",
        "контролировать соблюдение установленных правил проведения инвентаризаций денежных средств, товарно-материальных ценностей, основных фондов, расчетов и платежных обязательств;",
        "обеспечить участие в работе по оформлению материалов по недостачам и хищениям денежных средств и товарно-материальных ценностей;",
        "обеспечить контроль за законностью списания с бухгалтерского баланса Банка недостач, дебиторской задолженности и других потерь;",
        "обеспечить контроль за законностью, своевременностью и правильностью оформления бухгалтерских операций, проводимых Банком;",
        "обеспечить участие в подготовке мероприятий, предупреждающих образование недостач и незаконное расходование денежных средств, нарушения законодательства.",
        "бережно относиться к переданному ему для осуществления возложенных на него функций (обязанностей) имуществу или других целей материальным ценностям Банка и принимать меры к предотвращению любого ущерба (как прямого, так и косвенного) вверенным ценностям;",
        "своевременно сообщать руководству обо всех обстоятельствах, угрожающих обеспечению сохранности вверенных ему материальных ценностей и препятствующих выполнению своих должностных обязанностей;",
        "возместить материальный ущерб, причиненный Банку виновными действиями при выполнении своих обязанностей (недостачей, повреждением и др.), подтвержденными письменными документами (актом, заключением, инвентаризационной ведомостью и др.).",
    ];

    private static readonly string[] LiabilityBankDuties =
    [
        "создать Работнику условия, необходимые для нормальной работы и обеспечение полной сохранности вверенных ему ценностей;",
        "знакомить Работника с действующим законодательством о материальной ответственности за ущерб, причиненный предприятию, учреждению, организации, а также с инструкциями, нормативами документами, действующими в Банке;",
        "проводить в установленном порядке инвентаризацию материальных ценностей;",
    ];

    private static readonly string[] LiabilityClauses =
    [
        "3. Работник несет полную материальную ответственность за сохранность ценностей и ущерб, причиненный Банку, как в результате умышленных действий, так и в результате небрежного или недобросовестного исполнения своих должностных обязанностей, с момента фактического приема им ценностей.",
        "4. Работник несет ответственность за нарушение нормативных актов, регламентирующих финансово-хозяйственную деятельность, за нарушение сроков представления бухгалтерских отчетов и балансов руководству и государственным органам.",
        "5. В случае необеспечения по вине Работника сохранности вверенных ему материальных ценностей, определение размера ущерба, причиненного Банку, и его возмещение производятся в соответствии с действующим законодательством.",
        "6. Привлечение Работника к материальной ответственности осуществляется руководителем Банка после проведения служебного расследования причин образования ущерба, с учетом письменных объяснений, представленных Работником, а в необходимых случаях также заключений независимых экспертов.",
        "7. Работник не несет материальной ответственности, если ущерб причинен не по его вине или если будут установлены конкретные виновники причиненного ущерба из числа сотрудников Банка или других лиц.",
        "8. Действие настоящего Договора распространяется на все время работы с вверенными Работнику денежными и другими ценностями Банка.",
        "9. В случае прекращения трудовых отношений Работник обязуется передать все вверенные ему денежные средства, документы и материальные ценности по акту приема – передачи Банку.",
        "10. Прекращение трудовых отношений после причинения ущерба не влечет за собой освобождение Работника, виновного в причинении ущерба, от полной материальной ответственности.",
        "11. Настоящий Договор составлен в двух подлинных экземплярах, имеющих одинаковую юридическую силу, по одному для каждой из Сторон.",
    ];
}

/// <summary>Минимальный конструктор .docx поверх OpenXml: абзацы с жирностью и выравниванием.</summary>
internal sealed class Docx
{
    private readonly Body _body = new();

    /// <summary>Вставить готовый элемент (таблицу, абзац) — для листка заявки.</summary>
    public void Add(OpenXmlElement element) => _body.AppendChild(element);

    public void P(string text, bool bold = false, bool center = false)
    {
        var pPr = new ParagraphProperties(
            new SpacingBetweenLines { After = "60", Line = "264", LineRule = LineSpacingRuleValues.Auto });
        // Текст приказа/договора — по ширине, заголовки — по центру (как в бланках ЛС/МО).
        pPr.AppendChild(new Justification { Val = center ? JustificationValues.Center : JustificationValues.Both });

        var p = new Paragraph();
        p.AppendChild(pPr);
        // «\t» в тексте — настоящая табуляция Word (место подписи), а не символ внутри строки.
        var parts = text.Split('\t');
        for (var i = 0; i < parts.Length; i++)
        {
            var run = new Run();
            run.AppendChild(RunProps(bold));
            if (i > 0) run.AppendChild(new TabChar());
            run.AppendChild(new Text(parts[i]) { Space = SpaceProcessingModeValues.Preserve });
            p.AppendChild(run);
        }
        _body.AppendChild(p);
    }

    private static RunProperties RunProps(bool bold)
    {
        var rPr = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" },
            new FontSize { Val = "24" },
            new FontSizeComplexScript { Val = "24" });
        if (bold) rPr.AppendChild(new Bold());
        return rPr;
    }

    /// <summary>Абзац с логотипом: картинка 75 × 10,3 мм (933 × 127 px), как на бланке.</summary>
    private static Paragraph LogoParagraph(string relId)
    {
        const long cx = 2847975L, cy = 390525L;
        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = "Логотип", Description = "Логотип Банка" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = 0U, Name = "logo.png" },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
        return new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "240" }),
            new Run(drawing));
    }

    /// <summary>
    /// Лист A4 с полями бланка (лево 30 мм, право 15 мм, верх/низ 20 мм). Без этого у документа не
    /// было размеров страницы: Word подставлял свои, а просмотр в браузере растягивал текст на всю
    /// ширину окна без полей.
    /// </summary>
    public static SectionProperties A4Section() => new(
        new PageSize { Width = 11906U, Height = 16838U },
        new PageMargin
        {
            Top = 1134, Bottom = 1134, Left = 1701U, Right = 851U,
            Header = 708U, Footer = 708U, Gutter = 0U,
        });

    /// <summary>Логотип Банка в начале документа (приказ — как на бланке Ф-82-01/ЛС).</summary>
    public bool Logo { get; init; }

    private const string LogoResource = "SubstitutionBankLogo.png";

    public byte[] Build()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            if (Logo && typeof(Docx).Assembly.GetManifestResourceStream(LogoResource) is { } logo)
            {
                using (logo)
                {
                    var image = main.AddImagePart(ImagePartType.Png);
                    image.FeedData(logo);
                    _body.PrependChild(LogoParagraph(main.GetIdOfPart(image)));
                }
            }
            _body.AppendChild(A4Section());
            main.Document = new Document(_body);
            main.Document.Save();
        }
        return ms.ToArray();
    }
}

/// <summary>
/// Таблицы листка заявки (как выгрузка из Jira): Segoe UI 10, рамки #CCCCCC, ячейки подписей
/// с фоном #F0F0F0, ширина — на всю ширину страницы.
/// </summary>
internal static class CardDocx
{
    private const string FontName = "Segoe UI";
    private const string DefaultSize = "20"; // 10 pt (в полупунктах)
    private const string LabelFill = "F0F0F0";
    private const string BorderColor = "CCCCCC";

    public static DateTime Local(DateTime utc) =>
        delosfera_server.Modules.Documents.VND.Services.VndWorkingCalendar.ToLocal(utc);

    /// <summary>Таблица с колонками заданной ширины в процентах (в сумме 100).</summary>
    public static Table Grid(params int[] percents)
    {
        BorderType B<T>() where T : BorderType, new() =>
            new T { Val = BorderValues.Single, Size = 4U, Color = BorderColor, Space = 0U };
        var table = new Table(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(B<TopBorder>(), B<LeftBorder>(), B<BottomBorder>(), B<RightBorder>(),
                B<InsideHorizontalBorder>(), B<InsideVerticalBorder>()),
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "30", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 60, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "30", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 60, Type = TableWidthValues.Dxa })));
        // Ширина текста на листе A4Section: 11906 − 1701 − 851 твипов.
        const int content = 11906 - 1701 - 851;
        var grid = new TableGrid();
        foreach (var p in percents) grid.AppendChild(new GridColumn { Width = (content * p / 100).ToString() });
        table.AppendChild(grid);
        return table;
    }

    public static TableRow Row(params TableCell[] cells)
    {
        var row = new TableRow(new TableRowProperties(new CantSplit()));
        foreach (var c in cells) row.AppendChild(c);
        return row;
    }

    /// <summary>
    /// Ячейка: label=true — жирная подпись на сером фоне. plain=true — белый фон и у подписи
    /// (как строки «Статус/Тип» в шапке Jira).
    /// </summary>
    public static TableCell Cell(string text, int percent, bool label = false, bool plain = false, int span = 1)
    {
        var tcPr = new TableCellProperties(new TableCellWidth { Width = (percent * 50).ToString(), Type = TableWidthUnitValues.Pct });
        if (span > 1) tcPr.AppendChild(new GridSpan { Val = span });
        if (label && !plain) tcPr.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = LabelFill });
        var cell = new TableCell(tcPr);
        var lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines) cell.AppendChild(Para(line, bold: label, spaced: false));
        return cell;
    }

    /// <summary>Заголовок шапки: «[номер] тема» крупно и жирно, справа мелко даты.</summary>
    public static TableCell TitleCell(string title, string sub, int span)
    {
        var tcPr = new TableCellProperties(
            new TableCellWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new GridSpan { Val = span },
            new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = LabelFill });
        var p = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "40", After = "40" }));
        p.AppendChild(MakeRun(title, bold: true, sizeHalfPt: 26));
        p.AppendChild(MakeRun("   " + sub, sizeHalfPt: 16));
        return new TableCell(tcPr, p);
    }

    /// <summary>Плашка раздела «Описание» — серый ярлык с белым жирным текстом.</summary>
    public static Table SectionTitle(string text)
    {
        var table = new Table(new TableProperties(
            new TableWidth { Width = "0", Type = TableWidthUnitValues.Auto },
            new TableCellMarginDefault(
                new TableCellLeftMargin { Width = 80, Type = TableWidthValues.Dxa },
                new TableCellRightMargin { Width = 80, Type = TableWidthValues.Dxa })));
        table.AppendChild(new TableGrid(new GridColumn { Width = "1400" }));
        var tcPr = new TableCellProperties(
            new TableCellWidth { Width = "0", Type = TableWidthUnitValues.Auto },
            new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "BBBBBB" });
        var p = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "20", After = "20" }));
        p.AppendChild(MakeRun(text, bold: true, color: "FFFFFF"));
        table.AppendChild(new TableRow(new TableCell(tcPr, p)));
        return table;
    }

    public static Paragraph Para(string text, bool bold = false, bool spaced = true, int sizeHalfPt = 0, string? color = null)
    {
        var p = new Paragraph(new ParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = spaced ? "60" : "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }));
        p.AppendChild(MakeRun(text, bold, sizeHalfPt == 0 ? null : sizeHalfPt, color));
        return p;
    }

    public static Paragraph Spacer() => Para("", false, false, 0, null);

    private static Run MakeRun(string text, bool bold = false, int? sizeHalfPt = null, string? color = null)
    {
        var size = sizeHalfPt?.ToString() ?? DefaultSize;
        var rPr = new RunProperties(
            new RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName, EastAsia = FontName });
        if (bold) rPr.AppendChild(new Bold());
        if (color is not null) rPr.AppendChild(new Color { Val = color });
        rPr.AppendChild(new FontSize { Val = size });
        rPr.AppendChild(new FontSizeComplexScript { Val = size });
        return new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }
}

/// <summary>
/// Абзацы и таблицы договора о материальной ответственности по образцу (Ф-94-01/МО):
/// Segoe UI 10, текст по ширине, реквизиты сторон — в две колонки без рамок.
/// </summary>
internal static class MoDocx
{
    private const string FontName = "Segoe UI";
    private const string FontSizeHalfPt = "20"; // 10 pt

    public static readonly JustificationValues Center = JustificationValues.Center;
    public static readonly JustificationValues Both = JustificationValues.Both;
    public static readonly JustificationValues Left = JustificationValues.Left;

    public static Run R(string text, bool bold = false, bool italic = false, bool underline = false, int size = 0)
    {
        var sz = size > 0 ? size.ToString() : FontSizeHalfPt;
        var rPr = new RunProperties(new RunFonts { Ascii = FontName, HighAnsi = FontName, ComplexScript = FontName, EastAsia = FontName });
        if (bold) rPr.AppendChild(new Bold());
        if (italic) rPr.AppendChild(new Italic());
        rPr.AppendChild(new FontSize { Val = sz });
        rPr.AppendChild(new FontSizeComplexScript { Val = sz });
        if (underline) rPr.AppendChild(new Underline { Val = UnderlineValues.Single });
        var run = new Run(rPr);
        // «\t» — настоящая табуляция Word (после буквы подпункта).
        var parts = text.Split('\t');
        for (var i = 0; i < parts.Length; i++)
        {
            if (i > 0) run.AppendChild(new TabChar());
            run.AppendChild(new Text(parts[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
        return run;
    }

    public static Paragraph P(JustificationValues align, params Run[] runs) => Pi(align, runs);

    /// <summary>Абзац с отступами: красная строка, левый отступ, висячий отступ, интервал перед.</summary>
    public static Paragraph Pi(JustificationValues align, Run[] runs, int firstLine = 0, int left = 0, int hanging = 0, int before = 0,
        int after = 0)
    {
        var pPr = new ParagraphProperties(
            new SpacingBetweenLines { Before = before.ToString(), After = after.ToString(), Line = "240", LineRule = LineSpacingRuleValues.Auto });
        if (firstLine > 0 || left > 0 || hanging > 0)
        {
            var ind = new Indentation { Left = left.ToString() };
            if (hanging > 0) ind.Hanging = hanging.ToString();
            else if (firstLine > 0) ind.FirstLine = firstLine.ToString();
            pPr.AppendChild(ind);
        }
        pPr.AppendChild(new Justification { Val = align });
        var p = new Paragraph(pPr);
        if (runs.Length == 0) runs = [R("")];
        foreach (var r in runs) p.AppendChild(r);
        return p;
    }

    /// <summary>Строка с левой табуляцией в <paramref name="tabPos"/> твипов (подпись: ФИО — черта — дата).</summary>
    public static Paragraph Tabbed(Run[] runs, int tabPos)
    {
        var pPr = new ParagraphProperties(
            new Tabs(new TabStop { Val = TabStopValues.Left, Position = tabPos }),
            new SpacingBetweenLines { Before = "0", After = "120", Line = "240", LineRule = LineSpacingRuleValues.Auto });
        var p = new Paragraph(pPr);
        foreach (var r in runs) p.AppendChild(r);
        return p;
    }

    /// <summary>«г. Бишкек» слева и дата справа (жирная, курсив, подчёркнута) — по правой табуляции.</summary>
    public static Paragraph CityDate(string city, string date)
    {
        const int content = 11906 - 1701 - 851;
        var pPr = new ParagraphProperties(
            new Tabs(new TabStop { Val = TabStopValues.Right, Position = content }),
            new SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto });
        return new Paragraph(pPr, R(city, bold: true), R("\t", bold: true), R(date, bold: true, italic: true, underline: true));
    }

    /// <summary>Две колонки без рамок (реквизиты / подписи сторон).</summary>
    public static Table TwoColumns(Paragraph[] left, Paragraph[] right)
    {
        const int content = 11906 - 1701 - 851;
        var half = content / 2;
        TableBorders NoBorders() => new(
            new TopBorder { Val = BorderValues.None }, new LeftBorder { Val = BorderValues.None },
            new BottomBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None },
            new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None });
        var table = new Table(new TableProperties(
            new TableWidth { Width = content.ToString(), Type = TableWidthUnitValues.Dxa },
            NoBorders(),
            new TableLayout { Type = TableLayoutValues.Fixed }));
        table.AppendChild(new TableGrid(new GridColumn { Width = half.ToString() }, new GridColumn { Width = half.ToString() }));
        TableCell Cell(Paragraph[] paragraphs)
        {
            var cell = new TableCell(new TableCellProperties(new TableCellWidth { Width = half.ToString(), Type = TableWidthUnitValues.Dxa }));
            foreach (var p in paragraphs.Length == 0 ? new[] { P(Left) } : paragraphs) cell.AppendChild(p);
            return cell;
        }
        table.AppendChild(new TableRow(Cell(left), Cell(right)));
        return table;
    }
}
