using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using delosfera_server.Data;
using delosfera_server.Modules.Documents.VND.Services;
using delosfera_server.Modules.Substitutions.Models;

namespace delosfera_server.Modules.Substitutions.Services;

/// <summary>Место подписи под документом: кто и в каком качестве должен подписать.</summary>
public sealed record SubstitutionSignerSlot(SubstitutionSignerRole Role, int? UserId);

/// <summary>Сведения о подписи для штампа в подписанной версии документа.</summary>
public sealed record SubstitutionStamp(
    SubstitutionSignerRole Role,
    string? SignerName,
    string? OnBehalfOfName,
    DateTime SignedAt,
    string? CertIssuer,
    string? CertValidNotAfter,
    string Hash,
    bool IsTest,
    // Автосогласование: под этой версией подписи нет, а SignedAt/Hash — от подписи под прежней
    // версией SignedVersion (документ изменили после подписи, новую подпись не поставили).
    bool AutoApproved = false,
    int? SignedVersion = null,
    // Место подписи в ЭЦП-версии: после абзаца Anchor текста (без рукописного блока подписей), с
    // отступом IndentMm от левого поля. null — в конце текста (подписи до появления выбора места).
    int? Anchor = null,
    int? IndentMm = null);

/// <summary>
/// Зафиксированные документы заявки на замещение (приказ и договор о матответственности) и
/// правила их подписания. Статические — чтобы и сервис заявок (переход «На подписании», правка
/// УЧР), и сервис подписей, и реестр задач брали одно и то же правило, не обрастая зависимостями
/// друг от друга.
/// </summary>
public static class SubstitutionDocuments
{
    /// <summary>Роль этапа директора филиала в маршруте (см. SubstitutionService.BuildApprovalRouteAsync).</summary>
    public const string DirectorStepRole = "Директор филиала";

    public static readonly SubstitutionDocumentKind[] Kinds =
        [SubstitutionDocumentKind.Order, SubstitutionDocumentKind.Liability];

    public static string KindTitle(SubstitutionDocumentKind kind) => kind switch
    {
        SubstitutionDocumentKind.Order => "Приказ о временном возложении обязанностей",
        SubstitutionDocumentKind.Liability => "Договор о полной материальной ответственности",
        _ => kind.ToString(),
    };

    public static string RoleTitle(SubstitutionDocumentKind kind, SubstitutionSignerRole role) => (kind, role) switch
    {
        (SubstitutionDocumentKind.Liability, SubstitutionSignerRole.Director) => "Директор (от Банка)",
        (SubstitutionDocumentKind.Liability, SubstitutionSignerRole.Substitute) => "Работник (замещающий)",
        (_, SubstitutionSignerRole.Director) => "Директор",
        (_, SubstitutionSignerRole.Absent) => "Отсутствующий — ознакомлен и согласен",
        (_, SubstitutionSignerRole.Substitute) => "Замещающий — ознакомлен и согласен",
        _ => role.ToString(),
    };

    /// <summary>Кто подписывает документ: приказ — директор, отсутствующий и замещающий;
    /// договор — директор (от Банка) и замещающий (Работник).</summary>
    public static List<SubstitutionSignerSlot> Slots(SubstitutionDocument d) => d.Kind switch
    {
        SubstitutionDocumentKind.Order =>
        [
            new(SubstitutionSignerRole.Director, d.DirectorUserId),
            new(SubstitutionSignerRole.Absent, d.AbsentUserId),
            new(SubstitutionSignerRole.Substitute, d.SubstituteUserId),
        ],
        _ =>
        [
            new(SubstitutionSignerRole.Director, d.DirectorUserId),
            new(SubstitutionSignerRole.Substitute, d.SubstituteUserId),
        ],
    };

    /// <summary>
    /// Зафиксировать документы для подписания: сформировать приказ и договор по текущим данным
    /// заявки и сохранить новой версией. Прежние версии перестают быть действующими (их подписи
    /// остаются в журнале). Вызывающий сохраняет изменения (SaveChanges) сам.
    /// </summary>
    public static async Task<List<SubstitutionDocument>> FreezeAsync(
        DelosferaDbContext db, ISubstitutionPrintService print, int requestId, CancellationToken ct = default)
    {
        var r = await db.SubstitutionRequests.AsNoTracking()
                    .Include(x => x.CommissionMembers)
                    .Include(x => x.Approvals)
                    .FirstOrDefaultAsync(x => x.Id == requestId, ct)
                ?? throw new KeyNotFoundException("Заявка на замещение не найдена");

        var director = await ResolveDirectorAsync(db, r, ct);

        var existing = await db.SubstitutionDocuments.Where(x => x.RequestId == requestId).ToListAsync(ct);
        foreach (var old in existing.Where(x => x.IsCurrent)) old.IsCurrent = false;

        var stamp = r.RegNumber ?? requestId.ToString();
        var now = DateTime.UtcNow;
        var created = new List<SubstitutionDocument>();
        foreach (var kind in Kinds)
        {
            var bytes = kind == SubstitutionDocumentKind.Order ? print.Order(r) : print.Liability(r);
            var doc = new SubstitutionDocument
            {
                RequestId = requestId,
                Kind = kind,
                Version = existing.Where(x => x.Kind == kind).Select(x => x.Version).DefaultIfEmpty(0).Max() + 1,
                IsCurrent = true,
                FileName = kind == SubstitutionDocumentKind.Order
                    ? $"Приказ о возложении обязанностей {stamp}.docx"
                    : $"Договор о матответственности {stamp}.docx",
                Content = bytes,
                ContentSha256 = ResolutionSignatureHasher.Sha256Hex(bytes),
                CreatedAt = now,
                DirectorUserId = director,
                AbsentUserId = kind == SubstitutionDocumentKind.Order ? r.AbsentUserId : null,
                SubstituteUserId = r.SubstituteUserId,
            };
            db.SubstitutionDocuments.Add(doc);
            created.Add(doc);
        }
        return created;
    }

    /// <summary>
    /// Директор, подписывающий приказ и договор от имени Банка: у филиала — согласующий этапа
    /// «Директор филиала» действующего круга (с учётом переназначения), иначе — начальник
    /// филиала по оргструктуре. У Головного офиса директора филиала нет — подписант берётся из
    /// настроек маршрута УЧР (HrRoutingSettings.HeadOfficeSignerUserId).
    /// </summary>
    public static async Task<int?> ResolveDirectorAsync(DelosferaDbContext db, SubstitutionRequest r, CancellationToken ct)
    {
        var round = r.Approvals.Select(a => a.Round).DefaultIfEmpty(1).Max();
        var step = r.Approvals.FirstOrDefault(a => a.Round == round && a.RoleLabel == DirectorStepRole);
        if (step is not null) return step.UserId;

        var (isBranch, head) = await BranchOfAsync(db, r.AbsentUnitId, ct);
        if (isBranch) return head;

        return await db.HrRoutingSettings.AsNoTracking()
            .Select(s => s.HeadOfficeSignerUserId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Филиал подразделения: поднимаемся по дереву до подразделения с «Филиал» в названии.</summary>
    public static async Task<(bool IsBranch, int? HeadUserId)> BranchOfAsync(
        DelosferaDbContext db, int? unitId, CancellationToken ct)
    {
        var guard = 0;
        var current = unitId;
        while (current is { } id && guard++ < 20)
        {
            var u = await db.OrganizationUnits.AsNoTracking().Where(x => x.Id == id)
                .Select(x => new { x.TitleRu, x.HeadUserId, x.ParentId }).FirstOrDefaultAsync(ct);
            if (u is null) return (false, null);
            if (u.TitleRu.Contains("Филиал", StringComparison.OrdinalIgnoreCase)) return (true, u.HeadUserId);
            current = u.ParentId;
        }
        return (false, null);
    }

    // ── ЭЦП-версия: подписи на выбранных подписантами местах ────────────────────────────────────

    /// <summary>Наибольший отступ блока подписи от левого поля, мм (блок шириной ~80 мм остаётся в пределах листа).</summary>
    public const int MaxIndentMm = 85;

    /// <summary>Префикс закладок-якорей в макете для выбора места подписи (dsa_0, dsa_1, …).</summary>
    public const string AnchorBookmarkPrefix = "dsa_";

    private const int StampWidthMm = 75;
    private const string StampColor = "1F3A93";
    private const string StampFill = "F2F5FD";
    private const string StampMuted = "7A869A";
    private const string StampTest = "B45309";
    private const string StampAuto = "B3730A";
    private const string PendingColor = "A3ACBC";

    private static readonly string[] MonthsGenitive =
    [
        "января", "февраля", "марта", "апреля", "мая", "июня",
        "июля", "августа", "сентября", "октября", "ноября", "декабря",
    ];

    /// <summary>Дата «как от руки» в строке подписи: «20» октября 2026 г.</summary>
    public static string PrettyDate(DateTime local) =>
        $"«{local.Day:00}» {MonthsGenitive[local.Month - 1]} {local.Year} г.";

    /// <summary>
    /// Сколько мест («после абзаца N») можно выбрать под подпись: число абзацев и таблиц документа
    /// без рукописного блока подписей. Место подписи — индекс от 0 до AnchorCount − 1.
    /// </summary>
    public static int AnchorCount(SubstitutionDocument doc)
    {
        using var ms = new MemoryStream(doc.Content, writable: false);
        using var word = WordprocessingDocument.Open(ms, false);
        var body = word.MainDocumentPart!.Document.Body!;
        return SplitSignatureBlock(body, doc.Kind).Content.Count;
    }

    /// <summary>
    /// Тело документа делится на текст и рукописный блок подписей в конце (его строки с «______»
    /// в ЭЦП-версии заменяются блоками подписей): у приказа — с последней строки «Директор …», у
    /// договора — с последней строки «Банк: …». Блок определяется по тексту, поэтому подходит и для
    /// версий, зафиксированных до появления выбора места подписи.
    /// </summary>
    private static (List<OpenXmlElement> Content, List<OpenXmlElement> SignatureBlock) SplitSignatureBlock(
        Body body, SubstitutionDocumentKind kind)
    {
        var all = body.ChildElements.Where(e => e is W.Paragraph or Table).ToList();
        var marker = kind == SubstitutionDocumentKind.Order ? "Директор" : "Банк:";
        var cut = all.FindLastIndex(e => e is W.Paragraph p && TextOf(p).TrimStart().StartsWith(marker, StringComparison.Ordinal));
        if (cut < 0) return (all, []);
        return (all.Take(cut).ToList(), all.Skip(cut).ToList());
    }

    private static string TextOf(OpenXmlElement e) => string.Concat(e.Descendants<Text>().Select(t => t.Text));

    /// <summary>Место подписи в пределах документа: null или вне диапазона — в конце текста.</summary>
    public static (int Anchor, int IndentMm) NormalizePlacement(int? anchor, int? indentMm, int anchorCount)
    {
        var last = Math.Max(0, anchorCount - 1);
        var a = anchor is { } x && x >= 0 && x <= last ? x : last;
        var i = Math.Clamp(indentMm ?? 0, 0, MaxIndentMm);
        return (a, i);
    }

    /// <summary>
    /// Подписанная версия документа (ЭЦП): зафиксированный файл, в котором рукописный блок подписей
    /// заменён блоками подписей на местах, выбранных подписантами (штамп ЭЦП + «Директор ____» /
    /// «Ознакомлены и согласны: Фамилия И.О. ____ «дата»»); места, где подписи ещё нет, — серые.
    /// В конце — лист подписей ЭЦП (кто, в каком качестве, когда, сертификат, хэш).
    /// Исходный файл не меняется: «без подписей» отдаётся он сам, байт в байт тот, что подписывали.
    ///
    /// <paramref name="layout"/> — макет для окна выбора места подписи: только уже поставленные
    /// подписи (без серых мест и листа подписей) и закладки-якоря dsa_N на каждом абзаце —
    /// по ним клиент понимает, после какого абзаца ставится подпись.
    /// </summary>
    public static byte[] WithStamps(SubstitutionDocument doc, IReadOnlyList<SubstitutionStamp> stamps, TimeZoneInfo zone,
        IReadOnlyDictionary<SubstitutionSignerRole, string?>? slotNames = null, bool layout = false)
    {
        using var ms = new MemoryStream();
        ms.Write(doc.Content);
        using (var word = WordprocessingDocument.Open(ms, true))
        {
            var body = word.MainDocumentPart!.Document.Body!;
            // Параметры страницы должны оставаться последним элементом тела: лист вставляем перед
            // ними. У документов, зафиксированных до появления параметров, — добавляем A4.
            var sect = body.Elements<SectionProperties>().LastOrDefault();
            if (sect is null)
            {
                sect = Docx.A4Section();
                body.AppendChild(sect);
            }

            var (content, signatureBlock) = SplitSignatureBlock(body, doc.Kind);
            foreach (var e in signatureBlock) e.Remove();
            if (content.Count == 0)
            {
                var empty = new Paragraph();
                body.InsertBefore(empty, sect);
                content.Add(empty);
            }

            PlaceSignatureBlocks(doc, content, stamps, zone, slotNames ?? new Dictionary<SubstitutionSignerRole, string?>(),
                ContentWidthTwips(sect), layout);

            if (layout)
                AddAnchorBookmarks(content);
            else
                AppendSignatureSheet(body, sect, doc, stamps, zone);

            word.MainDocumentPart.Document.Save();
        }
        return ms.ToArray();
    }

    private sealed record PlacedBlock(SubstitutionSignerRole Role, SubstitutionStamp? Stamp, int Anchor, int IndentMm);

    /// <summary>
    /// Блоки подписей по местам: подписанные — после выбранного абзаца (несколько на одном месте —
    /// в порядке подписания), без места (подписаны до появления выбора, автосогласование) и ещё не
    /// подписанные — в конце текста. «Ознакомлены и согласны:» ставится один раз — перед первым по
    /// тексту блоком отсутствующего/замещающего в приказе.
    /// </summary>
    private static void PlaceSignatureBlocks(SubstitutionDocument doc, List<OpenXmlElement> content,
        IReadOnlyList<SubstitutionStamp> stamps, TimeZoneInfo zone,
        IReadOnlyDictionary<SubstitutionSignerRole, string?> slotNames, int contentTwips, bool layout)
    {
        var slots = Slots(doc);
        var signed = new List<PlacedBlock>();
        var pending = new List<PlacedBlock>();
        foreach (var slot in slots)
        {
            var s = stamps.FirstOrDefault(x => x.Role == slot.Role);
            if (s is not null)
            {
                var (anchor, indent) = NormalizePlacement(s.Anchor, s.IndentMm, content.Count);
                signed.Add(new PlacedBlock(slot.Role, s, anchor, indent));
            }
            else if (!layout)
            {
                pending.Add(new PlacedBlock(slot.Role, null, content.Count - 1, 0));
            }
        }

        var ordered = signed
            .OrderBy(b => b.Anchor)
            .ThenBy(b => b.Stamp!.SignedAt)
            .Concat(pending)
            .OrderBy(b => b.Anchor) // устойчивая сортировка: на одном месте — подписанные, затем серые
            .ToList();

        var headerDone = false;
        var tail = new Dictionary<int, OpenXmlElement>();
        foreach (var b in ordered)
        {
            var ack = doc.Kind == SubstitutionDocumentKind.Order && b.Role is SubstitutionSignerRole.Absent or SubstitutionSignerRole.Substitute;
            var withHeader = ack && !headerDone;
            if (ack) headerDone = true;

            var after = tail.TryGetValue(b.Anchor, out var t) ? t : content[b.Anchor];
            foreach (var el in BuildBlock(doc.Kind, b, slotNames.GetValueOrDefault(b.Role), withHeader, zone, contentTwips))
            {
                after.InsertAfterSelf(el);
                after = el;
            }
            tail[b.Anchor] = after;
        }
    }

    /// <summary>Закладка dsa_N в начале каждого абзаца/таблицы текста — якоря для окна выбора места.</summary>
    private static void AddAnchorBookmarks(List<OpenXmlElement> content)
    {
        for (var i = 0; i < content.Count; i++)
        {
            var p = content[i] as W.Paragraph ?? content[i].Descendants<Paragraph>().FirstOrDefault();
            if (p is null) continue;
            var id = (900000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var start = new BookmarkStart { Name = AnchorBookmarkPrefix + i, Id = id };
            var end = new BookmarkEnd { Id = id };
            if (p.ParagraphProperties is { } pPr) pPr.InsertAfterSelf(start);
            else p.PrependChild(start);
            start.InsertAfterSelf(end);
        }
    }

    private static int ContentWidthTwips(SectionProperties sect)
    {
        var width = (int?)sect.GetFirstChild<PageSize>()?.Width?.Value ?? 11906;
        var margin = sect.GetFirstChild<PageMargin>();
        var left = (int?)margin?.Left?.Value ?? 1701;
        var right = (int?)margin?.Right?.Value ?? 851;
        return Math.Max(4000, width - left - right);
    }

    private static int MmToTwips(int mm) => (int)Math.Round(mm * 1440 / 25.4);

    // Колонки строки подписи, мм: подпись слева («Директор» / «Фамилия И.О.»), штамп ЭЦП, дата.
    private const int LabelOrderDirectorMm = 30;
    private const int LabelAckMm = 35;
    private const int LabelLiabilityMm = 28;
    private const int DateColumnMm = 50;

    /// <summary>
    /// Ширина строки подписи (мм) — сколько места занимает блок по горизонтали; отступ места подписи
    /// ограничивается так, чтобы строка не выходила за правое поле (то же правило — в окне выбора места).
    /// </summary>
    public static int BlockWidthMm(SubstitutionDocumentKind kind, SubstitutionSignerRole role) => (kind, role) switch
    {
        (SubstitutionDocumentKind.Order, SubstitutionSignerRole.Director) => LabelOrderDirectorMm + StampWidthMm,
        (SubstitutionDocumentKind.Order, _) => LabelAckMm + StampWidthMm + DateColumnMm,
        _ => LabelLiabilityMm + StampWidthMm + DateColumnMm,
    };

    /// <summary>
    /// Блок одной подписи: (заголовок) + строка «подпись — штамп ЭЦП (— дата)» вместо рукописной
    /// черты. Приказ: «Директор [штамп]»; отсутствующий/замещающий — («Ознакомлены и согласны:»)
    /// «Фамилия И.О. [штамп] «20» октября 2026 г.». Договор: «Банк: ОАО «Керемет Банк»» /
    /// «Работник: ФИО», затем «Директор / Подпись [штамп] «дата»». Без подписи — то же серым,
    /// на месте штампа — пунктирная рамка «Место для подписи ЭЦП».
    /// </summary>
    private static IEnumerable<OpenXmlElement> BuildBlock(SubstitutionDocumentKind kind, PlacedBlock b, string? fullName,
        bool ackHeader, TimeZoneInfo zone, int contentTwips)
    {
        var s = b.Stamp;
        var color = s is null ? PendingColor : null;
        var rowTwips = MmToTwips(BlockWidthMm(kind, b.Role));
        var left = Math.Max(0, Math.Min(MmToTwips(b.IndentMm), contentTwips - rowTwips));
        var local = s is null ? (DateTime?)null
            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.SignedAt, DateTimeKind.Utc), zone);
        var date = local is { } d ? PrettyDate(d) : "«____»__________ ____ г.";

        // Заголовок над строкой — или пустой абзац-отбивка: он же не даёт соседним строкам-таблицам
        // слиться в одну таблицу.
        string? header = null;
        if (kind == SubstitutionDocumentKind.Order)
        {
            if (ackHeader) header = "Ознакомлены и согласны:";
        }
        else
        {
            header = b.Role == SubstitutionSignerRole.Director
                ? "Банк: ОАО «Керемет Банк»"
                : $"Работник: {(string.IsNullOrWhiteSpace(fullName) ? "—" : fullName.Trim())}";
        }
        yield return header is null
            ? SignLine("", left, bold: false, color, before: 120, keepNext: true, size: 8)
            : SignLine(header, left, bold: kind != SubstitutionDocumentKind.Order, color, before: 200, keepNext: true);

        var stamp = StampBox(s, 0, 0, local, before: 0);
        if (kind == SubstitutionDocumentKind.Order && b.Role == SubstitutionSignerRole.Director)
        {
            yield return SignRow(left,
                (LabelOrderDirectorMm, SignLine("Директор", 0, bold: true, color, 0, false)),
                (StampWidthMm, stamp));
        }
        else if (kind == SubstitutionDocumentKind.Order)
        {
            yield return SignRow(left,
                (LabelAckMm, SignLine(SubstitutionPrintService.Short(fullName), 0, bold: false, color, 0, false)),
                (StampWidthMm, stamp),
                (DateColumnMm, SignLine(date, 0, bold: false, color, 0, false, indentLeft: 120)));
        }
        else
        {
            yield return SignRow(left,
                (LabelLiabilityMm, SignLine(b.Role == SubstitutionSignerRole.Director ? "Директор" : "Подпись", 0, bold: false, color, 0, false)),
                (StampWidthMm, stamp),
                (DateColumnMm, SignLine(date, 0, bold: false, color, 0, false, indentLeft: 120)));
        }
    }

    /// <summary>
    /// Строка подписи — таблица без рамок в одну строку: подпись, штамп, дата по центру по вертикали.
    /// Отступ места подписи — пустой первой колонкой (так он одинаково виден и в Word, и в просмотре).
    /// </summary>
    private static Table SignRow(int leftTwips, params (int WidthMm, Paragraph Content)[] cells)
    {
        var columns = new List<(int Twips, Paragraph Content)>();
        if (leftTwips > 20) columns.Add((leftTwips, new Paragraph()));
        columns.AddRange(cells.Select(c => (MmToTwips(c.WidthMm), c.Content)));

        TableBorders NoBorders() => new(
            new TopBorder { Val = BorderValues.None }, new LeftBorder { Val = BorderValues.None },
            new BottomBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None },
            new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None });

        var table = new Table(new TableProperties(
            new TableWidth { Width = columns.Sum(c => c.Twips).ToString(), Type = TableWidthUnitValues.Dxa },
            NoBorders(),
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 0, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 0, Type = TableWidthValues.Dxa })));
        table.AppendChild(new TableGrid(columns.Select(c => new GridColumn { Width = c.Twips.ToString() })));
        var row = new TableRow(new TableRowProperties(new CantSplit()));
        foreach (var (twips, content) in columns)
        {
            row.AppendChild(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = twips.ToString(), Type = TableWidthUnitValues.Dxa },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
                content));
        }
        table.AppendChild(row);
        return table;
    }

    /// <summary>Строка подписи — шрифтом документа (Times New Roman 12), с отступом места подписи.</summary>
    private static Paragraph SignLine(string text, int leftTwips, bool bold, string? color, int before, bool keepNext,
        int size = 24, int indentLeft = 0)
    {
        var pPr = new ParagraphProperties();
        if (keepNext) pPr.AppendChild(new KeepNext());
        pPr.AppendChild(new SpacingBetweenLines { Before = before.ToString(), After = size < 24 ? "0" : "60" });
        pPr.AppendChild(new Indentation { Left = (leftTwips + indentLeft).ToString() });
        pPr.AppendChild(new Justification { Val = JustificationValues.Left });
        // Порядок свойств — по схеме OOXML: шрифт, жирность, цвет, размер.
        var rPr = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" });
        if (bold) rPr.AppendChild(new Bold());
        if (color is not null) rPr.AppendChild(new Color { Val = color });
        rPr.Append(new FontSize { Val = size.ToString() }, new FontSizeComplexScript { Val = size.ToString() });
        return new Paragraph(pPr, new Run(rPr, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    /// <summary>
    /// Штамп ЭЦП — как у листа согласования ВНД: двойная синяя рамка, светлая заливка; один абзац с
    /// переносами строк (рамка абзаца одинаково выглядит в Word и в просмотре в браузере). Тест —
    /// оранжевый, автосогласование — янтарный, ещё не подписано — серая пунктирная рамка.
    /// </summary>
    private static Paragraph StampBox(SubstitutionStamp? s, int leftTwips, int rightTwips, DateTime? local, int before)
    {
        var accent = s is null ? PendingColor : s.IsTest ? StampTest : s.AutoApproved ? StampAuto : StampColor;
        var border = s is null ? BorderValues.Dashed : BorderValues.Double;
        var pPr = new ParagraphProperties(
            new KeepNext(),
            new ParagraphBorders(
                new TopBorder { Val = border, Size = 6, Color = accent, Space = 2 },
                new LeftBorder { Val = border, Size = 6, Color = accent, Space = 4 },
                new BottomBorder { Val = border, Size = 6, Color = accent, Space = 2 },
                new RightBorder { Val = border, Size = 6, Color = accent, Space = 4 }),
            new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = s is null ? "FFFFFF" : StampFill },
            new SpacingBetweenLines { Before = before.ToString(), After = "40", Line = "228", LineRule = LineSpacingRuleValues.Auto },
            new Indentation { Left = (leftTwips + 80).ToString(), Right = (rightTwips + 80).ToString() },
            new Justification { Val = JustificationValues.Center });
        var p = new Paragraph(pPr);

        var firstLine = true;
        void Add(string text, bool bold = false, int size = 14, string? color = null, string font = "Arial")
        {
            var rPr = new RunProperties(
                new RunFonts { Ascii = font, HighAnsi = font, ComplexScript = font },
                new Color { Val = color ?? accent },
                new FontSize { Val = size.ToString() },
                new FontSizeComplexScript { Val = size.ToString() });
            if (bold) rPr.InsertAfter(new Bold(), rPr.GetFirstChild<RunFonts>());
            var run = new Run(rPr);
            if (!firstLine) run.AppendChild(new Break());
            run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            p.AppendChild(run);
            firstLine = false;
        }

        if (s is null)
        {
            Add("МЕСТО ДЛЯ ПОДПИСИ ЭЦП", bold: true, size: 15);
            Add("ожидает подписи");
            return p;
        }

        if (s.IsTest)
        {
            Add("ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП", bold: true, size: 15);
            Add("не является подписью", size: 12);
        }
        else if (s.AutoApproved)
        {
            Add("АВТОСОГЛАСОВАНИЕ", bold: true, size: 15);
            Add($"подписано ЭЦП в версии {s.SignedVersion?.ToString() ?? "—"}", size: 12);
        }
        else
        {
            Add("ПОДПИСАНО С ПОМОЩЬЮ ЭЦП", bold: true, size: 15);
            Add("облачная ЭЦП ГП «Инфоком» · подпись проверена", size: 12, color: StampMuted);
        }
        var who = s.OnBehalfOfName is null ? s.SignerName : $"{s.SignerName} (за {s.OnBehalfOfName})";
        if (!string.IsNullOrWhiteSpace(who)) Add($"Владелец: {who}", bold: true);
        if (!string.IsNullOrWhiteSpace(s.CertIssuer) || !string.IsNullOrWhiteSpace(s.CertValidNotAfter))
            Add($"Сертификат: {s.CertIssuer ?? "—"}{(string.IsNullOrWhiteSpace(s.CertValidNotAfter) ? "" : $", до {CertDate(s.CertValidNotAfter)}")}", size: 12);
        if (local is { } at) Add($"Подписано: {at:dd.MM.yyyy HH:mm}");
        Add($"Хэш: {ShortHash(s.Hash)}", size: 12, color: StampMuted, font: "Consolas");
        return p;
    }

    private static string ShortHash(string hex)
    {
        var text = hex.Trim().ToUpperInvariant();
        return text.Length <= 24 ? text : $"{text[..12]}…{text[^12..]}";
    }

    private static string CertDate(string raw) =>
        DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal, out var date)
            ? date.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture)
            : raw.Trim();

    /// <summary>Лист подписей ЭЦП в конце документа — с новой страницы, как лист согласования у ВНД.</summary>
    private static void AppendSignatureSheet(Body body, SectionProperties sect, SubstitutionDocument doc,
        IReadOnlyList<SubstitutionStamp> stamps, TimeZoneInfo zone)
    {
        void Add(OpenXmlElement e) => body.InsertBefore(e, sect);

        Add(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
        Add(Paragraph("ЛИСТ ПОДПИСЕЙ ЭЦП", bold: true, size: 26, center: true));
        Add(Paragraph($"{KindTitle(doc.Kind)} · версия {doc.Version}", size: 22, center: true));
        if (!doc.IsCurrent)
            Add(Paragraph("ВЕРСИЯ НЕАКТУАЛЬНА: документ изменён после подписания, действует более новая версия.",
                bold: true, size: 20, center: true, color: "C0392B"));
        Add(Paragraph(""));

        var table = new Table(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "8B97AB" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "8B97AB" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "8B97AB" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "8B97AB" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "8B97AB" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "8B97AB" }),
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 100, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 100, Type = TableWidthValues.Dxa })));
        // № | Подписант (роль) | Подпись ЭЦП
        int[] widths = [500, 3300, 5550];
        table.AppendChild(new TableGrid(widths.Select(w => new GridColumn { Width = w.ToString() })));
        table.AppendChild(Row(widths, header: true,
            [Paragraph("№", bold: true, size: 20, center: true)],
            [Paragraph("Подписант", bold: true, size: 20)],
            [Paragraph("Подпись ЭЦП", bold: true, size: 20)]));

        var n = 0;
        foreach (var slot in Slots(doc))
        {
            n++;
            var title = RoleTitle(doc.Kind, slot.Role);
            var s = stamps.FirstOrDefault(x => x.Role == slot.Role);
            var who = s is null ? null : s.OnBehalfOfName is null ? s.SignerName : $"{s.SignerName} (за {s.OnBehalfOfName})";

            var signer = new List<Paragraph> { Paragraph(title, bold: true, size: 20) };
            if (who is not null) signer.Add(Paragraph(who, size: 20));

            var stamp = new List<Paragraph>();
            if (s is null)
            {
                stamp.Add(Paragraph("Не подписано", size: 20, color: "8B97AB"));
            }
            else
            {
                var at = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.SignedAt, DateTimeKind.Utc), zone);
                if (s.AutoApproved)
                {
                    stamp.Add(Paragraph("АВТОСОГЛАСОВАНИЕ", bold: true, size: 20, color: "B3730A"));
                    stamp.Add(Paragraph(
                        $"Подписано ЭЦП в версии {s.SignedVersion?.ToString() ?? "—"} — {at:dd.MM.yyyy HH:mm}" +
                        $"{(s.IsTest ? " (тестовая имитация)" : "")}. После изменения документа новая подпись " +
                        "не поставлена — согласие подписанта считается сохранённым.", size: 18));
                }
                else
                {
                    stamp.Add(s.IsTest
                        ? Paragraph("ТЕСТОВАЯ ИМИТАЦИЯ ЭЦП (не является подписью)", bold: true, size: 20, color: "B3730A")
                        : Paragraph("Подписано с помощью ЭЦП", bold: true, size: 20, color: "1C7A4D"));
                    stamp.Add(Paragraph($"Дата и время: {at:dd.MM.yyyy HH:mm}", size: 18));
                }
                if (!string.IsNullOrWhiteSpace(s.CertIssuer) || !string.IsNullOrWhiteSpace(s.CertValidNotAfter))
                    stamp.Add(Paragraph(
                        $"Сертификат: {s.CertIssuer ?? "—"}{(string.IsNullOrWhiteSpace(s.CertValidNotAfter) ? "" : $", действителен до {s.CertValidNotAfter}")}",
                        size: 18));
                stamp.Add(Paragraph($"Хэш: {s.Hash}", size: 16, color: "55617A"));
            }

            table.AppendChild(Row(widths, header: false,
                [Paragraph(n.ToString(), size: 20, center: true)], signer, stamp));
        }
        Add(table);

        Add(Paragraph(""));
        Add(Paragraph($"Контрольная сумма файла документа (SHA-256): {doc.ContentSha256}", size: 16, color: "55617A"));
    }

    private static TableRow Row(int[] widths, bool header, params IEnumerable<Paragraph>[] cells)
    {
        var row = new TableRow();
        if (header) row.AppendChild(new TableRowProperties(new TableHeader()));
        for (var i = 0; i < cells.Length; i++)
        {
            var props = new TableCellProperties(
                new TableCellWidth { Width = widths[i].ToString(), Type = TableWidthUnitValues.Dxa });
            if (header) props.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "EEF1F5", Color = "auto" });
            var cell = new TableCell(props);
            foreach (var p in cells[i]) cell.AppendChild(p);
            row.AppendChild(cell);
        }
        return row;
    }

    private static Paragraph Paragraph(string text, bool bold = false, int size = 24, bool center = false, string? color = null)
    {
        var rPr = new RunProperties(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" });
        if (bold) rPr.AppendChild(new Bold());
        if (color is not null) rPr.AppendChild(new Color { Val = color });
        rPr.Append(new FontSize { Val = size.ToString() }, new FontSizeComplexScript { Val = size.ToString() });
        var run = new Run();
        run.AppendChild(rPr);
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        var p = new Paragraph();
        var pPr = new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "40" });
        if (center) pPr.AppendChild(new Justification { Val = JustificationValues.Center });
        p.AppendChild(pPr);
        p.AppendChild(run);
        return p;
    }
}
