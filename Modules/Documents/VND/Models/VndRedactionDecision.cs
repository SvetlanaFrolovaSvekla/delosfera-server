using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/// <summary>Вид решения органа/руководства по редакции ВНД (строка блока «Внесены изменения,
/// дополнения» Титульного листа).</summary>
public enum VndRedactionDecisionKind
{
    /// <summary>Утверждение самой редакции — проставляется при первой консолидации редакции
    /// (VndActualizationService.PublishAsync), когда инициатор вносит дату и № принятия.</summary>
    Adoption = 0,

    /// <summary>Повторное утверждение уже действующей редакции без выпуска новой — актуализация
    /// «без изменений», консолидированная с новым протоколом/СЗ/приказом.</summary>
    NoChangesActualization = 1,
}

/// <summary>Журнал решений по редакции ВНД: утверждение редакции и все последующие решения по ней
/// (актуализация без изменений и т.п.). Это источник для блока «Внесены изменения, дополнения»
/// Титульного листа (см. VndTitlePageService) — раньше такой истории не было вовсе: при
/// актуализации без изменений PublishAsync просто перезаписывал AdoptionDate/AdoptionCode
/// редакции новым протоколом, и прежний терялся.
///
/// ФИО и должность того, кто провёл решение (консолидировал редакцию), хранятся снимком на момент
/// решения — смена должности или увольнение сотрудника позже не переписывают задним числом уже
/// выпущенные титульные листы.</summary>
public class VndRedactionDecision
{
    public int Id { get; set; }

    public int VndRedactionId { get; set; }
    public VndRedaction? VndRedaction { get; set; }

    public VndRedactionDecisionKind Kind { get; set; }

    /// <summary>Дата решения (протокола/СЗ/приказа).</summary>
    public DateOnly? DecisionDate { get; set; }

    /// <summary>№ решения, например "12(2)" — номер протокола и номер вопроса повестки.</summary>
    public string? DecisionCode { get; set; }

    /// <summary>Дата вступления решения в силу.</summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>Кто провёл решение в системе (консолидировал редакцию). Null — решение перенесено
    /// из реквизитов редакции, созданной до появления журнала.</summary>
    public int? ActorUserId { get; set; }
    public User? ActorUser { get; set; }

    /// <summary>ФИО на момент решения (снимок).</summary>
    public string? ActorName { get; set; }

    /// <summary>Должность на момент решения (снимок).</summary>
    public string? ActorPosition { get; set; }

    public DateTime CreatedAt { get; set; }
}
