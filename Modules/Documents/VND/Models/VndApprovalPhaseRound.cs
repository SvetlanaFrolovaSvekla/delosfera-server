using delosfera_server.Common.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Снимок одного КРУГА фазы "Повторное согласование" (Repeat) или "Финальная
/// выдержка" (FinalHold), сделанный непосредственно перед тем, как поля решения на
/// <see cref="VndApprovalStage"/> (Repeat*/FinalHold*) будут перезаписаны следующим кругом —
/// см. VndApprovalService.SnapshotPhaseRoundIfNeededAsync.
///
/// Зачем это нужно: RepeatDecision/RepeatComment/RepeatDecidedAt (и FinalHoldDecision и т.п.)
/// на VndApprovalStage - это только ТЕКУЩИЙ круг. Если замечания устраняли несколько раз
/// подряд в рамках одного и того же процесса согласования (без отклонения всей редакции),
/// каждая повторная отправка (ResubmitAfterRevisionAsync) раньше просто затирала предыдущее
/// решение и комментарий - история промежуточных кругов терялась безвозвратно. Эта таблица
/// сохраняет такой снимок ДО затирания, чтобы в истории маршрута согласования
/// (см. VndRedactionHistoryDetail.tsx на клиенте) можно было показать каждый круг отдельной
/// схемой: "Первичное согласование" → "Согласование после внесённых изменений" (круг 1, 2,
/// 3...) → "Финальная выдержка" (может тоже повториться, если на ней снова оставили
/// замечание) - именно в таком порядке.
///
/// Вложения к решениям этого круга НЕ удаляются, а привязываются к нему (см.
/// <see cref="VndApprovalStageAttachment.PhaseRoundId"/>, Attachments ниже); цитаты кругов
/// хранятся бессрочно и отличаются версией документа (VndApprovalStageQuote.RevisionIndex).</summary>
public class VndApprovalPhaseRound
{
    public int Id { get; set; }

    public int ApprovalProcessId { get; set; }
    public VndApprovalProcess? ApprovalProcess { get; set; }

    /// <summary>Repeat или FinalHold — Primary никогда сюда не попадает, у него всегда ровно
    /// один круг (собственные поля Primary* на VndApprovalStage никогда не перезаписываются).</summary>
    public ApprovalStagePhase Phase { get; set; }

    /// <summary>Порядковый номер круга внутри этой фазы этого процесса, начиная с 1.</summary>
    public int RoundNumber { get; set; }

    /// <summary>Когда начался именно этот круг (снимок VndApprovalProcess.RepeatStartedAt /
    /// FinalHoldStartedAt на момент старта круга, до перезаписи следующим).</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Когда круг завершился (был перезаписан следующим) - фактически момент создания
    /// этой записи.</summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>Комментарий инициатора об исправлениях на этом круге (снимок
    /// VndApprovalProcess.RepeatInitiatorComment) - заполняется только для Phase == Repeat,
    /// у финальной выдержки такого комментария нет.</summary>
    public string? InitiatorComment { get; set; }

    public ICollection<VndApprovalPhaseRoundStageDecision> StageDecisions { get; set; } =
        new List<VndApprovalPhaseRoundStageDecision>();

    /// <summary>Вложения, приложенные согласующими к решениям ЭТОГО круга (см.
    /// VndApprovalStageAttachment.PhaseRoundId).</summary>
    public ICollection<VndApprovalStageAttachment> Attachments { get; set; } =
        new List<VndApprovalStageAttachment>();

    public DateTime CreatedAt { get; set; }
}
