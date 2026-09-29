namespace delosfera_server.Modules.Documents.VND.DTO.Request;

/// <summary>Тело запроса хэша резолюции — только текст резолюции. Содержимое документа и его
/// код клиент НЕ присылает: backend берёт их из БД по vndId/stageId сам (см.
/// ResolutionSignatureService.GetHashForStageAsync).</summary>
public class ResolutionHashRequest
{
    public string? Comment { get; set; }

    /// <summary>Решение, под которым ставится подпись (входит в хэш, схема v2). По умолчанию "Согласовать".</summary>
    public ApprovalDecisionType Decision { get; set; } = ApprovalDecisionType.Approve;
}
