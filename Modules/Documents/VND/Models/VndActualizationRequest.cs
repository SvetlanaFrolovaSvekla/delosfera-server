using delosfera_server.Common.Models;
using delosfera_server.Modules.Users.Models;

namespace delosfera_server.Modules.Documents.VND.Models;

/*
 Заявка на доступ к актуализации ВНД - для пользователей с правами
 ActualizeVndWithApprovalByRequest / ActualizeVndWithoutApprovalByRequest.
 Решение принимает главный редактор (пользователь с правом
 ActualizeAnyVndWithApproval или ActualizeAnyVndWithoutApproval).
*/

public class VndActualizationRequest : IAuditableEntity
{
    public int Id { get; set; }

    public int VndId { get; set; }
    public VndDocument? Vnd { get; set; }

    public int RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }

    /// <summary>true - заявитель обладает правом "с последующим согласованием",
    /// false - "без согласования". Определяется тем, каким из двух
    /// прав ActualizeVnd...ByRequest он обладает (проверяется в сервисе
    /// на этапе создания заявки, здесь просто фиксируется результат).</summary>
    public bool RequiresApproval { get; set; }

    public ActualizationAccessStatus Status { get; set; } = ActualizationAccessStatus.Pending;

    public int? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }
    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}