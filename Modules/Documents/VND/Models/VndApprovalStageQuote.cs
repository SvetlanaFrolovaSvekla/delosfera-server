namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Цитата из текста редакции, на которую согласующий сослался в своей резолюции
/// (см. "+ Сослаться на текст редакции" в VndApproverResolutionPanel на клиенте) — привязана
/// к конкретной фазе решения, как и <see cref="VndApprovalStageAttachment"/>.
///
/// Сам текст комментария/замечания к этой цитате отдельно не хранится — при клике на метку
/// в тексте показывается вся резолюция этой фазы целиком (Primary/Repeat/FinalHoldComment на
/// <see cref="VndApprovalStage"/>), там же обычно и находится пояснение согласующего.
///
/// Text хранится как есть и используется для повторного поиска этого фрагмента в отрендеренном
/// docx при отображении меток в "Просмотр редакции" — документ рендерится через docx-preview
/// в обычный HTML, поиск по позиции символа в исходном Word-файле был бы куда сложнее и
/// потребовал бы отдельного маппинга, поиск по тексту — самый практичный вариант.
///
/// Хранится бессрочно, наравне с текстом самой резолюции.</summary>
public class VndApprovalStageQuote
{
    public int Id { get; set; }

    public int VndApprovalStageId { get; set; }
    public VndApprovalStage? VndApprovalStage { get; set; }

    /// <summary>К решению какой фазы относится цитата</summary>
    public ApprovalStagePhase Phase { get; set; }

    /// <summary>Из какого документа редакции процитировано — "ru"/"kg"/"en"/"tid"/
    /// "approvalSheet"/"disagreementMatrix" (см. RedactionViewTarget на клиенте).</summary>
    public required string DocumentTarget { get; set; }

    public required string Text { get; set; }

    /// <summary>Версия документа редакции, к которой относится цитата (0 - самая первая
    /// поданная версия "Р1", 1 - "Р1.1" и т.д.) — снимается в момент принятия решения как
    /// количество уже существующих <see cref="VndRedactionRevisionSnapshot"/> этой редакции
    /// (см. VndApprovalService.DecideAsync/AttachDecisionQuotes). Совпадает с тем, каким будет
    /// SnapshotNumber - 1 у снимка, который снимет следующая повторная отправка, если версия,
    /// к которой относится цитата, будет вытеснена ею. Нужно, чтобы при просмотре КОНКРЕТНОЙ
    /// версии документа показывать только цитаты/замечания именно этой версии, а не всех версий
    /// разом (иначе замечания к старой версии "приклеивались" бы к новой, уже исправленной) -
    /// см. VndApprovalService.ToQuoteResponses.</summary>
    public int RevisionIndex { get; set; }

    public DateTime CreatedAt { get; set; }
}
