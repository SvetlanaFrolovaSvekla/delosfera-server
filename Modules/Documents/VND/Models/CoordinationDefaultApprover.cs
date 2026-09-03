using delosfera_server.Common.Models;
using delosfera_server.Modules.Dictionaries.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/*
 Справочник обязательных (фиксированных) этапов согласования ВНД - полноценный CRUD.
 Каждая запись - это этап, который всегда обязателен в маршруте согласования, в порядке,
 заданном полем Order: название этапа, подразделение (СП), к которому обязан относиться
 согласующий, и сам согласующий по умолчанию (подставляется в конструктор маршрута, но может
 быть переопределён инициатором вручную при запуске согласования).

 Записи этого справочника не хранят "живую" ссылку на уже запущенные маршруты - при построении
 маршрута (VndApprovalService.BuildAndValidateStagesAsync) данные (Title/OrgUnitId/
 ApproverUserId) копируются в VndApprovalStage как снимок, поэтому переименование/удаление
 записи здесь не меняет историю уже согласованных документов.
*/

public class CoordinationDefaultApprover : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Название этапа, отображается в справочнике и в маршруте согласования</summary>
    public required string Title { get; set; }

    /// <summary>Порядковый номер этапа в маршруте (1,2,3...) - определяет, в каком порядке
    /// обязательные этапы должны идти в начале маршрута. Уникален, ведётся сплошным
    /// диапазоном 1..N без пропусков - см. CoordinationDefaultApproverService.Reorder.</summary>
    public int Order { get; set; }

    /// <summary>Подразделение (СП), к которому обязан относиться согласующий этого этапа</summary>
    public int OrgUnitId { get; set; }
    public OrganizationUnit? OrgUnit { get; set; }

    /// <summary>Согласующий по умолчанию для этого этапа. Может быть не задан (null) -
    /// тогда конструктор маршрута просто не подставит значение автоматически</summary>
    public int? ApproverUserId { get; set; }
    public User? ApproverUser { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
